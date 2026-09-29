using System.Collections.ObjectModel;
using OpenCmd.Models;
using OpenCmd.Services;

namespace OpenCmd.ViewModels;

sealed class MainViewModel : ObservableModel
{
    const int MaxRecent = 3;

    readonly ObservableCollection<DetectedProject> _projects = [];
    readonly ObservableCollection<RecentEntry> _recents = [];
    AppSettings _settings = new();
    string? _statusOverride;
    bool _usedFallback;

    public MainViewModel()
    {
        Shells.Add(new ShellOption("PowerShell", "powershell.exe"));
        if (ExecutableExists("pwsh.exe"))
            Shells.Add(new ShellOption("PowerShell 7", "pwsh.exe"));
        Shells.Add(new ShellOption("CMD", "cmd.exe"));
        Shells[0].IsSelected = true;
    }

    public ObservableCollection<DetectedProject> Projects => _projects;
    public ObservableCollection<RecentEntry> Recents => _recents;
    public ObservableCollection<ShellOption> Shells { get; } = [];

    public string? RootPath { get; private set; }

    public bool HasRoot => RootPath != null;
    public string RootName => RootPath == null ? "" : PathUtil.FolderName(RootPath);
    public string WindowTitle => RootPath == null ? "Open CMD" : $"Open CMD — {RootName}";
    public bool HasRecents => Recents.Count > 0;
    public bool ShowRecentHome => !HasRoot && HasRecents;
    public bool ShowProjects => Projects.Count > 0;
    public bool ShowEmptyFolder => HasRoot && Projects.Count == 0;
    public bool ShowFallback => HasRoot && _usedFallback && Projects.Count > 0;

    public bool CanOpen => Projects.Any(project => project.IsSelected);

    public string ProjectsHeading => Projects.Count switch
    {
        0 => "Partes del proyecto",
        1 => "1 carpeta",
        _ => $"{Projects.Count} carpetas"
    };

    public string OpenLabel
    {
        get
        {
            var count = Projects.Count(project => project.IsSelected);
            return count >= 2 ? $"Abrir en {count} paneles" : "Abrir PowerShell";
        }
    }

    public string StatusText
    {
        get
        {
            if (!string.IsNullOrEmpty(_statusOverride))
                return _statusOverride;

            var selected = Projects.Where(project => project.IsSelected).ToList();
            return selected.Count switch
            {
                0 when !HasRoot => "Elegí una carpeta para empezar.",
                0 => "Marcá al menos una carpeta.",
                1 => $"Una consola, en {selected[0].Name}.",
                2 => $"{selected[0].Name} a la izquierda, {selected[1].Name} a la derecha.",
                3 => $"{selected[0].Name} a la izquierda. {selected[1].Name} arriba a la derecha y {selected[2].Name} abajo.",
                4 => "Grilla de 2×2, en el orden de la lista.",
                _ => $"{selected.Count} paneles, en el orden de la lista."
            };
        }
    }

    public string SelectedShell =>
        Shells.FirstOrDefault(shell => shell.IsSelected)?.Executable ?? "powershell.exe";

    public void Load()
    {
        _settings = SettingsStore.Load();
        foreach (var shell in Shells)
            shell.IsSelected = shell.Executable.Equals(_settings.Shell, StringComparison.OrdinalIgnoreCase);
        if (Shells.All(shell => !shell.IsSelected))
            Shells[0].IsSelected = true;
        if (TrimRecents())
            Save();
        ReloadRecents();
        Notify();
    }

    public string? LoadRoot(string path)
    {
        if (!Directory.Exists(path))
        {
            RemoveRecent(path);
            Save();
            Notify();
            return "Esa carpeta ya no existe.";
        }

        RememberCurrentProjects();
        RootPath = PathUtil.Normalize(path);
        ApplyScan(ProjectScanner.Scan(RootPath));
        _statusOverride = null;
        Save();
        Notify();
        return null;
    }

    public string? OpenSavedProject(string path)
    {
        var error = LoadRoot(path);
        if (error != null)
            return error;

        LaunchTerminal();
        return null;
    }

    public void GoHome()
    {
        if (RootPath == null)
            return;

        RememberCurrentProjects();
        Save();
        RootPath = null;
        _usedFallback = false;
        _statusOverride = null;
        ReplaceProjects([]);
        Notify();
    }

    public void Reload()
    {
        if (RootPath == null)
            return;

        RememberCurrentProjects();
        ApplyScan(ProjectScanner.Scan(RootPath));
        _statusOverride = null;
        Save();
        Notify();
    }

    public void Move(DetectedProject project, int delta)
    {
        var index = Projects.IndexOf(project);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= Projects.Count)
            return;

