using System;
using AlarmCore.Models;
using AlarmCore.Services;
using Android.App;
using Android.Content;
using Android.Media;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using AndroidAlarmType = Android.App.AlarmType;
using AndroidApplication = Android.App.Application;

namespace Chudisoft_Interval_Timer;

public class AndroidAlarmScheduler : IAlarmScheduler
{
    private readonly Context _context;
    public event Action<AlarmModel>? AlarmTriggered;

    public AndroidAlarmScheduler()
    {
        _context = Android.App.Application.Context;
    }

    public void ScheduleAlarm(AlarmModel alarm)
    {
        if (!alarm.Enabled)
        {
            CancelAlarm(alarm.Id);
            return;
        }

        var activity = Platform.CurrentActivity;

        if (activity is null ||
            !AndroidPermissionHelper.HasRequiredPermissions(activity))
        {
            return;
        }

        var next = AlarmOccurrenceCalculator.GetNextOccurrence(alarm, DateTime.UtcNow);
        if (!next.HasValue)
        {
            CancelAlarm(alarm.Id);
            return;
        }


        var triggerAt = next.Value;
        var intent = new Intent(_context, typeof(AlarmReceiver));
        intent.SetAction($"com.chudisoft.intervaltimer.ALARM_{alarm.Id}");
        intent.PutExtra("AlarmId", alarm.Id.ToString());
        intent.PutExtra("AlarmName", alarm.Name);

        var pendingIntent = PendingIntent.GetBroadcast(
            _context,
            alarm.Id.GetHashCode(),
            intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var alarmManager = (AlarmManager?)_context.GetSystemService(Context.AlarmService);
        if (alarmManager is null || pendingIntent is null)
        {
            return;
        }
        if (Build.VERSION.SdkInt >= BuildVersionCodes.S &&
            !alarmManager.CanScheduleExactAlarms())
        {
            Android.Util.Log.Warn(
                "AndroidAlarmScheduler",
                "Exact alarm permission has not been granted.");

            return;
        }

        var utcMillis = new DateTimeOffset(triggerAt.ToUniversalTime()).ToUnixTimeMilliseconds();
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
        {
            alarmManager.SetExactAndAllowWhileIdle(AndroidAlarmType.RtcWakeup, utcMillis, pendingIntent);
        }
        else
        {
            alarmManager.SetExact(AndroidAlarmType.RtcWakeup, utcMillis, pendingIntent);
        }
    }

    public void CancelAlarm(Guid alarmId)
    {
        var intent = new Intent(_context, typeof(AlarmReceiver));
        intent.SetAction($"com.chudisoft.intervaltimer.ALARM_{alarmId}");

        var pendingIntent = PendingIntent.GetBroadcast(
            _context,
            alarmId.GetHashCode(),
            intent,
            PendingIntentFlags.CancelCurrent | PendingIntentFlags.Immutable);

        var alarmManager = (AlarmManager?)_context.GetSystemService(Context.AlarmService);
        if (alarmManager is not null)
        {
            alarmManager.Cancel(pendingIntent);
        }
    }

    public void CancelAll() { }

    public void RestoreScheduledAlarms() { }

    public void DismissAlarm(Guid alarmId)
    {
        CancelAlarm(alarmId);
    }
}

[BroadcastReceiver(Enabled = true, Exported = true)]
public class AlarmReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        var alarmId = intent?.GetStringExtra("AlarmId");
        if (string.IsNullOrWhiteSpace(alarmId) || context is null)
        {
            return;
        }

        var ringIntent = new Intent(context, typeof(AlarmRingActivity));
        ringIntent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
        ringIntent.PutExtra("AlarmId", alarmId);
        ringIntent.PutExtra("AlarmName", intent.GetStringExtra("AlarmName") ?? "Alarm");
        context.StartActivity(ringIntent);

        var serviceIntent = new Intent(context, typeof(AlarmPlaybackService));
        serviceIntent.SetAction("com.chudisoft.intervaltimer.PLAY_ALARM");
        serviceIntent.PutExtra("AlarmId", alarmId);
        serviceIntent.PutExtra("AlarmName", intent.GetStringExtra("AlarmName") ?? "Alarm");

        ContextCompat.StartForegroundService(context, serviceIntent);
    }
}

