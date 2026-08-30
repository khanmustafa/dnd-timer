using System.Text.Json;
using Android.App;
using Android.Content;
using Android.OS;

namespace DndTimer;

public sealed class DndSchedule
{
    public int Id { get; set; }
    public string Title { get; set; } = "Quiet time";
    public int Hour { get; set; }
    public int Minute { get; set; }
    public int DurationMinutes { get; set; } = 30;
    public bool Enabled { get; set; } = true;
}

public static class ScheduleManager
{
    const string PreferencesName = "dnd_schedules";
    const string SchedulesKey = "items";
    const string NextIdKey = "next_id";
    const int RequestCodeBase = 5000;

    public static List<DndSchedule> Load(Context context)
    {
        var json = context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)!
            .GetString(SchedulesKey, "[]") ?? "[]";
        try { return JsonSerializer.Deserialize<List<DndSchedule>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    public static bool Save(Context context, DndSchedule schedule)
    {
        var items = Load(context);
        if (schedule.Id == 0)
        {
            if (items.Count >= 10) return false;
            var prefs = context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)!;
            schedule.Id = prefs.GetInt(NextIdKey, 1);
            prefs.Edit()!.PutInt(NextIdKey, schedule.Id + 1)!.Apply();
            items.Add(schedule);
        }
        else
        {
            var index = items.FindIndex(x => x.Id == schedule.Id);
            if (index < 0) return false;
            items[index] = schedule;
        }

        Persist(context, items);
        if (schedule.Enabled) ScheduleNext(context, schedule);
        else CancelAlarm(context, schedule.Id);
        return true;
    }

    public static void Delete(Context context, int id)
    {
        var items = Load(context);
        items.RemoveAll(x => x.Id == id);
        Persist(context, items);
        CancelAlarm(context, id);
    }

    public static void RescheduleAll(Context context)
    {
        foreach (var schedule in Load(context))
        {
            CancelAlarm(context, schedule.Id);
            if (schedule.Enabled) ScheduleNext(context, schedule);
        }
    }

    public static void ScheduleNext(Context context, DndSchedule schedule)
    {
        var now = DateTime.Now;
        var next = new DateTime(now.Year, now.Month, now.Day, schedule.Hour, schedule.Minute, 0);
        if (next <= now.AddSeconds(2)) next = next.AddDays(1);
        var triggerAt = new DateTimeOffset(next).ToUnixTimeMilliseconds();
        var alarmManager = (AlarmManager)context.GetSystemService(Context.AlarmService)!;
        var pendingIntent = CreatePendingIntent(context, schedule.Id);
        if (Build.VERSION.SdkInt < BuildVersionCodes.S || alarmManager.CanScheduleExactAlarms())
            alarmManager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAt, pendingIntent);
        else
            alarmManager.SetAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAt, pendingIntent);
    }

    public static DndSchedule? Find(Context context, int id) => Load(context).FirstOrDefault(x => x.Id == id);

    static void Persist(Context context, List<DndSchedule> schedules)
    {
        context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)!
            .Edit()!.PutString(SchedulesKey, JsonSerializer.Serialize(schedules))!.Apply();
    }

    static void CancelAlarm(Context context, int id)
    {
        var manager = (AlarmManager)context.GetSystemService(Context.AlarmService)!;
        manager.Cancel(CreatePendingIntent(context, id));
    }

    static PendingIntent CreatePendingIntent(Context context, int id)
    {
        var intent = new Intent(context, typeof(ScheduleTriggerReceiver));
        intent.PutExtra("schedule_id", id);
        return PendingIntent.GetBroadcast(context, RequestCodeBase + id, intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    }
}

[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class ScheduleTriggerReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null) return;
        var schedule = ScheduleManager.Find(context, intent?.GetIntExtra("schedule_id", 0) ?? 0);
        if (schedule is null || !schedule.Enabled) return;
        try { DndScheduler.Start(context, schedule.DurationMinutes, schedule.Title); }
        finally { ScheduleManager.ScheduleNext(context, schedule); }
    }
}
