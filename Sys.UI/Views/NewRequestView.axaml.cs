using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Sys.UI.Localization;
using Sys.UI.ViewModels;


namespace Sys.UI.Views;

public partial class NewRequestView : UserControl
{
    public NewRequestView()
    {
        InitializeComponent();
    }

    private async void OnPickFileClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not NewRequestViewModel vm) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings.T("File.ChooseFile"),
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new(Strings.T("File.Supported")) { Patterns = new[] { "*.pdf", "*.docx", "*.xlsx", "*.jpg", "*.png" } }
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
    private void OnAmountLostFocus(object? sender, RoutedEventArgs e) => AmountFormatHelper.Format(sender);
    private void OnAmountTextChanged(object? sender, TextChangedEventArgs e) => AmountFormatHelper.FormatLive(sender);

}