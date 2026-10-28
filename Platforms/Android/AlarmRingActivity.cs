
using Android.App;
using Android.OS;
using Android.Widget;

namespace Chudisoft_Interval_Timer;

[Activity(
    Theme = "@style/Maui.MainTheme",
    Exported = false,
    ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
public class AlarmRingActivity : Activity
{
    private string _alarmName = "Alarm";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        _alarmName = Intent?.GetStringExtra("AlarmName") ?? "Alarm";

        SetContentView(Resource.Layout.alarm_ring_activity);

        var dismissButton =
            FindViewById<Android.Widget.Button>(Resource.Id.dismissAlarmButton);

        var titleText =
            FindViewById<TextView>(Resource.Id.alarmTitleText);

        if (titleText is not null)
            titleText.Text = _alarmName;

        if (dismissButton is not null)
        {
            dismissButton.Click += (_, _) =>
            {
                var serviceIntent =
                    new Android.Content.Intent(
                        this, typeof(AlarmPlaybackService));

                StopService(serviceIntent);
                Finish();
            };
        }
    }
}
