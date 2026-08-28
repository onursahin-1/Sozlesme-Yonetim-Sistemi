using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Sys.Domain;
using Sys.UI.ViewModels;

using Sys.UI.Localization;

namespace Sys.UI.Views;

public partial class ContractDetailView : UserControl
{
    public ContractDetailView()
    {
        InitializeComponent();
    }

    private async void OnDownloadAttachmentClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: Attachment attachment }) return;
        if (DataContext is not ContractDetailViewModel vm) return;

        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = Strings.T("File.SaveAs"),
                SuggestedFileName = attachment.FileName,
            });

            if (file is null) return;

            var path = file.TryGetLocalPath();
            if (path is not null)
            {
                await vm.DownloadAttachmentAsync(attachment, path);
            }
        }
        catch (System.Exception ex)
        {
            vm.ErrorMessage = Strings.T("File.DownloadFailed", ex.Message);
        }
    }

    // "PDF Kaydet". Asıl yazdırma artık ViewModel'deki PrintCommand üzerinden,
    // dosya seçim penceresi olmadan doğrudan yazıcıya gidiyor.
    private async void OnExportPdfClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ContractDetailViewModel vm) return;

        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = Strings.T("File.SavePdf"),
                SuggestedFileName = vm.SuggestedPdfFileName,
                DefaultExtension = "pdf",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType(Strings.T("File.PdfDocument")) { Patterns = new[] { "*.pdf" } }
                },
            });

            if (file is null) return;

            var path = file.TryGetLocalPath();
            if (path is not null)
            {
                await vm.ExportPdfAsync(path);
            }
        }
        catch (System.Exception ex)
        {
            vm.ErrorMessage = Strings.T("File.PdfFailed", ex.Message);
        }
    }

    // İhlali "giderildi" olarak işaretler. Gerekçe zorunlu olduğu için pencere açılır;
    // pencere ayrıca sözleşmenin durumunun değişip değişmeyeceğini de yazar.
    private async void OnResolveViolationClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ViolationRowViewModel row }) return;
        if (DataContext is not ContractDetailViewModel vm) return;

        try
        {
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner is null) return;

            var note = await ResolveViolationDialog.ShowAsync(
                owner, row.ViolationType, vm.IsLastOpenViolation(row), vm.ResolvedStatusText);

            if (note is null) return; // kullanıcı vazgeçti

            await vm.ResolveViolationAsync(row, note);
        }
        catch (System.Exception ex)
        {
            vm.ErrorMessage = Strings.T("Det.ViolationFailed", ex.Message);
        }
    }

    private async void OnDeleteAttachmentClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: Attachment attachment }) return;
        if (DataContext is not ContractDetailViewModel vm) return;

        try
        {
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner is null) return;

            var confirmed = await ConfirmDialog.ShowAsync(owner,
                Strings.T("Confirm.DeleteFile", attachment.FileName),
                Strings.T("Confirm.DeleteFileButton"));

            if (confirmed)
                vm.DeleteAttachmentCommand.Execute(attachment);
        }
        catch (System.Exception ex)
        {
            vm.ErrorMessage = Strings.T("Det.DeleteFailed", ex.Message);
        }
    }
}