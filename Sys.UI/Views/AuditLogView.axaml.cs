using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Sys.UI.ViewModels;

namespace Sys.UI.Views;

public partial class AuditLogView : UserControl
{
    public AuditLogView()
    {
        InitializeComponent();
    }

    // Dosya seçim penceresi yalnızca kod-arkasından (TopLevel üzerinden) açılabildiği
    // için, seçilen yol ViewModel'e buradan iletiliyor.
    private async void OnExportExcelClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AuditLogViewModel vm) return;

        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Excel Olarak Kaydet",
                SuggestedFileName = vm.SuggestedExportFileName,
                DefaultExtension = "xlsx",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Excel Çalışma Kitabı") { Patterns = new[] { "*.xlsx" } }
                },
            });

            if (file is null) return;

            var path = file.TryGetLocalPath();
            if (path is not null) await vm.ExportToExcelAsync(path);
        }
        catch (Exception ex)
        {
            vm.ErrorMessage = "Excel'e aktarılırken bir hata oluştu: " + ex.Message;
        }
    }
}
