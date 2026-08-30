# DND Timer for Android

A compact native **.NET for Android** app that enables total-silence Do Not Disturb for a duration chosen by the user. Select between 0 and 120 minutes, start a quiet session, and let the app restore the previous interruption mode automatically.

The launcher icon combines a quiet crescent moon with a small clock detail to represent timed silence.

Current version: **2.3**

## Features

- Material 3 interface using the official Android Material Components library
- Duration slider from 0 to 120 minutes
- Quick presets for 15, 30, 45, 60, 90, and 120 minutes
- Up to 10 named daily schedules such as Fajr, Zuhr, and Asr
- Enable, disable, edit, or delete each schedule independently
- Background-readiness checklist for DND, notification, and exact-alarm access
- Schedules remain paused until all required background permissions are ready
- Automatic schedule recovery after reboot, app update, clock change, or time-zone change
- Live countdown in the app and notification panel
- **Cancel DND** action directly in the notification
- Automatic restoration when the countdown expires
- Foreground countdown service with an exact-alarm fallback
- Timer recovery after a device restart
- Android 15-compatible app-specific DND behavior
- Native C# implementation using the official Android Material Components binding
- Material 3 bottom-sheet schedule editor with the duration slider above Save

## Requirements

- .NET 9 SDK
- .NET Android workload
- Android SDK API 35
- Android 6.0 (API 23) or newer device

The UI uses `Xamarin.Google.Android.Material` 1.14.0.6, the official .NET binding for Android Material Components.

Install the workload with:

```powershell
dotnet workload install android
```

## Build an APK

From this project directory:

```powershell
dotnet publish .\DndTimer.csproj -c Release -f net9.0-android
```

The signed APK is generated in:

```text
bin\Release\net9.0-android\publish\com.khans.dndtimer-Signed.apk
```

If your Android SDK is in a custom location:

```powershell
dotnet publish .\DndTimer.csproj -c Release -f net9.0-android `
  -p:AndroidSdkDirectory="C:\path\to\android-sdk"
```

## First-time setup

Android controls these privileges through system settings. The app guides the user to the relevant screen when access is missing:

1. Allow notification permission on Android 13 and newer.
2. Grant **Do Not Disturb access** to DND Timer.
3. Grant **Alarms & reminders** access on Android 12 and newer.

The notification remains visible while a session is active because Android uses it for the foreground countdown service.

### Background setup

The schedule screen displays a readiness checklist. All three items must be ready before a schedule can be enabled:

1. **Do Not Disturb access** allows the app to activate and restore DND.
2. **Notifications allowed** lets Android display the required foreground countdown notification.
3. **Exact alarms allowed** lets daily schedules run at the selected time, including while the device is idle.

If setup is incomplete, a new schedule is saved in a paused state. Select **Fix setup**, grant the requested system access, return to the app, and enable the schedule. Granting exact-alarm access causes the app to reschedule all enabled entries immediately.

## Daily schedules

Open **Manage daily schedules** from the main screen, then select **Add schedule**. Each entry contains:

- A title
- A daily start time
- A DND duration from 1 to 120 minutes
- An enabled/disabled toggle

The app stores all schedules locally on the device. Up to 10 schedules can be active. When schedules overlap, DND remains active until the latest scheduled end time and then restores the sound mode that was present before the first schedule began.

## How restoration works

When a session starts, the app saves the current interruption filter before enabling total silence. It schedules an exact alarm and starts a foreground countdown service. Either path can restore the saved filter when time expires, remove the notification, and clear the timer state.

On Android 15 and newer, third-party apps no longer directly control the global DND state. Android represents the app's request as an app-specific automatic rule; ending the session deactivates this app's contribution without disabling unrelated DND rules.

## Project structure

- `MainActivity.cs` — Android 14-style UI and permission flow
- `DndScheduler.cs` — DND state, countdown service, alarms, notification, and receivers
- `ScheduleManager.cs` — schedule persistence, daily alarm calculation, and background trigger receiver
- `ScheduleActivity.cs` — Material 3 schedule list, readiness checklist, and bottom-sheet editor
- `Resources/values/styles.xml` — Material 3 application theme
- `Resources/mipmap-*` — density-specific launcher icons
- `Properties/AndroidManifest.xml` — required Android permissions
- `DndTimer.csproj` — .NET Android project configuration

## Install on a connected device

With Android platform tools installed and USB debugging enabled:

```powershell
adb install -r .\bin\Release\net9.0-android\publish\com.khans.dndtimer-Signed.apk
```

## Important

Device manufacturers may apply additional battery restrictions. If the timer is interrupted on a heavily customized Android device, allow DND Timer to run in the background or exclude it from vendor battery optimization.

If a schedule does not activate:

1. Open **Manage daily schedules**.
2. Confirm the background checklist reports **Ready for automatic schedules**.
3. Confirm the schedule toggle is enabled and its next-run time is correct.
4. Check the manufacturer's battery settings and allow background activity for DND Timer.
