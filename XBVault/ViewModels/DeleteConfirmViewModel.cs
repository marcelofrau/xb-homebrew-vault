using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XBVault.ViewModels;

public partial class DeleteConfirmViewModel : ObservableObject
{
    public DeleteConfirmViewModel(string summary, List<string> filePaths)
    {
        Summary = summary;
        FilePaths = filePaths;
    }

    public string Summary { get; }
    public List<string> FilePaths { get; }
    public string FileListText => string.Join('\n', FilePaths);
    public int FileCount => FilePaths.Count;
    public bool Confirmed { get; private set; }
    public event System.Action<bool>? Completed;

    [RelayCommand]
    private void Confirm()
    {
        Confirmed = true;
        Completed?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        Confirmed = false;
        Completed?.Invoke(false);
    }
}
