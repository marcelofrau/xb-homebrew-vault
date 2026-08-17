using System;
using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;

namespace XBVault.Android;

[Application]
public class AndroidApp : AvaloniaAndroidApplication<App>
{
    public AndroidApp(IntPtr handle, JniHandleOwnership transfer)
        : base(handle, transfer)
    {
    }
}
