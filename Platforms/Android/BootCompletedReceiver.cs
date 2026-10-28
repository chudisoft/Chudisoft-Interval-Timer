using AlarmCore.Services;
using Android.App;
using Android.Content;

namespace Chudisoft_Interval_Timer;

[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter(new[] { Intent.ActionBootCompleted, "android.intent.action.QUICKBOOT_POWERON" })]
public class BootCompletedReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null)
        {
            return;
        }

        var dbPath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal), "alarms.db3");
        var repository = new AlarmRepository(dbPath);
        var newScheduler = new AndroidAlarmScheduler();
        var pendingAlarms = repository.GetAllAsync().GetAwaiter().GetResult().Where(a => a.Enabled).ToList();
        foreach (var alarm in pendingAlarms)
        {
            newScheduler.ScheduleAlarm(alarm);
        }
    }
}
