using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using OpenCmd.Models;
using OpenCmd.Services;
using OpenCmd.ViewModels;

namespace OpenCmd;

public partial class MainWindow : Window
{
    readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        _vm.Load();
        SourceInitialized += (_, _) => WindowTheme.UseDarkTitleBar(this);
    }

    public void OpenFolder(string path)
    {
        var error = _vm.LoadRoot(path);
        if (error != null)
            MessageBox.Show(this, error, "Open CMD", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Elegí la carpeta padre del proyecto",
            Multiselect = false
        };

        if (_vm.RootPath != null && Directory.Exists(_vm.RootPath))
            dialog.InitialDirectory = _vm.RootPath;

        if (dialog.ShowDialog(this) == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
            OpenFolder(dialog.FolderName);
    }

    void Refresh_Click(object sender, RoutedEventArgs e) => _vm.Reload();

    void Home_Click(object sender, RoutedEventArgs e) => _vm.GoHome();

    void OpenRootInCursor_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.RootPath != null)
            OpenInCursor(_vm.RootPath);
    }

    void OpenChildInCursor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DetectedProject project })
            OpenInCursor(project.FullPath);
    }

    void OpenInCursor(string folder)
    {
        try
        {
            CursorLauncher.Open(folder);
        }
        catch (Exception ex)
        {
            var message = ex is InvalidOperationException
                ? ex.Message
                : "No se pudo abrir Cursor. " + ex.Message;
            MessageBox.Show(this, message, "Open CMD", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void Shell_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ShellOption selected })
            return;

        foreach (var shell in _vm.Shells)
        {
            var shouldSelect = shell == selected;
            if (shell.IsSelected != shouldSelect)
                shell.IsSelected = shouldSelect;
        }
    }

    void Open_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _vm.LaunchTerminal();
        }
        catch (Exception ex)
        {
            var message = ex is InvalidOperationException
                ? ex.Message
                : "No se pudo abrir la terminal. " + ex.Message;
            MessageBox.Show(this, message, "Open CMD", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void RecentOpen_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: RecentEntry recent })
            return;

        try
        {
            var error = _vm.OpenSavedProject(recent.FullPath);
            if (error != null)
                MessageBox.Show(this, error, "Open CMD", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            var message = ex is InvalidOperationException
                ? ex.Message
                : "No se pudo abrir la terminal. " + ex.Message;
            MessageBox.Show(this, message, "Open CMD", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void RecentEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: RecentEntry recent })
            OpenFolder(recent.FullPath);
    }

    void RecentCursor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: RecentEntry recent })
            OpenInCursor(recent.FullPath);
    }

    void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DetectedProject project })
            _vm.Move(project, -1);
    }

    void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DetectedProject project })
            _vm.Move(project, 1);
    }

    void ToggleProject_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DetectedProject project })
            project.IsSelected = !project.IsSelected;
    }

    void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        var dropped = e.Data.GetData(DataFormats.FileDrop) as string[];
        var folder = dropped?.FirstOrDefault(Directory.Exists);
        if (folder == null && dropped != null)
        {
            var file = dropped.FirstOrDefault(File.Exists);
            if (file != null)
                folder = Path.GetDirectoryName(file);
        }

        if (!string.IsNullOrEmpty(folder))
            OpenFolder(folder);
    }

    void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ChooseFolder_Click(sender, e);
            e.Handled = true;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _vm.Persist();
        base.OnClosing(e);
    }
}
