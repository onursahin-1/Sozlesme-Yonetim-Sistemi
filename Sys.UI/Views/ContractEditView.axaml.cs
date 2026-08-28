using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Sys.UI.Localization;
using Sys.UI.ViewModels;

namespace Sys.UI.Views;

public partial class ContractEditView : UserControl
{
    public ContractEditView()
    {
        InitializeComponent();
    }

    private async void OnPickFileClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ContractEditViewModel vm) return;

        try
        {
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
        catch (Exception ex)
        {
            vm.ErrorMessage = Strings.T("File.PickFailed", ex.Message);
        }
    }
    private void OnAmountLostFocus(object? sender, RoutedEventArgs e) => AmountFormatHelper.Format(sender);
    private void OnAmountTextChanged(object? sender, TextChangedEventArgs e) => AmountFormatHelper.FormatLive(sender);
}