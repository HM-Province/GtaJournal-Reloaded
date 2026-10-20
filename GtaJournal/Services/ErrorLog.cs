using System.Runtime.InteropServices;

namespace GtaJournal.Services;

/// <summary>Журнал ошибок в %LOCALAPPDATA%\GtaJournal\error.log.</summary>
public static class ErrorLog
{
    private const long MaxSize = 1024 * 1024;

    public static string FilePath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GtaJournal", "error.log");

    public static void Write(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            if (File.Exists(FilePath) && new FileInfo(FilePath).Length > MaxSize)
                File.Delete(FilePath);
            File.AppendAllText(FilePath, $"[{DateTimeOffset.Now:O}] {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public static void ShowFatal(Exception exception) =>
        MessageBoxW(IntPtr.Zero, $"Не удалось запустить GTA Journal.\n\n{exception.Message}", "GTA Journal", 0x10 /* MB_ICONERROR */);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hwnd, string text, string caption, uint type);
}
