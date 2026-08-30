using Android.App;
using Android.Content;
using Android.OS;

namespace DndTimer;

public static class DndScheduler
{
    const string PreferencesName = "dnd_timer_state";
    const string EndTimeKey = "end_time_unix_ms";
    const string PreviousFilterKey = "previous_filter";
    const string ActiveTitleKey = "active_title";
    const int AlarmRequestCode = 2401;
    const int CancelRequestCode = 2402;
    internal const int NotificationId = 2403;
    const string NotificationChannelId = "active_dnd_timer";

    public static bool Start(Context context, int minutes, string? title = null)
    {
        var manager = (NotificationManager)context.GetSystemService(Context.NotificationService)!;
        if (!manager.IsNotificationPolicyAccessGranted)
        {
            Android.Util.Log.Warn("DndTimer", "DND activation skipped because notification policy access is missing.");
            return false;
        }
        var preferences = context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)!;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var previousEnd = preferences.GetLong(EndTimeKey, 0);
        var requestedEnd = DateTimeOffset.UtcNow.AddMinutes(minutes).ToUnixTimeMilliseconds();
        var alreadyActive = previousEnd > now;
        if (!alreadyActive) Cancel(context, restoreDnd: false);

        preferences = context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)!;
        var editor = preferences.Edit()!;
        if (!alreadyActive) editor.PutInt(PreviousFilterKey, (int)manager.CurrentInterruptionFilter);
        var effectiveEnd = Math.Max(previousEnd, requestedEnd);
        editor.PutLong(EndTimeKey, effectiveEnd);
        if (!string.IsNullOrWhiteSpace(title) && requestedEnd >= previousEnd) editor.PutString(ActiveTitleKey, title);
        else if (!alreadyActive) editor.PutString(ActiveTitleKey, "Quiet time");
        editor.Apply();

        manager.SetInterruptionFilter(InterruptionFilter.None);
        Schedule(context, TimeSpan.FromMilliseconds(effectiveEnd - now));
        StartCountdownService(context);
        return true;
    }

    public static void Cancel(Context context, bool restoreDnd)
    {
        var alarmManager = (AlarmManager)context.GetSystemService(Context.AlarmService)!;
        alarmManager.Cancel(CreatePendingIntent(context));

        var notificationManager = (NotificationManager)context.GetSystemService(Context.NotificationService)!;
        notificationManager.Cancel(NotificationId);
        context.StopService(new Intent(context, typeof(DndCountdownService)));

        var preferences = context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)!;
        if (restoreDnd)
        {
            var manager = (NotificationManager)context.GetSystemService(Context.NotificationService)!;
            if (manager.IsNotificationPolicyAccessGranted)
            {
                // Android 15+ deactivates this app's implicit DND rule when ALL is set.
                var filter = Build.VERSION.SdkInt >= BuildVersionCodes.VanillaIceCream
                    ? InterruptionFilter.All
                    : (InterruptionFilter)preferences.GetInt(PreviousFilterKey, (int)InterruptionFilter.All);
                manager.SetInterruptionFilter(filter);
            }
        }

        preferences.Edit()!.Remove(EndTimeKey)!.Remove(PreviousFilterKey)!.Remove(ActiveTitleKey)!.Apply();
    }

    public static long GetRemainingMilliseconds(Context context)
    {
        var end = context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)!
            .GetLong(EndTimeKey, 0);
        return Math.Max(0, end - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    public static void ResumeAfterBoot(Context context)
    {
        var remaining = GetRemainingMilliseconds(context);
        if (remaining <= 0) Cancel(context, restoreDnd: true);
        else
        {
            var delay = TimeSpan.FromMilliseconds(remaining);
            Schedule(context, delay);
            StartCountdownService(context);
        }
    }

    static void Schedule(Context context, TimeSpan delay)
    {
        var alarmManager = (AlarmManager)context.GetSystemService(Context.AlarmService)!;
        var triggerAt = SystemClock.ElapsedRealtime() + (long)delay.TotalMilliseconds;
        if (Build.VERSION.SdkInt < BuildVersionCodes.S || alarmManager.CanScheduleExactAlarms())
            alarmManager.SetExactAndAllowWhileIdle(AlarmType.ElapsedRealtimeWakeup, triggerAt, CreatePendingIntent(context));
        else
            alarmManager.SetAndAllowWhileIdle(AlarmType.ElapsedRealtimeWakeup, triggerAt, CreatePendingIntent(context));
    }

    static PendingIntent CreatePendingIntent(Context context)
    {
        var intent = new Intent(context, typeof(DndExpiryReceiver));
        return PendingIntent.GetBroadcast(context, AlarmRequestCode, intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    }

    static void StartCountdownService(Context context)
    {
        var intent = new Intent(context, typeof(DndCountdownService));
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O) context.StartForegroundService(intent);
        else context.StartService(intent);
    }

    internal static Notification BuildNotification(Context context, TimeSpan remaining)
    {
        var manager = (NotificationManager)context.GetSystemService(Context.NotificationService)!;
        var activeTitle = context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)!
            .GetString(ActiveTitleKey, "Quiet time") ?? "Quiet time";
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(
                NotificationChannelId,
                "Active DND timer",
                NotificationImportance.Low)
            {
                Description = "Shows the remaining Do Not Disturb time"
            };
            channel.SetSound(null, null);
            manager.CreateNotificationChannel(channel);
        }

        var openIntent = new Intent(context, typeof(MainActivity));
        openIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        var openPendingIntent = PendingIntent.GetActivity(context, 0, openIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var cancelIntent = new Intent(context, typeof(DndCancelReceiver));
        var cancelPendingIntent = PendingIntent.GetBroadcast(context, CancelRequestCode, cancelIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var endTime = Java.Lang.JavaSystem.CurrentTimeMillis() + (long)remaining.TotalMilliseconds;
        var builder = Build.VERSION.SdkInt >= BuildVersionCodes.O
            ? new Notification.Builder(context, NotificationChannelId)
            : new Notification.Builder(context);

        builder
            .SetSmallIcon(Android.Resource.Drawable.IcDialogInfo)
            .SetContentTitle($"{activeTitle} · DND active")
            .SetContentText("Time remaining")
            .SetContentIntent(openPendingIntent)
            .SetOngoing(true)
            .SetOnlyAlertOnce(true)
            .SetWhen(endTime)
            .SetUsesChronometer(true)
            .AddAction(Android.Resource.Drawable.IcMenuCloseClearCancel, "Cancel DND", cancelPendingIntent);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
            builder.SetChronometerCountDown(true);

        return builder.Build();
    }
}

