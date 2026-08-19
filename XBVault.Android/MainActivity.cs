using System;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Util;
using Android.Views;
using Avalonia.Android;
using XBVault.Services;

namespace XBVault.Android;

[Activity(
    Label = "XBVault",
    Theme = "@style/MainTheme",
    MainLauncher = true,
    ScreenOrientation = ScreenOrientation.Portrait,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
    private const string TAG = "XBVault";

    public MainActivity()
    {
        Logger.OnLog += entry =>
        {
            var message = $"[{entry.Timestamp:HH:mm:ss.fff}] [{entry.Level}] {entry.Message}";
            switch (entry.Level)
            {
                case LogLevel.Trace:
                case LogLevel.Debug:
                    Log.Debug(TAG, message);
                    break;
                case LogLevel.Info:
                    Log.Info(TAG, message);
                    break;
                case LogLevel.Warn:
                    Log.Warn(TAG, message);
                    break;
                case LogLevel.Error:
                case LogLevel.Fatal:
                    Log.Error(TAG, message);
                    break;
            }
        };

        PlatformHelper.OpenUrlAction = url =>
        {
            try
            {
                var intent = new Intent(Intent.ActionView, global::Android.Net.Uri.Parse(url));
                intent.AddFlags(ActivityFlags.NewTask);
                ApplicationContext?.StartActivity(intent);
            }
            catch (Exception ex)
            {
                Log.Error(TAG, $"Failed to open URL: {url} — {ex.Message}");
            }
        };

        // Suppress Avalonia TopLevelImpl NRE when Activity is paused/recreated
        // This is an Avalonia 12 Android issue: the CompositingRenderer fires after
        // the native surface is destroyed during minimize/lifecycle transitions
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex && IsTopLevelNre(ex))
            {
                Log.Warn(TAG, "Suppressed TopLevelImpl NRE from AppDomain.UnhandledException");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            if (IsTopLevelNre(e.Exception))
            {
                Log.Warn(TAG, "Suppressed TopLevelImpl NRE from UnobservedTaskException");
                e.SetObserved();
            }
        };
    }

    private static bool IsTopLevelNre(Exception ex)
    {
        return ex is NullReferenceException &&
               ex.StackTrace?.Contains("TopLevelImpl") == true;
    }

    protected override void OnResume()
    {
        base.OnResume();
        Log.Info(TAG, $"OnResume: Content type={Content?.GetType().FullName ?? "null"}");

        try
        {
            if (Content is AvaloniaView avaloniaView)
            {
                Log.Info(TAG, $"OnResume: AvaloniaView found, Width={avaloniaView.Width}, Height={avaloniaView.Height}, IsAttached={avaloniaView.IsAttachedToWindow}");

                // Phase 1: immediate cancel — reset any pending pointer/gesture state
                var now = Java.Lang.JavaSystem.CurrentTimeMillis();
                var cancel = MotionEvent.Obtain(now, now, MotionEventActions.Cancel, 0, 0, 0);
                var dispatched = avaloniaView.DispatchTouchEvent(cancel);
                cancel.Recycle();
                Log.Info(TAG, $"OnResume: DispatchTouchEvent(Cancel) result={dispatched}");

                // Phase 2: delayed invalidation — surface may not be fully ready immediately
                avaloniaView.PostDelayed(() =>
                {
                    Log.Info(TAG, $"OnResume[+300ms]: requesting layout + invalidate, W={avaloniaView.Width}, H={avaloniaView.Height}");
                    avaloniaView.RequestLayout();
                    avaloniaView.Invalidate();

                    // Phase 3: second cancel after layout pass — belt and suspenders
                    avaloniaView.PostDelayed(() =>
                    {
                        var now2 = Java.Lang.JavaSystem.CurrentTimeMillis();
                        var cancel2 = MotionEvent.Obtain(now2, now2, MotionEventActions.Cancel, 0, 0, 0);
                        avaloniaView.DispatchTouchEvent(cancel2);
                        cancel2.Recycle();
                        Log.Info(TAG, $"OnResume[+600ms]: second DispatchTouchEvent(Cancel) dispatched");
                    }, 300);
                }, 300);
            }
            else
            {
                Log.Warn(TAG, $"OnResume: Content is not AvaloniaView — type={Content?.GetType().FullName ?? "null"}");
            }
        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"OnResume gesture reset FAILED: {ex}");
        }
    }

#pragma warning disable CA1422
    public override void OnBackPressed()
    {
        if (AndroidBackHandler.OnBack?.Invoke() is true)
            return;
        base.OnBackPressed();
    }
#pragma warning restore CA1422
}