        Projects.Move(index, target);
        _statusOverride = null;
        Raise(nameof(StatusText));
    }

    public void LaunchTerminal()
    {
        if (RootPath == null)
            throw new InvalidOperationException("Elegí una carpeta.");

        var selected = Projects.Where(project => project.IsSelected).ToList();
        if (selected.Count == 0)
            throw new InvalidOperationException("Marcá al menos una carpeta.");

        RememberCurrentProjects();
        TouchRecent(RootPath);
        Save();
        TerminalLauncher.Launch(RootName, selected, SelectedShell);
        _statusOverride = "Terminal abierta.";
        Raise(nameof(StatusText));
    }

    public void Persist()
    {
        RememberCurrentProjects();
        Save();
    }

    void ApplyScan(ScanResult scan)
    {
        ReplaceProjects(scan.Projects);
        _usedFallback = scan.UsedFallback;
        if (RootPath == null)
            return;

        HashSet<string> hidden = [];
        if (_settings.Unchecked.TryGetValue(RootPath, out var uncheckedPaths))
            hidden = new HashSet<string>(uncheckedPaths, StringComparer.OrdinalIgnoreCase);

        foreach (var project in Projects)
        {
            project.IsSelected = !hidden.Contains(project.FullPath);
            if (_settings.Commands.TryGetValue(project.FullPath, out var command))
                project.StartCommand = command;
        }

        if (_settings.Order.TryGetValue(RootPath, out var order) && order.Count > 0)
            ApplyOrder(order);
    }

    void ApplyOrder(List<string> order)
    {
        var rank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < order.Count; i++)
            rank[order[i]] = i;

        var sorted = Projects
            .Select((project, index) => (project, index))
            .OrderBy(item => rank.TryGetValue(item.project.FullPath, out var saved) ? saved : 10_000 + item.index)
            .Select(item => item.project)
            .ToList();

        ReplaceProjects(sorted);
    }

    void ReplaceProjects(IReadOnlyList<DetectedProject> projects)
    {
        foreach (var project in Projects)
            project.PropertyChanged -= OnProjectChanged;

        Projects.Clear();
        foreach (var project in projects)
        {
            project.PropertyChanged += OnProjectChanged;
            Projects.Add(project);
        }
    }

    void OnProjectChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DetectedProject.IsSelected))
            return;

        _statusOverride = null;
        Raise(nameof(CanOpen));
        Raise(nameof(OpenLabel));
        Raise(nameof(StatusText));
    }

    void RememberCurrentProjects()
    {
        _settings.Shell = SelectedShell;
        if (RootPath == null)
            return;

        _settings.Unchecked[RootPath] = Projects.Where(project => !project.IsSelected).Select(project => project.FullPath).ToList();
        _settings.Order[RootPath] = Projects.Select(project => project.FullPath).ToList();

        foreach (var project in Projects)
        {
            var command = project.StartCommand.Trim();
            if (command.Length == 0)
                _settings.Commands.Remove(project.FullPath);
            else
                _settings.Commands[project.FullPath] = command;
        }
    }

    void TouchRecent(string path)
    {
        _settings.Recent.RemoveAll(item => item.Equals(path, StringComparison.OrdinalIgnoreCase));
        _settings.Recent.Insert(0, path);
        TrimRecents();
        ReloadRecents();
    }

    bool TrimRecents()
    {
        var before = _settings.Recent.Count;
        _settings.Recent.RemoveAll(path => !Directory.Exists(path));
        if (_settings.Recent.Count > MaxRecent)
            _settings.Recent.RemoveRange(MaxRecent, _settings.Recent.Count - MaxRecent);
        return _settings.Recent.Count != before;
    }

    void RemoveRecent(string path)
    {
        var key = path;
        try { key = PathUtil.Normalize(path); } catch (Exception ex) when (ex is IOException or ArgumentException) { }
        _settings.Recent.RemoveAll(item => item.Equals(key, StringComparison.OrdinalIgnoreCase) || item.Equals(path, StringComparison.OrdinalIgnoreCase));
        ReloadRecents();
    }

    void ReloadRecents()
    {
        Recents.Clear();
        foreach (var path in _settings.Recent.Take(MaxRecent))
        {
            if (Directory.Exists(path))
                Recents.Add(new RecentEntry(path, DescribeRecent(path)));
        }

        Raise(nameof(HasRecents));
        Raise(nameof(ShowRecentHome));
    }

    string DescribeRecent(string path)
    {
        _settings.Order.TryGetValue(path, out var order);
        _settings.Unchecked.TryGetValue(path, out var hiddenList);
        var hidden = new HashSet<string>(hiddenList ?? [], StringComparer.OrdinalIgnoreCase);
        var names = (order ?? [])
            .Where(folder => !hidden.Contains(folder) && Directory.Exists(folder))
            .Select(PathUtil.FolderName)
            .ToList();

        if (names.Count == 0)
            names = ProjectScanner.Scan(path).Projects.Select(project => project.Name).ToList();

        return names.Count == 0 ? "Sin carpetas internas" : string.Join(" · ", names);
    }

    void Save() => SettingsStore.Save(_settings);

    void Notify()
    {
        Raise(nameof(HasRoot));
        Raise(nameof(RootPath));
        Raise(nameof(RootName));
        Raise(nameof(WindowTitle));
        Raise(nameof(HasRecents));
        Raise(nameof(ShowRecentHome));
        Raise(nameof(ShowProjects));
        Raise(nameof(ShowEmptyFolder));
        Raise(nameof(ShowFallback));
        Raise(nameof(CanOpen));
        Raise(nameof(OpenLabel));
        Raise(nameof(ProjectsHeading));
        Raise(nameof(StatusText));
    }

    static bool ExecutableExists(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
            return false;

        foreach (var dir in path.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                if (File.Exists(Path.Combine(dir.Trim(), fileName)))
                    return true;
            }
            catch (Exception ex) when (ex is IOException or ArgumentException)
            {
            }
        }

        return false;
    }
}
