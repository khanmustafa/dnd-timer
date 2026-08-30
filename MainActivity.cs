using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Provider;
using Android.Views;
using Android.Widget;
using Orientation = Android.Widget.Orientation;

namespace DndTimer;

[Activity(Label = "DND Timer", MainLauncher = true, Exported = true)]
public sealed class MainActivity : Activity
{
    static readonly Color PageColor = Color.Rgb(248, 247, 252);
    static readonly Color InkColor = Color.Rgb(31, 31, 38);
    static readonly Color MutedColor = Color.Rgb(101, 99, 112);
    static readonly Color PrimaryColor = Color.Rgb(92, 75, 168);
    static readonly Color PrimarySoftColor = Color.Rgb(235, 229, 255);
    static readonly Color ActiveColor = Color.Rgb(44, 116, 91);
    static readonly Color ActiveSoftColor = Color.Rgb(221, 245, 235);
    TextView _minutesLabel = null!, _durationHint = null!, _statusTitle = null!, _statusLabel = null!, _statusIcon = null!;
    LinearLayout _statusCard = null!;
    Button _startButton = null!, _stopButton = null!;
    readonly Handler _handler = new(Looper.MainLooper!);
    Action? _ticker;
    float _density;
    int Dp(int value) => (int)(value * _density);

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _density = Resources?.DisplayMetrics?.Density ?? 1f;
        Window?.SetStatusBarColor(PageColor);
        Window?.SetNavigationBarColor(PageColor);
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            Window!.DecorView.SystemUiVisibility = (StatusBarVisibility)SystemUiFlags.LightStatusBar;

        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetPadding(Dp(20), Dp(28), Dp(20), Dp(28));
        root.SetBackgroundColor(PageColor);

        var brandRow = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        brandRow.SetGravity(GravityFlags.CenterVertical);
        var brandIcon = Label("◑", 25, PrimaryColor, true);
        brandIcon.Gravity = GravityFlags.Center;
        brandIcon.Background = Rounded(PrimarySoftColor, 18);
        brandRow.AddView(brandIcon, new LinearLayout.LayoutParams(Dp(48), Dp(48)));
        var brandText = new LinearLayout(this) { Orientation = Orientation.Vertical };
        brandText.SetPadding(Dp(14), 0, 0, 0);
        brandText.AddView(Label("DND Timer", 25, InkColor, true));
        brandText.AddView(Label("Quiet time, on your terms", 14, MutedColor));
        brandRow.AddView(brandText, new LinearLayout.LayoutParams(0, -2, 1));
        root.AddView(brandRow);

