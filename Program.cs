using System.Runtime.InteropServices;
using RotatePlus;
using Windows.Win32;

if (args.Length > 0)
{
    // Attach to the parent console so output appears in the launching terminal.
    PInvoke.AttachConsole(unchecked((uint)-1));  // ATTACH_PARENT_PROCESS

    RunCli(args);
    return;
}

// ── Tray mode ────────────────────────────────────────────────────────────────
bool createdNew;
using var mutex = new Mutex(initiallyOwned: true, @"Global\rotate+_singleton", out createdNew);
if (!createdNew)
    return;  // Another instance is already running.

ApplicationConfiguration.Initialize();
Application.Run(new TrayContext());

// ─────────────────────────────────────────────────────────────────────────────

static void RunCli(string[] args)
{
    if (args.Length == 0 || args[0] == "list")
    {
        ListMonitors();
        return;
    }

    if (args[0] == "set" && args.Length == 3
        && int.TryParse(args[1], out int setIdx)
        && uint.TryParse(args[2], out uint setDeg))
    {
        var monitors = DisplayService.EnumerateMonitors();
        if (setIdx < 0 || setIdx >= monitors.Count)
        {
            Console.Error.WriteLine($"Error: index {setIdx} out of range (0–{monitors.Count - 1}).");
            return;
        }
        DisplayService.SetRotation(monitors[setIdx], setDeg);
        Console.WriteLine($"Set monitor {setIdx} ({monitors[setIdx].FriendlyName}) to {setDeg}°.");
        return;
    }

    if (args[0] == "test" && args.Length == 2 && int.TryParse(args[1], out int testIdx))
    {
        var monitors = DisplayService.EnumerateMonitors();
        if (testIdx < 0 || testIdx >= monitors.Count)
        {
            Console.Error.WriteLine($"Error: index {testIdx} out of range (0–{monitors.Count - 1}).");
            return;
        }

        var mon = monitors[testIdx];
        uint originalRotation = DisplayService.GetRotation(mon);
        Console.WriteLine($"Testing monitor {testIdx}: {mon.FriendlyName}");
        Console.WriteLine($"Original rotation: {originalRotation}°");
        Console.WriteLine();

        uint[] targets = [90, 180, 270, 0];
        int pass = 0, fail = 0;

        foreach (uint target in targets)
        {
            DisplayService.SetRotation(mon, target);
            uint actual = DisplayService.GetRotation(mon);
            bool ok = actual == target;
            Console.WriteLine($"  {target,3}° → read back {actual,3}° : {(ok ? "PASS" : "FAIL")}");
            if (ok) pass++; else fail++;
        }

        DisplayService.SetRotation(mon, originalRotation);
        Console.WriteLine();
        Console.WriteLine($"Result: {pass} PASS, {fail} FAIL. Restored to {originalRotation}°.");
        return;
    }

    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  rotatePlus list");
    Console.Error.WriteLine("  rotatePlus set <index> <0|90|180|270>");
    Console.Error.WriteLine("  rotatePlus test <index>");
}

static void ListMonitors()
{
    var monitors = DisplayService.EnumerateMonitors();
    Console.WriteLine($"{"IDX",-4} {"FRIENDLY NAME",-32} {"ROT",5}  {"GDI",-12}  DEVICE PATH");
    Console.WriteLine(new string('-', 110));
    foreach (var m in monitors)
    {
        string path = m.DevicePath.Length > 50
            ? "…" + m.DevicePath[^49..]
            : m.DevicePath;
        Console.WriteLine($"{m.Index,-4} {m.FriendlyName,-32} {m.Rotation,3}°  {m.GdiDeviceName,-12}  {path}");
    }
}
