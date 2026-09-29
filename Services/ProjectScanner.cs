using System.IO;
using System.Text.Json;
using OpenCmd.Models;

namespace OpenCmd.Services;

sealed class ScanResult
{
    public required List<DetectedProject> Projects { get; init; }
    public bool UsedFallback { get; init; }
}

static class ProjectScanner
{
    static readonly HashSet<string> Ignored = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "bower_components", ".git", ".svn", ".hg", "dist", "build", "out",
        "bin", "obj", ".vs", ".idea", ".vscode", "coverage", ".next", ".nuxt", "target",
        "vendor", "venv", ".venv", "__pycache__", ".turbo", ".cache", ".angular", "tmp",
        "temp", "logs", "log"
    };

    static readonly HashSet<string> Containers = new(StringComparer.OrdinalIgnoreCase)
    {
        "apps", "packages", "services", "projects"
    };

    static readonly HashSet<string> FallbackSkip = new(StringComparer.OrdinalIgnoreCase)
    {
        "docs", "doc", "documentation", "test", "tests", "__tests__", "scripts", "tools",
        "assets", "public", "static", "images", "img", "config", "configs", "examples",
        "example", "demo", "samples", "github"
    };

    static readonly string[] RoleNames =
    [
        "backend", "frontend", "front-end", "back-end", "servidor", "cliente",
        "server", "client", "gateway", "worker", "mobile", "desktop", "admin", "front",
        "back", "api", "web", "app", "www", "site", "cms", "ui"
    ];

    public static ScanResult Scan(string root)
    {
        root = PathUtil.Normalize(root);
        var found = new Dictionary<string, DetectedProject>(StringComparer.OrdinalIgnoreCase);

        foreach (var pattern in ReadWorkspaces(root))
        {
            foreach (var dir in ExpandWorkspace(root, pattern))
                Add(found, dir);
        }

        var children = ListDirs(root);
        foreach (var child in children)
        {
            if (IsProject(child))
                Add(found, child);
        }

        if (found.Count < 2)
        {
            foreach (var child in children)
            {
                if (found.ContainsKey(PathUtil.Normalize(child)))
                    continue;

                var nested = ListDirs(child).Where(IsProject).ToList();
                var name = Path.GetFileName(child);
                if (Containers.Contains(name) || nested.Count >= 2)
                {
                    foreach (var dir in nested)
                        Add(found, dir);
                }
            }
        }

        var usedFallback = false;
        if (found.Count == 0)
        {
            usedFallback = true;
            var candidates = children.Where(dir => !FallbackSkip.Contains(Path.GetFileName(dir))).ToList();
            if (candidates.Count == 0)
                candidates = children;
            foreach (var dir in candidates)
                Add(found, dir);
        }

        var projects = found.Values.ToList();
        projects.Sort(CompareProjects);
        return new ScanResult { Projects = projects, UsedFallback = usedFallback };
    }

    static void Add(Dictionary<string, DetectedProject> found, string dir)
    {
        if (IsIgnored(dir))
            return;

        var full = PathUtil.Normalize(dir);
        if (!found.ContainsKey(full))
            found[full] = Create(full);
    }

    static DetectedProject Create(string dir)
    {
        var kind = DetectKind(dir) ?? "Carpeta";
        return new DetectedProject
        {
            Name = PathUtil.FolderName(dir),
            FullPath = dir,
            Kind = kind,
            CommandHint = Hint(dir)
        };
    }

    static int CompareProjects(DetectedProject a, DetectedProject b)
    {
        var rank = RoleRank(a.Name).CompareTo(RoleRank(b.Name));
        return rank != 0 ? rank : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
    }

    static bool IsProject(string dir) => DetectKind(dir) != null || MatchRole(Path.GetFileName(dir)) != null;

    static bool IsIgnored(string dir)
    {
        var name = Path.GetFileName(dir);
        return name.StartsWith('.') || Ignored.Contains(name);
    }

    static List<string> ListDirs(string root)
    {
        try
        {
            return Directory.EnumerateDirectories(root)
                .Where(dir => !IsIgnored(dir))
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    static string? DetectKind(string dir)
    {
        try
        {
            if (File.Exists(Path.Combine(dir, "package.json"))) return "Node.js";
            if (File.Exists(Path.Combine(dir, "angular.json"))) return "Angular";
            if (File.Exists(Path.Combine(dir, "pyproject.toml")) ||
                File.Exists(Path.Combine(dir, "requirements.txt")) ||
                File.Exists(Path.Combine(dir, "Pipfile")) ||
                File.Exists(Path.Combine(dir, "manage.py"))) return "Python";
            if (File.Exists(Path.Combine(dir, "go.mod"))) return "Go";
            if (File.Exists(Path.Combine(dir, "Cargo.toml"))) return "Rust";
            if (File.Exists(Path.Combine(dir, "pom.xml")) ||
                File.Exists(Path.Combine(dir, "build.gradle")) ||
                File.Exists(Path.Combine(dir, "build.gradle.kts"))) return "Java";
            if (HasFile(dir, "*.csproj") || HasFile(dir, "*.fsproj") || HasFile(dir, "*.sln")) return ".NET";
            if (File.Exists(Path.Combine(dir, "composer.json"))) return "PHP";
            if (File.Exists(Path.Combine(dir, "Gemfile"))) return "Ruby";
            if (File.Exists(Path.Combine(dir, "pubspec.yaml"))) return "Flutter";
            if (File.Exists(Path.Combine(dir, "mix.exs"))) return "Elixir";
            if (File.Exists(Path.Combine(dir, "deno.json")) || File.Exists(Path.Combine(dir, "deno.jsonc"))) return "Deno";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return null;
    }

    static bool HasFile(string dir, string pattern)
    {
        try
        {
            return Directory.EnumerateFiles(dir, pattern).Any();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    static string Hint(string dir)
    {
        if (File.Exists(Path.Combine(dir, "package.json")))
        {
            var scripts = ReadNpmScripts(dir);
            if (scripts.Contains("dev")) return "npm run dev";
            if (scripts.Contains("start")) return "npm start";
        }

        if (File.Exists(Path.Combine(dir, "manage.py"))) return "py manage.py runserver";
        if (File.Exists(Path.Combine(dir, "artisan"))) return "php artisan serve";
        if (File.Exists(Path.Combine(dir, "go.mod"))) return "go run .";
        if (File.Exists(Path.Combine(dir, "Cargo.toml"))) return "cargo run";
        if (HasFile(dir, "*.csproj") || HasFile(dir, "*.sln")) return "dotnet run";
        return "";
    }

    static HashSet<string> ReadNpmScripts(string dir)
    {
        var scripts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "package.json")));
            if (doc.RootElement.TryGetProperty("scripts", out var node) && node.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in node.EnumerateObject())
                    scripts.Add(property.Name);
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
        }

        return scripts;
    }

    static IEnumerable<string> ReadWorkspaces(string root)
    {
        var file = Path.Combine(root, "package.json");
        if (!File.Exists(file))
            yield break;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(File.ReadAllText(file));
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            yield break;
        }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("workspaces", out var workspaces))
                yield break;

            JsonElement list;
            if (workspaces.ValueKind == JsonValueKind.Array)
                list = workspaces;
            else if (workspaces.ValueKind == JsonValueKind.Object &&
                     workspaces.TryGetProperty("packages", out var packages) &&
                     packages.ValueKind == JsonValueKind.Array)
                list = packages;
            else
                yield break;

            foreach (var item in list.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var value = item.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                        yield return value;
                }
            }
        }
    }

    static IEnumerable<string> ExpandWorkspace(string root, string pattern)
    {
        var relative = pattern.Replace('/', Path.DirectorySeparatorChar).Trim();
        var star = relative.IndexOf('*');
        if (star >= 0)
        {
            var baseRelative = relative[..star].TrimEnd(Path.DirectorySeparatorChar);
            var baseDir = baseRelative.Length == 0 ? root : Path.Combine(root, baseRelative);
            if (!Directory.Exists(baseDir))
                yield break;

            foreach (var dir in ListDirs(baseDir))
                yield return dir;
            yield break;
        }

        var direct = Path.Combine(root, relative);
        if (Directory.Exists(direct))
            yield return direct;
    }

    static string? MatchRole(string name)
    {
        foreach (var role in RoleNames)
        {
            if (name.Equals(role, StringComparison.OrdinalIgnoreCase))
                return role;
        }

        string? best = null;
        foreach (var role in RoleNames)
        {
            if (name.StartsWith(role + "-", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith(role + "_", StringComparison.OrdinalIgnoreCase))
            {
                if (best == null || role.Length > best.Length)
                    best = role;
            }
        }

        return best;
    }

    static int RoleRank(string name)
    {
        var role = MatchRole(name)?.ToLowerInvariant();
        return role switch
        {
            "backend" or "back-end" or "back" or "server" or "servidor" or "api" or "gateway" or "worker" => 0,
            "frontend" or "front-end" or "front" or "client" or "cliente" or "web" or "app" or "ui" or "www"
                or "site" or "mobile" or "admin" or "cms" or "desktop" => 1,
            _ => 2
        };
    }
}
