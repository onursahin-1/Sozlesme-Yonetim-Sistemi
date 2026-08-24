using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Sys.UI.ViewModels;

namespace Sys.UI.Views;

public partial class ApprovalQueueView : UserControl
{
    public ApprovalQueueView()
    {
        InitializeComponent();
    }

    private async void OnApproveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ApprovalQueueViewModel vm) return;
        if (vm.IsBusy) return; // bir karar zaten işleniyorsa ikinci onay penceresini açma

        try
        {
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner is null) return;

            // Metin ViewModel'den geliyor: karar bir fesih ya da düzenleme talebine
            // aitse "Bu sözleşmeyi onaylamak..." demek yanlış olur.
            var confirmed = await ConfirmDialog.ShowAsync(owner, vm.ApproveConfirmMessage, vm.ApproveConfirmButtonText);
            if (confirmed)
                vm.ApproveCommand.Execute(null);
        }
        catch (Exception ex)
        {
            vm.ErrorMessage = "Onay işlemi sırasında bir hata oluştu: " + ex.Message;
        }
    }

    private async void OnRejectClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ApprovalQueueViewModel vm) return;
        if (vm.IsBusy) return; // bir karar zaten işleniyorsa ikinci onay penceresini açma

        try
        {
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner is null) return;

            var confirmed = await ConfirmDialog.ShowAsync(owner, vm.RejectConfirmMessage, vm.RejectConfirmButtonText);
            if (confirmed)
                vm.RejectCommand.Execute(null);
        }
        catch (Exception ex)
        {
            vm.ErrorMessage = "Red işlemi sırasında bir hata oluştu: " + ex.Message;
        }
    }
}