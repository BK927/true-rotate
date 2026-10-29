namespace TrueRotate;

/// <summary>
/// Local, size-capped diagnostic log at %LocalAppData%\TrueRotate\logs\truerotate.log.
/// Never transmitted anywhere — the user copies it out via Settings → "Copy diagnostics".
/// Every call is best-effort: logging must never throw or crash the tray.
/// </summary>
internal static class Log
{
    private const long MaxBytes = 512 * 1024;   // roll over to truerotate.log.1 past this

    public static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TrueRotate", "logs");

    private static readonly string FilePath   = Path.Combine(Dir, "truerotate.log");
    private static readonly string BackupPath = FilePath + ".1";
    private static readonly object Gate       = new();

    public static void Info (string message)                 => Write("INFO ", message);
    public static void Warn (string message)                 => Write("WARN ", message);
    public static void Error(string message, Exception? ex = null)
        => Write("ERROR", ex is null ? message : $"{message}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");

    private static void Write(string level, string message)
    {
        try
        {
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
            lock (Gate)
            {
                Directory.CreateDirectory(Dir);
                var fi = new FileInfo(FilePath);
                if (fi.Exists && fi.Length > MaxBytes)
                    File.Move(FilePath, BackupPath, overwrite: true);
                File.AppendAllText(FilePath, line);
            }
        }
        catch { /* best-effort */ }
    }

    /// <summary>Returns the last <paramref name="count"/> lines (spanning the backup file if needed).</summary>
    public static IReadOnlyList<string> Tail(int count)
    {
        try
        {
            lock (Gate)
            {
                var lines = new List<string>();
                if (File.Exists(BackupPath)) lines.AddRange(File.ReadAllLines(BackupPath));
                if (File.Exists(FilePath))   lines.AddRange(File.ReadAllLines(FilePath));
                return lines.Count <= count ? lines : lines[^count..];
            }
        }
        catch (Exception ex)
        {
            return [$"(log read failed: {ex.Message})"];
        }
    }
}
