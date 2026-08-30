using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Provider;
using Android.Views;
using Android.Widget;
using Orientation = Android.Widget.Orientation;

namespace DndTimer;

[Activity(Label = "Schedules", Exported = false)]
public sealed class ScheduleActivity : Activity
{
    static readonly Color Page = Color.Rgb(248, 247, 252);
    static readonly Color Ink = Color.Rgb(31, 31, 38);
    static readonly Color Muted = Color.Rgb(101, 99, 112);
    static readonly Color Primary = Color.Rgb(92, 75, 168);
    static readonly Color PrimarySoft = Color.Rgb(235, 229, 255);
    LinearLayout _list = null!;
    TextView _count = null!;
    float _density;
    int Dp(int value) => (int)(value * _density);

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _density = Resources?.DisplayMetrics?.Density ?? 1f;
        Window?.SetStatusBarColor(Page);
        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetPadding(Dp(20), Dp(25), Dp(20), Dp(28)); root.SetBackgroundColor(Page);
        var header = new LinearLayout(this) { Orientation = Orientation.Horizontal }; header.SetGravity(GravityFlags.CenterVertical);
        var back = Text("‹", 36, Ink, false); back.Gravity = GravityFlags.Center; back.Clickable = true; back.Click += (_, _) => Finish();
        header.AddView(back, new LinearLayout.LayoutParams(Dp(44), Dp(48)));
        var heading = new LinearLayout(this) { Orientation = Orientation.Vertical };
        heading.AddView(Text("Daily schedules", 25, Ink, true)); _count = Text("0 of 10 schedules", 13, Muted, false); heading.AddView(_count);
        header.AddView(heading, new LinearLayout.LayoutParams(0, -2, 1)); root.AddView(header);

