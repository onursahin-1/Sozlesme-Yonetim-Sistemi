using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Sys.UI.ViewModels;

using Sys.UI.Localization;

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

        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Strings.T("File.ChooseTermination"),
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new(Strings.T("File.PdfFiles")) { Patterns = new[] { "*.pdf" } }
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

    private async void OnSubmitClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ContractTerminationViewModel vm) return;

        try
        {
            if (!vm.CanSubmit()) return; // zorunlu alanlar eksikse onay penceresi açılmadan hata gösterilir

            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner is null) return;

            // Onay metni hangi sözleşmenin feshedileceğini adıyla yazıyor: listede
            // yanlış satır seçilmişse son fırsat burası.
            var confirmed = await ConfirmDialog.ShowAsync(owner,
                Strings.T("Confirm.TerminationAsk", vm.CurrentTitle),
                Strings.T("Confirm.TerminationButton"));

            if (confirmed)
                vm.SubmitCommand.Execute(null);
        }
        catch (Exception ex)
        {
            vm.ErrorMessage = Strings.T("Term.SubmitFailed", ex.Message);
        }
    }
}