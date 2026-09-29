using System.IO;
using System.Windows;

namespace OpenCmd;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, "Open CMD", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        var window = new MainWindow();
        MainWindow = window;
        var folder = ResolveFolder(e.Args);
        if (folder != null)
            window.OpenFolder(folder);
        window.Show();
    }

    private static string? ResolveFolder(string[] args)
    {
        foreach (var raw in args)
        {
            var arg = raw.Trim().Trim('"');
            if (arg.Length == 0)
                continue;
            if (Directory.Exists(arg))
                return arg;
            if (File.Exists(arg))
            {
                var parent = Path.GetDirectoryName(arg);
                if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                    return parent;
            }
        }

        return null;
    }
}
