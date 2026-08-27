using System.Runtime.InteropServices;

namespace XBVault.Services;

public static class PlatformDialog
{
    public static bool Confirm(string title, string message)
    {
        if (OperatingSystem.IsWindows())
            return Win32MessageBox(title, message, 0x00000004 | 0x00000020) == 6; // MB_YESNO | MB_ICONQUESTION

        if (OperatingSystem.IsMacOS())
            return OsxDialog(title, message);

        return LinuxDialog(title, message);
    }

    public static void Alert(string title, string message)
    {
        if (OperatingSystem.IsWindows())
        {
            Win32MessageBox(title, message, 0x00000000 | 0x00000040); // MB_OK | MB_ICONINFORMATION
            return;
        }

        if (OperatingSystem.IsMacOS())
            OsxAlert(title, message);
        else
            LinuxAlert(title, message);
    }

    private static bool OsxDialog(string title, string message)
    {
        try
        {
            var escaped = message.Replace("\"", "\\\"");
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "osascript",
                Arguments = $"-e \"display dialog \\\"{escaped}\\\" with title \\\"{title}\\\" buttons {{\\\"No\\\", \\\"Yes\\\"}} default button \\\"No\\\" cancel button \\\"No\\\"\"",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            var proc = System.Diagnostics.Process.Start(psi);
            if (proc is null) return false;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            return proc.ExitCode == 0 && output.Contains("button returned:Yes");
        }
        catch
        {
            return false;
        }
    }

    private static void OsxAlert(string title, string message)
    {
        try
        {
            var escaped = message.Replace("\"", "\\\"");
            System.Diagnostics.Process.Start("osascript",
                $"-e \"display dialog \\\"{escaped}\\\" with title \\\"{title}\\\" buttons {{\\\"OK\\\"}} default button \\\"OK\\\"\"");
        }
        catch { /* fallback — osascript unavailable, alert silently skipped */ }
    }

    private static bool LinuxDialog(string title, string message)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "zenity",
                Arguments = $"--question --title=\"{title}\" --text=\"{message}\" --width=400",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            var proc = System.Diagnostics.Process.Start(psi);
            if (proc is null) return false;
            proc.WaitForExit();
            return proc.ExitCode == 0;
        }
        catch
        {
            try
            {
                // Fallback: xmessage
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "xmessage",
                    Arguments = $"-buttons \"No:1,Yes:0\" -center \"{message}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                };
                var proc = System.Diagnostics.Process.Start(psi);
                if (proc is null) return false;
                proc.WaitForExit();
                return proc.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }

    private static void LinuxAlert(string title, string message)
    {
        try
        {
            System.Diagnostics.Process.Start("zenity",
                $"--info --title=\"{title}\" --text=\"{message}\" --width=400");
        }
        catch
        {
            try { System.Diagnostics.Process.Start("xmessage", $"-center \"{message}\""); }
            catch { /* fallback — xmessage unavailable, alert silently skipped */ }
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, BestFitMapping = false)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    private static int Win32MessageBox(string title, string message, uint type)
    {
        try
        {
            return MessageBox(IntPtr.Zero, message, title, type);
        }
        catch
        {
            return 0;
        }
    }
}
