using Android.App;
using Android.Content.PM;
using Android.OS;

namespace SnowballTests.Mobile.Maui;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);

		AppState.SystemLog = message => Android.Util.Log.Info("SNOWBALL", message);
		AppState.ExitProcess = code =>
		{
			Android.Util.Log.Info("SNOWBALL", $"EXIT {code}");
			Java.Lang.JavaSystem.Exit(code);
		};

		try
		{
			AppState.LogDirectory = GetExternalFilesDir(null)?.AbsolutePath;
		}
		catch
		{
			// fall back to the default app data directory
		}

		if (string.Equals(Intent?.GetStringExtra("SNOWBALL_CI"), "1", StringComparison.Ordinal)
			|| string.Equals(System.Environment.GetEnvironmentVariable("SNOWBALL_CI"), "1", StringComparison.Ordinal))
		{
			AppState.CiMode = true;
		}
	}
}
