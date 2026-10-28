using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using AndroidX.Core.App;

namespace Chudisoft_Interval_Timer;

public static class AndroidPermissionHelper
{
    public const int AlarmPermissionRequestCode = 1011;

    public static readonly string[] RequiredPermissions =
    {
        Manifest.Permission.PostNotifications,
        Manifest.Permission.Vibrate,
        Manifest.Permission.ReceiveBootCompleted,
        Manifest.Permission.ScheduleExactAlarm,
        Manifest.Permission.WakeLock,
        Manifest.Permission.ForegroundService
    };

    public static bool HasRequiredPermissions(Activity activity)
    {
        foreach (var permission in RequiredPermissions)
        {
            if (ActivityCompat.CheckSelfPermission(activity, permission) != Permission.Granted)
            {
                return false;
            }
        }

        return true;
    }

    public static void EnsureRequiredPermissions(Activity activity)
    {
        var missing = RequiredPermissions
            .Where(permission => ActivityCompat.CheckSelfPermission(activity, permission) != Permission.Granted)
            .ToArray();

        if (missing.Length == 0)
        {
            return;
        }

        ActivityCompat.RequestPermissions(activity, missing, AlarmPermissionRequestCode);
    }

    public static void ShowPermissionGuideIfNeeded(Activity activity)
    {
        if (HasRequiredPermissions(activity))
        {
            return;
        }

        var builder = new AlertDialog.Builder(activity);
        builder.SetTitle("Permission required")
            .SetMessage("Exact alarm scheduling and notifications need your permission to work while the app is in the background. Please enable them in Settings.")
            .SetPositiveButton("Open Settings", (sender, args) =>
            {
                var intent = new Intent(Android.Provider.Settings.ActionApplicationDetailsSettings);
                var uri = Android.Net.Uri.FromParts("package", activity.PackageName, null);
                intent.SetData(uri);
                activity.StartActivity(intent);
            })
            .SetNegativeButton("Later", (sender, args) => { })
            .Show();
    }
}
