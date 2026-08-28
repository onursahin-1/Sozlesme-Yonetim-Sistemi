﻿using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Sys.UI.Localization;
using Sys.UI.ViewModels;

namespace Sys.UI.Views;

public partial class ShellView : UserControl
{
    public ShellView()
    {
        InitializeComponent();
    }

    private async void OnToggleThemeClick(object? sender, RoutedEventArgs e)
    {
        if (await ConfirmDiscardAsync()) ((ShellViewModel)DataContext!).ToggleTheme();
    }

    private async void OnToggleLanguageClick(object? sender, RoutedEventArgs e)
    {
        if (await ConfirmDiscardAsync()) ((ShellViewModel)DataContext!).ToggleLanguage();
    }

    // Tema/dil değişimi açık sayfayı yeniden kuruyor; yarım kalmış bir formdaysak
    // girilenler gider. Kayıp sessiz olmasın diye önce soruluyor.
    //
    // Boş formda soru YOK: her geçişte onay istemek uyarıyı anlamsızlaştırır ve
    // insanlar okumadan onaylamayı öğrenir. Uyarı ancak nadir çıktığında işe yarar.
    private async Task<bool> ConfirmDiscardAsync()
    {
        if (DataContext is not ShellViewModel vm) return false;
        if (!vm.CurrentPageHasUnsavedInput) return true;

        if (TopLevel.GetTopLevel(this) is not Window owner) return true;

        return await ConfirmDialog.ShowAsync(owner,
            Strings.T("Confirm.DiscardInput"),
            Strings.T("Confirm.DiscardInputButton"));
    }
}
