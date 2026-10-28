
using AlarmCore.Models;
using AlarmCore.Services;
using Microsoft.UI.Dispatching;
using Windows.Media.Core;
using Windows.Media.Playback;
using System.Collections.Concurrent;

using Microsoft.Maui.Storage;

namespace Chudisoft_Interval_Timer;

public sealed class WindowsAlarmScheduler : IAlarmScheduler, IDisposable
{
    private readonly ConcurrentDictionary<Guid, Timer> _timers = new();
    private readonly ConcurrentDictionary<Guid, AlarmModel> _alarms = new();
    private readonly DispatcherQueue _dispatcher;
    private readonly MediaPlayer _player = new();

    public event Action<AlarmModel>? AlarmTriggered;

    public WindowsAlarmScheduler()
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException(
                "Create WindowsAlarmScheduler on the UI thread.");

        _player.IsLoopingEnabled = true;
    }

    public void ScheduleAlarm(AlarmModel alarm)
    {
        CancelAlarm(alarm.Id);

        if (!alarm.Enabled)
            return;

        var next = AlarmOccurrenceCalculator.GetNextOccurrence(
            alarm, DateTime.UtcNow);

        if (!next.HasValue)
            return;

        _alarms[alarm.Id] = alarm;

        var delay = next.Value.ToUniversalTime() - DateTime.UtcNow;
        if (delay < TimeSpan.Zero)
            delay = TimeSpan.Zero;

        var timer = new Timer(
            _ => OnAlarmElapsed(alarm.Id),
            null,
            delay,
            Timeout.InfiniteTimeSpan);

        _timers[alarm.Id] = timer;
    }

    private void OnAlarmElapsed(Guid alarmId)
    {
        if (!_alarms.TryGetValue(alarmId, out var alarm))
            return;

        _dispatcher.TryEnqueue(async () =>
        {
            // The alarm may have been cancelled before dispatch.
            if (!_alarms.ContainsKey(alarmId))
                return;

            await StartAlarmSoundAsync();
            AlarmTriggered?.Invoke(alarm);
        });
    }


    private async Task StartAlarmSoundAsync()
    {
        try
        {
            const string fileName = "alarm_tone.wav";

            // Read the bundled MAUI asset.
            await using var source =
                await FileSystem.OpenAppPackageFileAsync(fileName);

            // Copy it to a physical file for Windows MediaPlayer.
            var cachePath = Path.Combine(
                FileSystem.CacheDirectory, fileName);

            await using (var destination = File.Create(cachePath))
            {
                await source.CopyToAsync(destination);
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _player.Pause();
                _player.Source = MediaSource.CreateFromUri(
                    new Uri(cachePath));

                _player.IsLoopingEnabled = true;
                _player.Volume = 1.0;
                _player.Play();
            });

            System.Diagnostics.Debug.WriteLine(
                $"Playing alarm from: {cachePath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"FAILED TO PLAY ALARM: {ex}");
        }
    }


    private void StartAlarmSound2()
    {
        try
        {
            _player.MediaFailed -= Player_MediaFailed;
            _player.MediaFailed += Player_MediaFailed;

            _player.MediaEnded -= Player_MediaEnded;
            _player.MediaEnded += Player_MediaEnded;

            _player.IsLoopingEnabled = true;
            _player.Volume = 1.0;

            var uri = new Uri("ms-appx:///alarm_tone.wav");

            _player.Source = MediaSource.CreateFromUri(uri);
            _player.Play();

            System.Diagnostics.Debug.WriteLine(
                $"Alarm playback requested: {uri}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Alarm playback error: {ex}");
        }
    }

    private void Player_MediaFailed(
        Windows.Media.Playback.MediaPlayer sender,
        Windows.Media.Playback.MediaPlayerFailedEventArgs args)
    {
        System.Diagnostics.Debug.WriteLine(
            $"Alarm media failed: {args.Error} - {args.ErrorMessage}");
    }

    private void Player_MediaEnded(
        Windows.Media.Playback.MediaPlayer sender,
        object args)
    {
        System.Diagnostics.Debug.WriteLine(
            "Alarm audio ended.");
    }


    private void StartAlarmSound1()
    {
        try
        {
            _player.Source = MediaSource.CreateFromUri(
                new Uri("ms-appx:///Assets/alarm_tone.wav"));

            _player.Play();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Could not play alarm sound: {ex.Message}");
        }
    }

    public void StopAlarmSound()
    {
        _player.Pause();
        _player.Source = null;
    }

    public void DismissAlarm(Guid alarmId)
    {
        StopAlarmSound();
        CancelAlarm(alarmId);
    }

    public void CancelAlarm(Guid alarmId)
    {
        if (_timers.TryRemove(alarmId, out var timer))
        {
            timer.Change(Timeout.Infinite, Timeout.Infinite);
            timer.Dispose();
        }

        _alarms.TryRemove(alarmId, out _);
    }

    public void CancelAll()
    {
        foreach (var alarmId in _timers.Keys)
            CancelAlarm(alarmId);

        StopAlarmSound();
    }

    public void RestoreScheduledAlarms()
    {
        // Load saved AlarmModel instances from your persistence layer
        // and call ScheduleAlarm(alarm) for each one.
    }

    public void Dispose()
    {
        CancelAll();
        _player.Dispose();
    }
}
