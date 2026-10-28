using AlarmCore.Models;
using AlarmCore.Services;

namespace Chudisoft_Interval_Timer;

public partial class MainPage : ContentPage
{
    private readonly IAlarmScheduler _alarmScheduler;

    public MainPage(IAlarmScheduler alarmScheduler)
    {
        InitializeComponent();

        _alarmScheduler = alarmScheduler;

#if WINDOWS
        if (_alarmScheduler is WindowsAlarmScheduler windowsScheduler)
        {
            windowsScheduler.AlarmTriggered += OnAlarmTriggered;
        }
#endif
    }

#if WINDOWS
    private async void OnAlarmTriggered(AlarmModel alarm)
    {
        var result = await MainThread.InvokeOnMainThreadAsync(() =>
            DisplayAlert(
                alarm.Name,
                "Your alarm is ringing.",
                "Dismiss",
                "Keep ringing"));

        if (result && _alarmScheduler is WindowsAlarmScheduler scheduler)
        {
            scheduler.DismissAlarm(alarm.Id);
        }
    }
#endif
}