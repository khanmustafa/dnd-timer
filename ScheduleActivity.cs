using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.Graphics;
using Android.OS;
using Android.Provider;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using Google.Android.Material.BottomSheet;
using Google.Android.Material.Button;
using Google.Android.Material.Card;
using Google.Android.Material.TextField;
using Orientation = Android.Widget.Orientation;

namespace DndTimer;

[Activity(Label = "Schedules", Exported = false, Theme = "@style/AppTheme")]
public sealed class ScheduleActivity : AppCompatActivity
{
    static readonly Color Surface = Color.Rgb(255, 251, 255);
    static readonly Color SurfaceContainer = Color.Rgb(244, 239, 247);
    static readonly Color OnSurface = Color.Rgb(33, 31, 38);
    static readonly Color OnSurfaceVariant = Color.Rgb(98, 95, 102);
    static readonly Color Primary = Color.Rgb(103, 80, 164);
    static readonly Color PrimaryContainer = Color.Rgb(234, 221, 255);
    static readonly Color Success = Color.Rgb(36, 122, 82);
    static readonly Color Warning = Color.Rgb(139, 80, 0);
    LinearLayout _list = null!, _readinessItems = null!;
    TextView _count = null!, _readinessTitle = null!;
    MaterialButton _fixSetup = null!, _addButton = null!;
    float _density;
    int Dp(int value) => (int)(value * _density);

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _density = Resources?.DisplayMetrics?.Density ?? 1f;
        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetPadding(Dp(18), Dp(52), Dp(18), Dp(28)); root.SetBackgroundColor(Surface);

        var header = Row();
        var back = IconButton("‹"); back.ContentDescription = "Back"; back.Click += (_, _) => Finish();
        header.AddView(back, new LinearLayout.LayoutParams(Dp(48), Dp(48)));
        var heading = Column(); heading.AddView(Text("Daily schedules", 26, OnSurface, true));
        _count = Text("0 of 10 schedules", 13, OnSurfaceVariant); heading.AddView(_count);
        header.AddView(heading, new LinearLayout.LayoutParams(0, -2, 1)); root.AddView(header);