        var info = Text("Schedules repeat every day and start DND automatically at the selected time.", 14, Muted, false);
        info.SetPadding(Dp(16), Dp(15), Dp(16), Dp(15)); info.Background = Rounded(Color.White, 18);
        root.AddView(info, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(20) });
        _list = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.AddView(_list, new LinearLayout.LayoutParams(-1, -2));
        var add = new Button(this) { Text = "+  Add schedule", TextSize = 16 };
        add.SetTextColor(Color.White); add.SetTypeface(null, TypefaceStyle.Bold); add.BackgroundTintList = ColorStateList.ValueOf(Primary);
        add.Click += (_, _) => { if (ScheduleManager.Load(this).Count >= 10) Toast.MakeText(this, "Maximum 10 schedules reached", ToastLength.Short)?.Show(); else ShowEditor(null); };
        root.AddView(add, new LinearLayout.LayoutParams(-1, Dp(58)) { TopMargin = Dp(18) });
        var scroll = new ScrollView(this); scroll.AddView(root); SetContentView(scroll); Rebuild();
    }

    protected override void OnResume() { base.OnResume(); ScheduleManager.RescheduleAll(this); Rebuild(); }

    void Rebuild()
    {
        _list.RemoveAllViews(); var items = ScheduleManager.Load(this).OrderBy(x => x.Hour).ThenBy(x => x.Minute).ToList();
        _count.Text = $"{items.Count} of 10 schedules";
        if (items.Count == 0)
        {
            var empty = Text("No schedules yet\nAdd Fajr, Zuhr, Asr, or any quiet-time routine.", 15, Muted, false);
            empty.Gravity = GravityFlags.Center; empty.SetPadding(0, Dp(36), 0, Dp(28)); _list.AddView(empty);
        }
        foreach (var item in items) _list.AddView(BuildCard(item), new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(12) });
    }

    View BuildCard(DndSchedule item)
    {
        var card = new LinearLayout(this) { Orientation = Orientation.Vertical }; card.SetPadding(Dp(18), Dp(16), Dp(14), Dp(12)); card.Background = Rounded(Color.White, 22);
        var top = new LinearLayout(this) { Orientation = Orientation.Horizontal }; top.SetGravity(GravityFlags.CenterVertical);
        var details = new LinearLayout(this) { Orientation = Orientation.Vertical };
        details.AddView(Text(item.Title, 18, Ink, true));
        var time = DateTime.Today.AddHours(item.Hour).AddMinutes(item.Minute).ToString("h:mm tt");
        details.AddView(Text($"{time}  ·  {item.DurationMinutes} min", 14, Muted, false)); top.AddView(details, new LinearLayout.LayoutParams(0, -2, 1));
        var toggle = new Switch(this) { Checked = item.Enabled }; toggle.ButtonTintList = ColorStateList.ValueOf(Primary);
        toggle.CheckedChange += (_, args) => { item.Enabled = args.IsChecked; ScheduleManager.Save(this, item); };
        top.AddView(toggle); card.AddView(top);
        var actions = new LinearLayout(this) { Orientation = Orientation.Horizontal }; actions.SetGravity(GravityFlags.Right);
        var edit = new Button(this) { Text = "Edit" }; edit.Click += (_, _) => ShowEditor(item); actions.AddView(edit, new LinearLayout.LayoutParams(Dp(88), Dp(46)));
        var delete = new Button(this) { Text = "Delete" }; delete.SetTextColor(Color.Rgb(165, 45, 55));
        delete.Click += (_, _) => new AlertDialog.Builder(this).SetTitle("Delete schedule?").SetMessage(item.Title)
            .SetNegativeButton("Keep", (_, _) => { }).SetPositiveButton("Delete", (_, _) => { ScheduleManager.Delete(this, item.Id); Rebuild(); }).Show();
        actions.AddView(delete, new LinearLayout.LayoutParams(Dp(96), Dp(46))); card.AddView(actions); return card;
    }

    void ShowEditor(DndSchedule? existing)
    {
        var model = existing is null ? new DndSchedule { Hour = DateTime.Now.Hour, Minute = DateTime.Now.Minute, DurationMinutes = 30 } : new DndSchedule
        { Id = existing.Id, Title = existing.Title, Hour = existing.Hour, Minute = existing.Minute, DurationMinutes = existing.DurationMinutes, Enabled = existing.Enabled };
        var form = new LinearLayout(this) { Orientation = Orientation.Vertical }; form.SetPadding(Dp(20), 0, Dp(20), 0);
        var title = new EditText(this) { Hint = "Title, e.g. Fajr", Text = model.Title == "Quiet time" ? "" : model.Title };
        title.SetSingleLine(true); form.AddView(title);
        var picker = new TimePicker(this); picker.SetIs24HourView(Java.Lang.Boolean.False); picker.Hour = model.Hour; picker.Minute = model.Minute; form.AddView(picker);
        var durationLabel = Text($"Duration: {model.DurationMinutes} minutes", 16, Ink, true); form.AddView(durationLabel);
        var duration = new SeekBar(this) { Max = 119, Progress = model.DurationMinutes - 1 }; duration.ProgressTintList = ColorStateList.ValueOf(Primary); duration.ThumbTintList = ColorStateList.ValueOf(Primary);
        duration.ProgressChanged += (_, args) => durationLabel.Text = $"Duration: {args.Progress + 1} minutes"; form.AddView(duration);
        new AlertDialog.Builder(this).SetTitle(existing is null ? "New daily schedule" : "Edit schedule").SetView(form)
            .SetNegativeButton("Cancel", (_, _) => { }).SetPositiveButton("Save", (_, _) =>
            {
                model.Title = string.IsNullOrWhiteSpace(title.Text) ? "Quiet time" : title.Text.Trim(); model.Hour = picker.Hour; model.Minute = picker.Minute;
                model.DurationMinutes = duration.Progress + 1; model.Enabled = true; ScheduleManager.Save(this, model); EnsureExactAlarmAccess(); Rebuild();
            }).Show();
    }

    void EnsureExactAlarmAccess()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.S || ((AlarmManager)GetSystemService(AlarmService)!).CanScheduleExactAlarms()) return;
        try { var intent = new Intent(Settings.ActionRequestScheduleExactAlarm, Android.Net.Uri.Parse($"package:{PackageName}")); StartActivity(intent); }
        catch (ActivityNotFoundException) { }
        Toast.MakeText(this, "Allow Alarms & reminders for on-time schedules", ToastLength.Long)?.Show();
    }

    TextView Text(string text, float size, Color color, bool bold) { var v = new TextView(this) { Text = text, TextSize = size }; v.SetTextColor(color); if (bold) v.SetTypeface(null, TypefaceStyle.Bold); return v; }
    GradientDrawable Rounded(Color color, int radius) { var d = new GradientDrawable(); d.SetColor(color); d.SetCornerRadius(Dp(radius)); return d; }
}
