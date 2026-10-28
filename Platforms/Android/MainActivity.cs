using Android.App;
using Android.Content.PM;
using Android.OS;

namespace Chudisoft_Interval_Timer
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            AndroidPermissionHelper.EnsureRequiredPermissions(this);
            if (!AndroidPermissionHelper.HasRequiredPermissions(this))
            {
                AndroidPermissionHelper.ShowPermissionGuideIfNeeded(this);
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            if (requestCode == AndroidPermissionHelper.AlarmPermissionRequestCode)
            {
                if (!AndroidPermissionHelper.HasRequiredPermissions(this))
                {
                    AndroidPermissionHelper.ShowPermissionGuideIfNeeded(this);
                }
            }
        }
    }
}
