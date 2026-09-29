namespace OpenCmd.Models;

sealed class ShellOption : ObservableModel
{
    public ShellOption(string label, string executable)
    {
        Label = label;
        Executable = executable;
    }

    public string Label { get; }
    public string Executable { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }
}
