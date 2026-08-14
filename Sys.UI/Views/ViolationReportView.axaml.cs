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
}