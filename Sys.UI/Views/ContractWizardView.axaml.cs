using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Sys.Domain;
using Sys.UI.ViewModels;

namespace Sys.UI.Views;

public partial class ContractWizardView : UserControl
{
    public ContractWizardView()
    {
        InitializeComponent();
    }

    private void OnPickSozlesmeFileClick(object? sender, RoutedEventArgs e) => PickFile(AttachmentCategory.Sozlesme);
    private void OnPickEkFileClick(object? sender, RoutedEventArgs e) => PickFile(AttachmentCategory.Ek);
    private void OnPickTeminatFileClick(object? sender, RoutedEventArgs e) => PickFile(AttachmentCategory.Teminat);

    private async void PickFile(AttachmentCategory category)
    {
        if (DataContext is not ContractWizardViewModel vm) return;

        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Dosya Seç",
                AllowMultiple = true,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new("Desteklenen Dosyalar") { Patterns = new[] { "*.pdf", "*.docx", "*.xlsx", "*.jpg", "*.png" } }
                }
            });

            foreach (var file in files)
            {
                var path = file.TryGetLocalPath();
                if (path is not null)
                {
                    vm.AddFile(path, category);
                }
            }
        }
        catch (Exception ex)
        {
            vm.ErrorMessage = "Dosya seçilirken bir hata oluştu: " + ex.Message;
        }
    }
    private void OnAmountLostFocus(object? sender, RoutedEventArgs e) => AmountFormatHelper.Format(sender);
    private void OnAmountTextChanged(object? sender, TextChangedEventArgs e) => AmountFormatHelper.FormatLive(sender);
}