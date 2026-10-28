
namespace Chudisoft_Interval_Timer
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(
                new MainPage(
                    Application.Current!.Handler.MauiContext!.Services
                        .GetRequiredService<AlarmCore.Services.IAlarmScheduler>()
                ))
            {
                Title = "Chudisoft Interval Timer"
            };
        }
    }
}
