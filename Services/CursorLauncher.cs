using System.ComponentModel;
using System.Diagnostics;

namespace OpenCmd.Services;

static class CursorLauncher
{
    public static void Open(string folder)
    {
        if (!Directory.Exists(folder))
            throw new InvalidOperationException("Esa carpeta ya no existe.");

        try
        {
            if (!TryStartDirect(folder) && !TryStartHiddenCmd(folder))
                throw new FileNotFoundException("cursor.cmd");
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            throw new InvalidOperationException(
                "No se pudo abrir Cursor. Comprobá que el comando cursor esté instalado.",
                ex);
        }
    }

    static bool TryStartDirect(string folder)
    {
        if (!TryResolveInstall(out var cursorExe, out var cliJs))
            return false;

        var start = new ProcessStartInfo
        {
            FileName = cursorExe,
            Arguments = $"\"{cliJs}\" . --classic",
            WorkingDirectory = folder,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        start.Environment["ELECTRON_RUN_AS_NODE"] = "1";
        start.Environment["VSCODE_DEV"] = "";
        Process.Start(start);
        return true;
    }

    static bool TryStartHiddenCmd(string folder)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/d /c cursor.cmd . --classic",
            WorkingDirectory = folder,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
        return true;
    }

    static bool TryResolveInstall(out string cursorExe, out string cliJs)
    {
        cursorExe = "";
        cliJs = "";

        var cmdPath = FindOnPath("cursor.cmd");
        if (cmdPath == null)
            return false;

        var binDir = Path.GetDirectoryName(cmdPath);
        if (binDir == null)
            return false;

        cursorExe = Path.GetFullPath(Path.Combine(binDir, "..", "..", "..", "Cursor.exe"));
        cliJs = Path.GetFullPath(Path.Combine(binDir, "..", "out", "cli.js"));
        return File.Exists(cursorExe) && File.Exists(cliJs);
    }

    static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            return null;

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim('"'), fileName);
                if (File.Exists(candidate))
                    return Path.GetFullPath(candidate);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
            {
            }
        }

        return null;
    }
}
