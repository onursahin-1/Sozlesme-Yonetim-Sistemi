using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Sys.UI.ViewModels;

namespace Sys.UI.Views;

public partial class ContractTerminationView : UserControl
{
    public ContractTerminationView()
    {
        InitializeComponent();
    }

    private async void OnPickFileClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ContractTerminationViewModel vm) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Fesih Belgesi Seç",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("PDF Dosyaları") { Patterns = new[] { "*.pdf" } }
            }
        });

        if (files.Count > 0)
        {
            var path = files[0].TryGetLocalPath();
            if (path is not null)
            {
                vm.SetSelectedFile(path);
            }
        }
    }
}