[Service(Exported = false)]
public class AlarmPlaybackService : Service
{
    private MediaPlayer? _mediaPlayer;
    private Vibrator? _vibrator;
    private readonly string _channelId = "alarm_channel_silent_v2";


    public override StartCommandResult OnStartCommand(
        Intent? intent,
        StartCommandFlags flags,
        int startId)
    {
        var name = intent?.GetStringExtra("AlarmName") ?? "Alarm";
        var notificationManager =
            (NotificationManager?)GetSystemService(NotificationService);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O &&
            notificationManager is not null)
        {
            var channel = new NotificationChannel(
                _channelId,
                "Alarm notifications",
                NotificationImportance.Low)
            {
                Description = "Alarm playback controls"
            };

            // Prevent Android's default notification sound.
            channel.SetSound(null, null);
            channel.EnableVibration(false);

            notificationManager.CreateNotificationChannel(channel);
        }

        var dismissIntent = new Intent(
            this,
            typeof(AlarmDismissReceiver));

        dismissIntent.SetAction(
            "com.chudisoft.intervaltimer.DISMISS_ALARM");

        dismissIntent.PutExtra("AlarmName", name);

        var dismissPendingIntent = PendingIntent.GetBroadcast(
            this,
            0,
            dismissIntent,
            PendingIntentFlags.UpdateCurrent |
            PendingIntentFlags.Immutable);

        var builder = new NotificationCompat.Builder(this, _channelId)
            .SetSmallIcon(Resource.Drawable.abc_star_black_48dp)
            .SetContentTitle("Alarm")
            .SetContentText(name)
            .SetAutoCancel(false)
            .SetPriority(NotificationCompat.PriorityLow)
            .SetCategory(NotificationCompat.CategoryAlarm)
            .SetOngoing(true)
            .SetSilent(true)
            .AddAction(
                new NotificationCompat.Action(
                    Resource.Drawable.abc_ic_clear_material,
                    "Dismiss",
                    dismissPendingIntent));

        var notification = builder.Build();

        StartForeground(1, notification);

        // Your custom WAV playback remains here.
        StartAlarmSound();

        return StartCommandResult.NotSticky;
    }


    private string? _alarmFilePath;

    private void StartAlarmSound()
    {
        try
        {
            _mediaPlayer?.Release();
            _mediaPlayer = null;

            // Copy the packaged MAUI asset to a real file.
            _alarmFilePath = Path.Combine(
                CacheDir!.AbsolutePath,
                "alarm_tone.wav");

            using (var source = FileSystem.OpenAppPackageFileAsync(
                "alarm_tone.wav").GetAwaiter().GetResult())
            using (var destination = File.Create(_alarmFilePath))
            {
                source.CopyTo(destination);
            }

            _mediaPlayer = new MediaPlayer();

            _mediaPlayer.SetAudioAttributes(
                new AudioAttributes.Builder()
                    .SetUsage(AudioUsageKind.Alarm)
                    .SetContentType(AudioContentType.Music)
                    .Build());

            _mediaPlayer.SetDataSource(_alarmFilePath);
            _mediaPlayer.Looping = true;
            _mediaPlayer.SetVolume(1.0f, 1.0f);

            _mediaPlayer.Prepare();
            _mediaPlayer.Start();

            Android.Util.Log.Info(
                "AlarmPlaybackService",
                $"Custom WAV started. IsPlaying={_mediaPlayer.IsPlaying}");
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error(
                "AlarmPlaybackService",
                $"Custom WAV playback failed: {ex}");
        }
    }



    private sealed class AlarmMediaPlayerErrorListener
        : Java.Lang.Object, MediaPlayer.IOnErrorListener
    {
        public bool OnError(
            MediaPlayer? player,
            MediaError what,
            int extra)
        {
            Android.Util.Log.Error(
                "AlarmPlaybackService",
                $"MediaPlayer error: what={what}, extra={extra}");

            return false;
        }
    }




    public override void OnDestroy()
    {
        base.OnDestroy();
        _mediaPlayer?.Stop();
        _mediaPlayer?.Release();
        _mediaPlayer = null;
        _vibrator?.Cancel();
    }

    public override IBinder? OnBind(Intent? intent) => null;
}

[BroadcastReceiver(Enabled = true, Exported = true)]
public class AlarmDismissReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null)
        {
            return;
        }

        var serviceIntent = new Intent(context, typeof(AlarmPlaybackService));
        context.StopService(serviceIntent);
    }
}