[Service(Enabled = true, Exported = false,
    ForegroundServiceType = Android.Content.PM.ForegroundService.TypeSpecialUse)]
[MetaData(Android.Content.PM.PackageManager.PropertySpecialUseFgsSubtype,
    Value = "Keeps a user-started DND countdown reliable and restores sound at expiry")]
public sealed class DndCountdownService : Service
{
    readonly Handler _handler = new(Looper.MainLooper!);
    Action? _expiryAction;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        var remaining = DndScheduler.GetRemainingMilliseconds(this);
        var displayTime = TimeSpan.FromMilliseconds(Math.Max(remaining, 1000));
        StartForeground(DndScheduler.NotificationId, DndScheduler.BuildNotification(this, displayTime));

        if (_expiryAction is not null) _handler.RemoveCallbacks(_expiryAction);
        _expiryAction = () =>
        {
            DndScheduler.Cancel(this, restoreDnd: true);
            StopSelf();
        };

        if (remaining <= 0) _handler.Post(_expiryAction);
        else _handler.PostDelayed(_expiryAction, remaining);
        return StartCommandResult.Sticky;
    }

    public override void OnDestroy()
    {
        if (_expiryAction is not null) _handler.RemoveCallbacks(_expiryAction);
        base.OnDestroy();
    }
}

[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class DndExpiryReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is not null) DndScheduler.Cancel(context, restoreDnd: true);
    }
}

[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class DndCancelReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is not null) DndScheduler.Cancel(context, restoreDnd: true);
    }
}

[BroadcastReceiver(Enabled = true, Exported = true, DirectBootAware = true)]
[IntentFilter([Intent.ActionBootCompleted, Intent.ActionMyPackageReplaced, Intent.ActionTimeChanged, Intent.ActionTimezoneChanged])]
public sealed class BootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is not null)
        {
            DndScheduler.ResumeAfterBoot(context);
            ScheduleManager.RescheduleAll(context);
        }
    }
}
