using System.Diagnostics;
using System.Text;
using Microsoft.Win32;

namespace TrueRotate;

/// <summary>
/// Builds a plain-text diagnostic report the user can paste into a bug report:
/// app/OS/GPU versions, per-monitor actual vs. desired rotation, settings, and the
/// tail of the local log. Nothing here is sent anywhere by the app.
/// </summary>
internal static class Diagnostics
{
    private const int LogLines = 80;

    // Display adapters device class; each 000N subkey holds DriverDesc / DriverVersion.
    private const string DisplayClassKey =
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

    public static string AppVersion()
    {
        try
        {
            var v = Windows.ApplicationModel.Package.Current.Id.Version;
            return $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision} (Store)";
        }
        catch
        {
            return $"{typeof(Diagnostics).Assembly.GetName().Version} (unpackaged)";
        }
    }

    public static string BuildReport(OrientationStore store)
    {
        var sb = new StringBuilder();
        sb.AppendLine("TrueRotate diagnostics");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"App:       {AppVersion()}");
        sb.AppendLine($"OS:        {Environment.OSVersion.VersionString} ({(Environment.Is64BitOperatingSystem ? "x64" : "x86")})");
        sb.AppendLine($"Runtime:   .NET {Environment.Version}");
        sb.AppendLine($"NVIDIA app running: {(IsRunning("NVIDIA app") ? "yes" : "no")}");

        sb.AppendLine();
        sb.AppendLine("--- GPUs ---");
        foreach (var gpu in Gpus()) sb.AppendLine(gpu);

        sb.AppendLine();
        sb.AppendLine("--- Monitors (actual / desired) ---");
        try
        {
            var desired = store.All();
            foreach (var m in DisplayService.EnumerateMonitors())
            {
                string want = desired.TryGetValue(m.DevicePath, out uint d) ? $"{d}°" : "-";
                sb.AppendLine($"[{m.Index}] {m.FriendlyName}  {m.Rotation}° / {want}  {m.GdiDeviceName}  {m.DevicePath}");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"(enumeration failed: {ex.Message})");
        }

        sb.AppendLine();
        sb.AppendLine("--- Settings ---");
        sb.AppendLine($"AutoReapply={store.AutoReapply}  HotkeyTarget={store.HotkeyTarget}  Autostart={store.Autostart}  Language={store.Language}");

        sb.AppendLine();
        sb.AppendLine($"--- Log (last {LogLines} lines) ---");
        foreach (var line in Log.Tail(LogLines)) sb.AppendLine(line);

        return sb.ToString();
    }

    private static IEnumerable<string> Gpus()
    {
        var result = new List<string>();
        try
        {
            using var cls = Registry.LocalMachine.OpenSubKey(DisplayClassKey);
            if (cls is not null)
            {
                foreach (string name in cls.GetSubKeyNames())
                {
                    if (name.Length != 4 || !name.All(char.IsDigit)) continue;   // skip "Properties" etc.
                    try
                    {
                        using var k = cls.OpenSubKey(name);
                        if (k?.GetValue("DriverDesc") is string desc)
                            result.Add($"{desc}  driver {k.GetValue("DriverVersion") as string ?? "?"}");
                    }
                    catch { /* individual subkey unreadable — skip */ }
                }
            }
        }
        catch (Exception ex)
        {
            result.Add($"(GPU query failed: {ex.Message})");
        }
        if (result.Count == 0) result.Add("(none found)");
        return result;
    }

    private static bool IsRunning(string processName)
    {
        try
        {
            var procs = Process.GetProcessesByName(processName);
            foreach (var p in procs) p.Dispose();
            return procs.Length > 0;
        }
        catch { return false; }
    }
}
