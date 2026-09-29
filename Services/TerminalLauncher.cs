using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using OpenCmd.Models;

namespace OpenCmd.Services;

static class TerminalLauncher
{
    public static void Launch(string windowTitle, IReadOnlyList<DetectedProject> panes, string shell)
    {
        if (panes.Count == 0)
            throw new InvalidOperationException("Marcá al menos una carpeta.");

        CleanupOldBats();
        var arguments = BuildArguments(windowTitle, panes, shell);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "wt.exe",
                Arguments = arguments,
                UseShellExecute = false
            });
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "wt.exe",
                    Arguments = arguments,
                    UseShellExecute = true
                });
            }
            catch (Exception inner)
            {
                throw new InvalidOperationException(
                    "No se pudo abrir Windows Terminal. Instalá Windows Terminal desde Microsoft Store.",
                    inner);
            }
        }
    }

    public static string BuildArguments(string windowTitle, IReadOnlyList<DetectedProject> panes, string shell)
    {
        var builder = new StringBuilder();
        builder.Append("-w new ");
        AppendNewTab(builder, panes[0], shell, windowTitle);

        switch (panes.Count)
        {
            case 1:
                break;
            case 2:
                AppendSplit(builder, panes[1], shell, vertical: true);
                break;
            case 3:
                AppendSplit(builder, panes[1], shell, vertical: true);
                AppendSplit(builder, panes[2], shell, vertical: false);
                break;
            case 4:
                // wt arma la grilla así: arriba A|B, abajo el último split queda a la derecha.
                AppendSplit(builder, panes[1], shell, vertical: true);
                AppendSplit(builder, panes[3], shell, vertical: false);
                builder.Append(" ; move-focus left");
                AppendSplit(builder, panes[2], shell, vertical: false);
                break;
            default:
                AppendColumns(builder, panes, shell);
                break;
        }

        return builder.ToString();
    }

    static void AppendColumns(StringBuilder builder, IReadOnlyList<DetectedProject> panes, string shell)
    {
        var remaining = panes.Count - 1;
        for (var i = 1; i < panes.Count; i++)
        {
            var size = remaining / (double)(remaining + 1);
            remaining--;
            builder.Append(" ; split-pane -V -s ");
            builder.Append(size.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(' ');
            AppendPane(builder, panes[i], shell);
        }
    }

    static void AppendNewTab(StringBuilder builder, DetectedProject project, string shell, string title)
    {
        builder.Append("new-tab --title ");
        builder.Append(Quote(title));
        builder.Append(" --suppressApplicationTitle -d ");
        builder.Append(Quote(project.FullPath));
        builder.Append(' ');
        AppendShell(builder, project, shell);
    }

    static void AppendSplit(StringBuilder builder, DetectedProject project, string shell, bool vertical)
    {
        builder.Append(vertical ? " ; split-pane -V -s 0.5 " : " ; split-pane -H -s 0.5 ");
        AppendPane(builder, project, shell);
    }

    static void AppendPane(StringBuilder builder, DetectedProject project, string shell)
    {
        builder.Append("--suppressApplicationTitle -d ");
        builder.Append(Quote(project.FullPath));
        builder.Append(' ');
        AppendShell(builder, project, shell);
    }

    static void AppendShell(StringBuilder builder, DetectedProject project, string shell)
    {
        if (shell.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase))
        {
            builder.Append("cmd.exe /k ");
            builder.Append(Quote(WriteBat(project)));
            return;
        }

        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(BuildScript(project)));
        builder.Append(shell);
        builder.Append(" -NoExit -EncodedCommand ");
        builder.Append(encoded);
    }

    static string BuildScript(DetectedProject project)
    {
        var name = EscapePowerShell(project.Name);
        var path = EscapePowerShell(project.FullPath);
        var script = new StringBuilder();
        script.AppendLine($"Set-Location -LiteralPath '{path}'");
        script.AppendLine("try { $Host.UI.RawUI.WindowTitle = '" + name + "' } catch {}");
        script.AppendLine($"Write-Host ' {name} ' -ForegroundColor Black -BackgroundColor Cyan");
        script.AppendLine($"Write-Host '{path}' -ForegroundColor DarkGray");
        script.AppendLine("Write-Host ''");

        var command = project.StartCommand.Trim();
        if (command.Length > 0)
            script.AppendLine(command);

        return script.ToString();
    }

    static string EscapePowerShell(string value) => value.Replace("'", "''");

    static string WriteBat(DetectedProject project)
    {
        var dir = Path.Combine(Path.GetTempPath(), "OpenCmd");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".bat");
        var content = new StringBuilder();
        content.AppendLine("@echo off");
        content.AppendLine("chcp 65001 >nul");
        content.AppendLine("title " + SanitizeBat(project.Name));
        content.AppendLine("echo " + SanitizeBat(project.Name));
        content.AppendLine("echo " + SanitizeBat(project.FullPath));
        var command = project.StartCommand.Trim();
        if (command.Length > 0)
            content.AppendLine(command);
        File.WriteAllText(file, content.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return file;
    }

    static string SanitizeBat(string value) =>
        value.Replace("&", " ").Replace("|", " ").Replace("<", " ").Replace(">", " ").Replace("^", " ");

    static void CleanupOldBats()
    {
        var dir = Path.Combine(Path.GetTempPath(), "OpenCmd");
        if (!Directory.Exists(dir))
            return;

        foreach (var file in Directory.EnumerateFiles(dir, "*.bat"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddHours(-6))
                    File.Delete(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    static string Quote(string value)
    {
        var text = value.Replace("\"", "").Replace(";", "\\;");
        return "\"" + text + "\"";
    }
}