        var readinessCard = Card(PrimaryContainer);
        var readinessContent = Column(); readinessContent.SetPadding(Dp(16), Dp(15), Dp(16), Dp(12));
        _readinessTitle = Text("Background setup", 16, Primary, true); readinessContent.AddView(_readinessTitle);
        _readinessItems = Column(); readinessContent.AddView(_readinessItems);
        _fixSetup = new MaterialButton(this) { Text = "Fix setup" };
        _fixSetup.SetTextColor(Primary); _fixSetup.BackgroundTintList = ColorStateList.ValueOf(Color.Transparent);
        _fixSetup.Click += (_, _) => FixNextMissingPermission(); readinessContent.AddView(_fixSetup, new LinearLayout.LayoutParams(-2, Dp(48)));
        readinessCard.AddView(readinessContent); root.AddView(readinessCard, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(20) });

        var section = Row(); section.AddView(Text("Your schedules", 18, OnSurface, true), new LinearLayout.LayoutParams(0, -2, 1));
        var sectionCount = Text("Tap a schedule to edit", 12, OnSurfaceVariant); section.AddView(sectionCount);
        root.AddView(section, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(24), BottomMargin = Dp(2) });
        _list = Column(); root.AddView(_list);

        _addButton = FilledButton("＋  Add schedule"); _addButton.Click += (_, _) =>
        {
            if (ScheduleManager.Load(this).Count >= 10) Toast.MakeText(this, "Maximum 10 schedules reached", ToastLength.Short)?.Show();
            else ShowEditor(null);
        };
        root.AddView(_addButton, new LinearLayout.LayoutParams(-1, Dp(58)) { TopMargin = Dp(18) });
        var scroll = new ScrollView(this) { FillViewport = true }; scroll.AddView(root); SetContentView(scroll);
    }

    protected override void OnResume()
    {
        base.OnResume();
        ScheduleManager.RescheduleAll(this);
        RefreshReadiness(); Rebuild();
    }

    void RefreshReadiness()
    {
        _readinessItems.RemoveAllViews();
        AddReadiness("Do Not Disturb access", ScheduleManager.HasDndAccess(this));
        AddReadiness("Notifications allowed", ScheduleManager.HasNotificationAccess(this));
        AddReadiness("Exact alarms allowed", ScheduleManager.HasExactAlarmAccess(this));
        var ready = ScheduleManager.IsBackgroundReady(this);
        _readinessTitle.Text = ready ? "Ready for automatic schedules" : "Background setup required";
        _readinessTitle.SetTextColor(ready ? Success : Primary);
        _fixSetup.Visibility = ready ? ViewStates.Gone : ViewStates.Visible;
    }

    void AddReadiness(string label, bool ready)
    {
        var line = Text($"{(ready ? "✓" : "!")}   {label}", 13, ready ? Success : Warning, true);
        line.SetPadding(0, Dp(9), 0, 0); _readinessItems.AddView(line);
    }

    void FixNextMissingPermission()
    {
        if (!ScheduleManager.HasDndAccess(this))
        {
            StartActivity(new Intent(Settings.ActionNotificationPolicyAccessSettings)); return;
        }
        if (!ScheduleManager.HasNotificationAccess(this) && Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
        {
            RequestPermissions([Android.Manifest.Permission.PostNotifications], 2201); return;
        }
        if (!ScheduleManager.HasExactAlarmAccess(this) && Build.VERSION.SdkInt >= BuildVersionCodes.S)
        {
            try { StartActivity(new Intent(Settings.ActionRequestScheduleExactAlarm, Android.Net.Uri.Parse($"package:{PackageName}"))); }
            catch (ActivityNotFoundException) { }
        }
    }

    void Rebuild()
    {
        _list.RemoveAllViews();
        var items = ScheduleManager.Load(this).OrderBy(x => x.Hour).ThenBy(x => x.Minute).ToList();
        _count.Text = $"{items.Count} of 10 schedules"; _addButton.Enabled = items.Count < 10;
        if (items.Count == 0)
        {
            var empty = Text("No schedules yet\nCreate a named daily quiet-time routine.", 15, OnSurfaceVariant);
            empty.Gravity = GravityFlags.Center; empty.SetPadding(0, Dp(40), 0, Dp(30)); _list.AddView(empty); return;
        }
        foreach (var item in items) _list.AddView(BuildScheduleCard(item), new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(10) });
    }

    View BuildScheduleCard(DndSchedule item)
    {
        var card = Card(SurfaceContainer); card.Clickable = true; card.Focusable = true; card.Click += (_, _) => ShowEditor(item);
        var content = Column(); content.SetPadding(Dp(16), Dp(15), Dp(12), Dp(13));
        var top = Row(); var details = Column(); details.AddView(Text(item.Title, 18, OnSurface, true));
        var time = DateTime.Today.AddHours(item.Hour).AddMinutes(item.Minute).ToString("h:mm tt"); details.AddView(Text(time, 22, OnSurface, true));
        details.AddView(Text($"{item.DurationMinutes} minutes · Repeats daily", 13, OnSurfaceVariant)); top.AddView(details, new LinearLayout.LayoutParams(0, -2, 1));
        var toggle = new Switch(this) { Checked = item.Enabled, ContentDescription = $"Enable {item.Title}" };
        toggle.CheckedChange += (_, args) =>
        {
            if (args.IsChecked && !ScheduleManager.IsBackgroundReady(this))
            {
                toggle.Checked = false; Toast.MakeText(this, "Complete background setup before enabling schedules", ToastLength.Long)?.Show(); FixNextMissingPermission(); return;
            }
            item.Enabled = args.IsChecked; ScheduleManager.Save(this, item); Rebuild();
        };
        top.AddView(toggle); content.AddView(top);
        var next = item.Enabled ? $"Next run: {FormatNextRun(ScheduleManager.GetNextOccurrence(item))}" : "Paused";
        var nextLabel = Text($"◷  {next}", 12, item.Enabled ? Success : OnSurfaceVariant, true); nextLabel.SetPadding(0, Dp(12), 0, 0); content.AddView(nextLabel);
        card.AddView(content); return card;
    }

    string FormatNextRun(DateTime next) => next.Date == DateTime.Today ? $"today at {next:h:mm tt}" : $"tomorrow at {next:h:mm tt}";

    void ShowEditor(DndSchedule? existing)
    {
        var model = existing is null ? new DndSchedule { Hour = DateTime.Now.Hour, Minute = DateTime.Now.Minute, DurationMinutes = 30 } : new DndSchedule
        { Id = existing.Id, Title = existing.Title, Hour = existing.Hour, Minute = existing.Minute, DurationMinutes = existing.DurationMinutes, Enabled = existing.Enabled };
        var dialog = new BottomSheetDialog(this); var form = Column(); form.SetPadding(Dp(20), Dp(12), Dp(20), Dp(24));
        var handle = new Space(this); handle.SetBackgroundColor(Color.Rgb(121, 116, 126));
        var handleParams = new LinearLayout.LayoutParams(Dp(34), Dp(4)) { Gravity = GravityFlags.CenterHorizontal, BottomMargin = Dp(16) }; form.AddView(handle, handleParams);
        form.AddView(Text(existing is null ? "New schedule" : "Edit schedule", 25, OnSurface, true));

        var titleLayout = new TextInputLayout(this) { Hint = "Title" }; var title = new TextInputEditText(titleLayout.Context) { Text = model.Title == "Quiet time" ? "" : model.Title };
        title.SetSingleLine(true); titleLayout.AddView(title); form.AddView(titleLayout, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(18) });
        form.AddView(Text("Start time", 13, OnSurfaceVariant, true), new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(16) });
        var picker = new TimePicker(this); picker.SetIs24HourView(Java.Lang.Boolean.False); picker.Hour = model.Hour; picker.Minute = model.Minute; form.AddView(picker);

        var durationRow = Row(); var durationText = Column(); durationText.AddView(Text("Duration", 17, OnSurface, true)); durationText.AddView(Text("How long DND stays active", 12, OnSurfaceVariant)); durationRow.AddView(durationText, new LinearLayout.LayoutParams(0, -2, 1));
        var durationValue = Text($"{model.DurationMinutes} min", 20, Primary, true); durationRow.AddView(durationValue); form.AddView(durationRow, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(16) });
        var duration = new SeekBar(this) { Max = 119, Progress = model.DurationMinutes - 1 }; duration.ProgressTintList = ColorStateList.ValueOf(Primary); duration.ThumbTintList = ColorStateList.ValueOf(Primary);
        duration.ProgressChanged += (_, args) => durationValue.Text = $"{args.Progress + 1} min"; form.AddView(duration, new LinearLayout.LayoutParams(-1, Dp(52)));
        var endpoints = Row(); endpoints.AddView(Text("1 min", 11, OnSurfaceVariant), new LinearLayout.LayoutParams(0, -2, 1)); var max = Text("120 min", 11, OnSurfaceVariant); max.Gravity = GravityFlags.Right; endpoints.AddView(max, new LinearLayout.LayoutParams(0, -2, 1)); form.AddView(endpoints);
        var repeat = Row(); var repeatText = Column(); repeatText.AddView(Text("Repeat", 16, OnSurface, true)); repeatText.AddView(Text("Runs automatically", 12, OnSurfaceVariant)); repeat.AddView(repeatText, new LinearLayout.LayoutParams(0, -2, 1)); repeat.AddView(Text("Every day", 14, OnSurface, true));
        form.AddView(repeat, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(18), BottomMargin = Dp(8) });

        var save = FilledButton("Save schedule"); save.Click += (_, _) =>
        {
            model.Title = string.IsNullOrWhiteSpace(title.Text) ? "Quiet time" : title.Text.Trim(); model.Hour = picker.Hour; model.Minute = picker.Minute; model.DurationMinutes = duration.Progress + 1;
            if (existing is null) model.Enabled = ScheduleManager.IsBackgroundReady(this);
            ScheduleManager.Save(this, model); dialog.Dismiss(); RefreshReadiness(); Rebuild();
            if (!ScheduleManager.IsBackgroundReady(this)) { Toast.MakeText(this, "Schedule saved paused. Complete background setup to enable it.", ToastLength.Long)?.Show(); FixNextMissingPermission(); }
        };
        form.AddView(save, new LinearLayout.LayoutParams(-1, Dp(58)) { TopMargin = Dp(16) });
        if (existing is not null)
        {
            var delete = new MaterialButton(this) { Text = "Delete schedule" }; delete.SetTextColor(Color.Rgb(179, 38, 30)); delete.BackgroundTintList = ColorStateList.ValueOf(Color.Transparent);
            delete.Click += (_, _) => { ScheduleManager.Delete(this, existing.Id); dialog.Dismiss(); Rebuild(); }; form.AddView(delete, new LinearLayout.LayoutParams(-1, Dp(50)));
        }
        var scroll = new ScrollView(this); scroll.AddView(form); dialog.SetContentView(scroll); dialog.Show();
    }

    LinearLayout Row() { var v = new LinearLayout(this) { Orientation = Orientation.Horizontal }; v.SetGravity(GravityFlags.CenterVertical); return v; }
    LinearLayout Column() => new(this) { Orientation = Orientation.Vertical };
    TextView Text(string value, float size, Color color, bool bold = false) { var v = new TextView(this) { Text = value, TextSize = size }; v.SetTextColor(color); if (bold) v.SetTypeface(null, TypefaceStyle.Bold); return v; }
    MaterialCardView Card(Color color) { var card = new MaterialCardView(this) { Radius = Dp(20), CardElevation = 0 }; card.SetCardBackgroundColor(color); return card; }
    MaterialButton FilledButton(string text) { var button = new MaterialButton(this) { Text = text, CornerRadius = Dp(18), InsetTop = 0, InsetBottom = 0 }; button.SetTextColor(Color.White); button.BackgroundTintList = ColorStateList.ValueOf(Primary); return button; }
    MaterialButton IconButton(string text) { var button = new MaterialButton(this) { Text = text, CornerRadius = Dp(24), InsetTop = 0, InsetBottom = 0 }; button.SetMinWidth(0); button.SetTextColor(OnSurface); button.BackgroundTintList = ColorStateList.ValueOf(Color.Transparent); return button; }
}
