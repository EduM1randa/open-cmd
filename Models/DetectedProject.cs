namespace OpenCmd.Models;

sealed class DetectedProject : ObservableModel
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public required string Kind { get; init; }
    public string CommandHint { get; init; } = "";

    private bool _isSelected = true;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    private string _startCommand = "";
    public string StartCommand
    {
        get => _startCommand;
        set => SetField(ref _startCommand, value);
    }

    public string CommandPlaceholder =>
        string.IsNullOrWhiteSpace(CommandHint) ? "opcional" : $"opcional, ej. {CommandHint}";
}
