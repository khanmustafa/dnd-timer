using System.Text.Json;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Util;

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
        var next = GetNextOccurrence(schedule);
        var triggerAt = new DateTimeOffset(next).ToUnixTimeMilliseconds();
        var alarmManager = (AlarmManager)context.GetSystemService(Context.AlarmService)!;
        var pendingIntent = CreatePendingIntent(context, schedule.Id);
        if (Build.VERSION.SdkInt < BuildVersionCodes.S || alarmManager.CanScheduleExactAlarms())
            alarmManager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAt, pendingIntent);
        else
            alarmManager.SetAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAt, pendingIntent);
    }

    public static DndSchedule? Find(Context context, int id) => Load(context).FirstOrDefault(x => x.Id == id);

    public static DateTime GetNextOccurrence(DndSchedule schedule)
    {
        var now = DateTime.Now;
        var next = new DateTime(now.Year, now.Month, now.Day, schedule.Hour, schedule.Minute, 0);
        return next <= now.AddSeconds(2) ? next.AddDays(1) : next;
    }

    public static bool HasDndAccess(Context context) =>
        ((NotificationManager)context.GetSystemService(Context.NotificationService)!).IsNotificationPolicyAccessGranted;

    public static bool HasNotificationAccess(Context context) =>
        Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu ||
        context.CheckSelfPermission(Android.Manifest.Permission.PostNotifications) == Permission.Granted;

    public static bool HasExactAlarmAccess(Context context) =>
        Build.VERSION.SdkInt < BuildVersionCodes.S ||
        ((AlarmManager)context.GetSystemService(Context.AlarmService)!).CanScheduleExactAlarms();

    public static bool IsBackgroundReady(Context context) =>
        HasDndAccess(context) && HasNotificationAccess(context) && HasExactAlarmAccess(context);

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
        try
        {
            if (!ScheduleManager.IsBackgroundReady(context))
            {
                Log.Warn("DndTimer", $"Schedule '{schedule.Title}' skipped because background setup is incomplete.");
                return;
            }
            if (!DndScheduler.Start(context, schedule.DurationMinutes, schedule.Title))
                Log.Warn("DndTimer", $"Schedule '{schedule.Title}' could not activate DND.");
        }
        catch (Exception exception)
        {
            Log.Error("DndTimer", $"Schedule '{schedule.Title}' failed: {exception}");
        }
        finally { ScheduleManager.ScheduleNext(context, schedule); }
    }
}

[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter(["android.app.action.SCHEDULE_EXACT_ALARM_PERMISSION_STATE_CHANGED"])]
public sealed class ExactAlarmPermissionReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is not null && ScheduleManager.HasExactAlarmAccess(context))
            ScheduleManager.RescheduleAll(context);
    }
}
