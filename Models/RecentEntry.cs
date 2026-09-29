namespace OpenCmd.Models;

sealed class RecentEntry
{
    public RecentEntry(string fullPath, string summary)
    {
        FullPath = fullPath;
        Summary = summary;
    }

    public string FullPath { get; }
    public string Summary { get; }
    public string Name => PathUtil.FolderName(FullPath);
    public string Parent => Path.GetDirectoryName(FullPath) ?? "";
}
