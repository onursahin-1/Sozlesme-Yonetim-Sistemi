using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Sys.UI.ViewModels;

namespace Sys.UI.Views;

public partial class ViolationReportView : UserControl
{
    public ViolationReportView()
    {
        InitializeComponent();
    }

    private async void OnPickFileClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ViolationReportViewModel vm) return;

        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Dosya Seç",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new("Desteklenen Dosyalar") { Patterns = new[] { "*.pdf", "*.docx", "*.xlsx", "*.jpg", "*.png" } }
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
            vm.ErrorMessage = "Dosya seçilirken bir hata oluştu: " + ex.Message;
        }
    }

    // İhlal bildiriminin onay süreci yok: kaydedildiği anda sözleşme "İhlal Mevcut"
    // durumuna geçiyor ve bu durumu geri alacak bir akış bulunmuyor. Fesih ve düzenleme
    // taleplerinde onay penceresi varken burada hiç yoktu.
    private async void OnSubmitClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ViolationReportViewModel vm) return;
        if (vm.IsBusy) return;

        try
        {
            if (!vm.CanSubmit()) return; // zorunlu alanlar eksikse pencere açılmadan hata gösterilir

            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner is null) return;

            var confirmed = await ConfirmDialog.ShowAsync(owner,
                $"\"{vm.CurrentTitle}\" sözleşmesi için ihlal bildirilecek.\n\n" +
                "Sözleşme hemen \"İhlal Mevcut\" durumuna geçecek ve kayıt sözleşmenin " +
                "geçmişinde kalıcı olacak. Devam etmek istiyor musunuz?",
                "Evet, İhlali Bildir");

            if (confirmed)
                vm.SubmitCommand.Execute(null);
        }
        catch (Exception ex)
        {
            vm.ErrorMessage = "İhlal bildirilirken bir hata oluştu: " + ex.Message;
        }
    }
}