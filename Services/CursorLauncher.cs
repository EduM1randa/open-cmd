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
            Process.Start(new ProcessStartInfo
            {
                FileName = "cursor.cmd",
                Arguments = ". --classic",
                WorkingDirectory = folder,
                UseShellExecute = true
            });
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            throw new InvalidOperationException(
                "No se pudo abrir Cursor. Comprobá que el comando cursor esté instalado.",
                ex);
        }
    }
}
