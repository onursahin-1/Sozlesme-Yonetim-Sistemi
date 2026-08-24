using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Sys.UI.ViewModels;

namespace Sys.UI.Views;

public partial class ArchiveView : UserControl
{
    public ArchiveView()
    {
        InitializeComponent();
    }

    // Listeyi Excel'e aktarır (seçili sözleşmenin PDF'inden farklı: bu, filtreye
    // uyan tüm kayıtları içerir).
    private async void OnExportExcelClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ArchiveViewModel vm) return;

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
        catch (System.Exception ex)
        {
            vm.ErrorMessage = "Excel'e aktarılırken bir hata oluştu: " + ex.Message;
        }
    }

    // Dosya seçim penceresi yalnızca code-behind'dan (TopLevel üzerinden) açılabildiği
    // için, seçilen yol ViewModel'e buradan iletiliyor.
    // "PDF Kaydet". Asıl yazdırma artık ViewModel'deki PrintCommand üzerinden,
    // dosya seçim penceresi olmadan doğrudan yazıcıya gidiyor.
    private async void OnExportPdfClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ArchiveViewModel vm) return;

        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "PDF Olarak Kaydet",
                SuggestedFileName = vm.SuggestedPdfFileName,
                DefaultExtension = "pdf",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("PDF Belgesi") { Patterns = new[] { "*.pdf" } }
                },
            });

            if (file is null) return;

            var path = file.TryGetLocalPath();
            if (path is not null)
                await vm.ExportPdfAsync(path);
        }
        catch (System.Exception ex)
        {
            vm.ErrorMessage = "PDF oluşturulurken bir hata oluştu: " + ex.Message;
        }
    }
}
