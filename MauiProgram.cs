using AlarmCore.Services;
using Microsoft.Extensions.Logging;

namespace Chudisoft_Interval_Timer;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "alarms.json");
        builder.Services.AddSingleton<IAlarmRepository>(_ => new AlarmRepository(dbPath));
        //builder.Services.AddSingleton<AlarmSchedulerService>(_ => new AlarmSchedulerService(new NoopAlarmScheduler()));

#if ANDROID
        builder.Services.AddSingleton<IAlarmScheduler, AndroidAlarmScheduler>();
#elif WINDOWS
builder.Services.AddSingleton<IAlarmScheduler, WindowsAlarmScheduler>();
#endif

        builder.Services.AddSingleton<AlarmSchedulerService>(
            sp => new AlarmSchedulerService(
                sp.GetRequiredService<IAlarmScheduler>()));

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}

public class NoopAlarmScheduler : IAlarmScheduler
{
    public void ScheduleAlarm(AlarmCore.Models.AlarmModel alarm) { }
    public void CancelAlarm(Guid alarmId) { }
    public void CancelAll() { }
    public void RestoreScheduledAlarms() { }
}