        _statusCard = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        _statusCard.SetGravity(GravityFlags.CenterVertical);
        _statusCard.SetPadding(Dp(18), Dp(17), Dp(18), Dp(17));
        _statusCard.Background = Rounded(Color.White, 22);
        root.AddView(_statusCard, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(26) });
        _statusIcon = Label("✓", 18, MutedColor, true);
        _statusIcon.Gravity = GravityFlags.Center;
        _statusIcon.Background = Rounded(Color.Rgb(239, 238, 244), 16);
        _statusCard.AddView(_statusIcon, new LinearLayout.LayoutParams(Dp(44), Dp(44)));
        var statusText = new LinearLayout(this) { Orientation = Orientation.Vertical };
        statusText.SetPadding(Dp(14), 0, 0, 0);
        _statusTitle = Label("Ready when you are", 16, InkColor, true);
        _statusLabel = Label("No quiet session running", 13, MutedColor);
        statusText.AddView(_statusTitle);
        statusText.AddView(_statusLabel);
        _statusCard.AddView(statusText, new LinearLayout.LayoutParams(0, -2, 1));

        var durationCard = new LinearLayout(this) { Orientation = Orientation.Vertical };
        durationCard.SetPadding(Dp(20), Dp(22), Dp(20), Dp(20));
        durationCard.Background = Rounded(Color.White, 26);
        root.AddView(durationCard, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(16) });
        durationCard.AddView(Label("QUIET DURATION", 12, MutedColor, true));
        _minutesLabel = Label("30", 52, InkColor, true);
        _minutesLabel.Gravity = GravityFlags.Center;
        durationCard.AddView(_minutesLabel, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(14) });
        _durationHint = Label("minutes", 16, MutedColor);
        _durationHint.Gravity = GravityFlags.Center;
        durationCard.AddView(_durationHint);

        var slider = new SeekBar(this) { Max = 120, Progress = 30 };
        slider.ProgressTintList = ColorStateList.ValueOf(PrimaryColor);
        slider.ThumbTintList = ColorStateList.ValueOf(PrimaryColor);
        durationCard.AddView(slider, new LinearLayout.LayoutParams(-1, Dp(54)) { TopMargin = Dp(10) });
        var rangeRow = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        rangeRow.AddView(Label("0 min", 12, MutedColor), new LinearLayout.LayoutParams(0, -2, 1));
        var maxLabel = Label("120 min", 12, MutedColor); maxLabel.Gravity = GravityFlags.Right;
        rangeRow.AddView(maxLabel, new LinearLayout.LayoutParams(0, -2, 1));
        durationCard.AddView(rangeRow);

        durationCard.AddView(Label("Quick picks", 13, InkColor, true), new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(22) });
        var presetsScroll = new HorizontalScrollView(this) { HorizontalScrollBarEnabled = false };
        var presets = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        foreach (var value in new[] { 15, 30, 45, 60, 90, 120 })
        {
            var text = value < 60 ? $"{value}m" : value == 60 ? "1h" : value == 90 ? "1h 30m" : "2h";
            var chip = Label(text, 14, PrimaryColor, true);
            chip.Gravity = GravityFlags.Center; chip.SetPadding(Dp(16), 0, Dp(16), 0);
            chip.Background = Rounded(PrimarySoftColor, 18); chip.Clickable = true;
            chip.Click += (_, _) => slider.Progress = value;
            presets.AddView(chip, new LinearLayout.LayoutParams(-2, Dp(40)) { RightMargin = Dp(9), TopMargin = Dp(10) });
        }
        presetsScroll.AddView(presets); durationCard.AddView(presetsScroll, new LinearLayout.LayoutParams(-1, Dp(58)));

        _startButton = new Button(this) { Text = "Start quiet time", TextSize = 16 };
        _startButton.SetTextColor(Color.White); _startButton.SetTypeface(null, TypefaceStyle.Bold);
        _startButton.BackgroundTintList = ColorStateList.ValueOf(PrimaryColor);
        _startButton.Click += (_, _) => StartTimer(slider.Progress);
        root.AddView(_startButton, new LinearLayout.LayoutParams(-1, Dp(60)) { TopMargin = Dp(20) });
        _stopButton = new Button(this) { Text = "Cancel quiet time", TextSize = 16, Visibility = ViewStates.Gone };
        _stopButton.SetTextColor(Color.Rgb(151, 48, 55)); _stopButton.SetTypeface(null, TypefaceStyle.Bold);
        _stopButton.BackgroundTintList = ColorStateList.ValueOf(Color.Rgb(255, 232, 232));
        _stopButton.Click += (_, _) => { DndScheduler.Cancel(this, true); UpdateStatus(); };
        root.AddView(_stopButton, new LinearLayout.LayoutParams(-1, Dp(60)) { TopMargin = Dp(20) });
        var footnote = Label("Alarms are silenced during quiet time. Your previous sound mode returns automatically.", 12, MutedColor);
        footnote.Gravity = GravityFlags.Center; footnote.SetPadding(Dp(12), Dp(15), Dp(12), 0); root.AddView(footnote);

        var scroll = new ScrollView(this) { FillViewport = true }; scroll.AddView(root); SetContentView(scroll);
        slider.ProgressChanged += (_, args) =>
        {
            _minutesLabel.Text = args.Progress.ToString(); _durationHint.Text = args.Progress == 1 ? "minute" : "minutes";
            _startButton.Enabled = args.Progress > 0; _startButton.Alpha = args.Progress > 0 ? 1f : .45f;
        };
        _ticker = () => { UpdateStatus(); _handler.PostDelayed(_ticker!, 1000); };
    }

    TextView Label(string text, float size, Color color, bool bold = false)
    {
        var view = new TextView(this) { Text = text, TextSize = size }; view.SetTextColor(color);
        if (bold) view.SetTypeface(null, TypefaceStyle.Bold); return view;
    }

    GradientDrawable Rounded(Color color, int radius)
    {
        var drawable = new GradientDrawable(); drawable.SetColor(color); drawable.SetCornerRadius(Dp(radius)); return drawable;
    }

    protected override void OnResume() { base.OnResume(); _handler.RemoveCallbacks(_ticker!); _handler.Post(_ticker!); }
    protected override void OnPause() { _handler.RemoveCallbacks(_ticker!); base.OnPause(); }

    void StartTimer(int minutes)
    {
        if (minutes <= 0) return;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu && CheckSelfPermission(Android.Manifest.Permission.PostNotifications) != Permission.Granted)
        {
            RequestPermissions([Android.Manifest.Permission.PostNotifications], 2001);
            Toast.MakeText(this, "Allow notifications, then tap Start again.", ToastLength.Long)?.Show(); return;
        }
        var notificationManager = (NotificationManager)GetSystemService(NotificationService)!;
        if (!notificationManager.IsNotificationPolicyAccessGranted)
        {
            Toast.MakeText(this, "Allow DND Timer, then tap Start again.", ToastLength.Long)?.Show();
            StartActivity(new Intent(Settings.ActionNotificationPolicyAccessSettings)); return;
        }
        if (Build.VERSION.SdkInt >= BuildVersionCodes.S && !((AlarmManager)GetSystemService(AlarmService)!).CanScheduleExactAlarms())
        {
            try
            {
                var intent = new Intent(Settings.ActionRequestScheduleExactAlarm);
                intent.SetData(Android.Net.Uri.Parse($"package:{PackageName}")); StartActivity(intent);
                Toast.MakeText(this, "Allow exact alarms, then tap Start again.", ToastLength.Long)?.Show(); return;
            }
            catch (ActivityNotFoundException) { }
        }
        DndScheduler.Start(this, minutes); UpdateStatus();
    }

    void UpdateStatus()
    {
        var remaining = DndScheduler.GetRemainingMilliseconds(this); var active = remaining > 0;
        _startButton.Visibility = active ? ViewStates.Gone : ViewStates.Visible;
        _stopButton.Visibility = active ? ViewStates.Visible : ViewStates.Gone;
        if (!active)
        {
            _statusTitle.Text = "Ready when you are"; _statusLabel.Text = "No quiet session running"; _statusIcon.Text = "✓";
            _statusIcon.SetTextColor(MutedColor); _statusIcon.Background = Rounded(Color.Rgb(239, 238, 244), 16);
            _statusCard.Background = Rounded(Color.White, 22); return;
        }
        var span = TimeSpan.FromMilliseconds(remaining);
        var clock = span.TotalHours >= 1 ? $"{(int)span.TotalHours}:{span.Minutes:D2}:{span.Seconds:D2}" : $"{span.Minutes:D2}:{span.Seconds:D2}";
        _statusTitle.Text = "Quiet time is active"; _statusLabel.Text = $"{clock} remaining"; _statusIcon.Text = "☾";
        _statusIcon.SetTextColor(ActiveColor); _statusIcon.Background = Rounded(Color.White, 16); _statusCard.Background = Rounded(ActiveSoftColor, 22);
    }
}
