using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Sys.Domain;
using Sys.UI.ViewModels;

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
                Title = "Dosyayı Kaydet",
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
            vm.ErrorMessage = "Dosya indirilirken bir hata oluştu: " + ex.Message;
        }
    }

    private async void OnPrintClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ContractDetailViewModel vm) return;

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
            {
                await vm.ExportPdfAsync(path);
            }
        }
        catch (System.Exception ex)
        {
            vm.ErrorMessage = "PDF oluşturulurken bir hata oluştu: " + ex.Message;
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
                $"\"{attachment.FileName}\" dosyasını silmek istediğinizden emin misiniz? Bu işlem geri alınamaz.",
                "Evet, Sil");

            if (confirmed)
                vm.DeleteAttachmentCommand.Execute(attachment);
        }
        catch (System.Exception ex)
        {
            vm.ErrorMessage = "Silme işlemi sırasında bir hata oluştu: " + ex.Message;
        }
    }
}