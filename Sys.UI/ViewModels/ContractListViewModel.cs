using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sys.Domain;
using Sys.Services;
using System.Collections.Generic;

namespace Sys.UI.ViewModels;

public partial class ContractListViewModel : ViewModelBase
{
    private readonly ContractService _contractService;
    private readonly User _currentUser;

    // Sayfa başına kayıt sayısı tek merkezden (PagingDefaults) gelir; böylece
    // denetim kaydı vb. diğer sayfalanan ekranlarla her zaman tutarlı kalır.
    private const int PageSize = PagingDefaults.PageSize;

    // Arama kutusuna her harf yazıldığında veritabanına gitmemek için kısa bir
    // bekleme uygulanır (debounce). Kullanıcı yazmayı bıraktıktan ~350 ms sonra
    // tek bir sorgu atılır; bu sürede yeni harf gelirse önceki bekleme iptal edilir.
    private CancellationTokenSource? _searchDebounceCts;

    // Art arda gelen yüklemelerde geç dönen eski bir sorgunun, daha yeni bir
    // sorgunun sonucunu ezmesini engeller (denetim kaydı ekranındaki desenle aynı).
    private int _loadToken;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial ObservableCollection<ContractCardViewModel> FilteredContracts { get; set; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    public partial string SelectedFilter { get; set; } = "tumu";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    public partial string SearchText { get; set; } = string.Empty;

    // Tür filtresi. Durum filtresinden BAĞIMSIZ bir boyut: "Aktif + Hizmet" gibi
    // birleşimler kurulabilir. Gösterge panelindeki tür dağılımından tıklanarak da
    // buraya gelinir.
    //
    // Seçenekler sabit bir listeden değil VERİDEN geliyor: sözleşme türü serbest
    // metin olarak da girilebiliyor ve sabit listede olmayan bir tür filtreyle hiç
    // bulunamaz hâle gelirdi.
    public const string AllTypes = "Tüm türler";

    [ObservableProperty]
    public partial ObservableCollection<string> TypeOptions { get; set; } = new() { AllTypes };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    public partial string SelectedType { get; set; } = AllTypes;

    partial void OnSelectedTypeChanged(string value)
    {
        CurrentPage = 1;
        _ = LoadAsync();
    }

    // Servise gönderilen değer: "Tüm türler" seçiliyken filtre uygulanmamalı.
    private string? TypeFilter => SelectedType == AllTypes ? null : SelectedType;

    // Arama metni değiştiğinde sayfa 1'e döner — aksi halde kullanıcı 3. sayfadayken
    // arama yaptığında sonuç 3 sayfadan azsa boş bir ekranla karşılaşırdı.
    partial void OnSearchTextChanged(string value) => DebouncedReloadFirstPage();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoading { get; set; } = true;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ContractCardViewModel? SelectedContract { get; set; }

    // Red işlemi sürerken ikinci bir diyaloğun açılmasını engeller; aksi halde aynı
    // talep iki kez reddedilmeye çalışılır ve ikincisi eşzamanlılık hatası verirdi.
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    // --- Sayfalama durumu ---

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageInfoText))]
    [NotifyPropertyChangedFor(nameof(CanGoPrevious))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageInfoText))]
    [NotifyPropertyChangedFor(nameof(CanGoPrevious))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    [NotifyPropertyChangedFor(nameof(ShowPager))]
    [NotifyPropertyChangedFor(nameof(TotalPages))]
    public partial int TotalCount { get; set; }

    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool CanGoPrevious => CurrentPage > 1;
    public bool CanGoNext => CurrentPage < TotalPages;
    public bool ShowPager => TotalCount > PageSize;
    public string PageInfoText => $"Sayfa {CurrentPage} / {TotalPages}  ·  Toplam {TotalCount} kayıt";

    // Filtre butonlarında hangisinin seçili olduğunu görsel olarak belirtmek ve
    // sonuç bulunamadığında "filtreleri temizle" aksiyonunu göstermek için kullanılır.
    public bool IsEmpty => !IsLoading && FilteredContracts.Count == 0;
    public bool HasActiveFilters => SelectedFilter != "tumu" || !string.IsNullOrWhiteSpace(SearchText) || TypeFilter is not null;

    public event Action<Contract>? EditRequested;
    public event Action<Contract>? ViewDetailsRequested;
    public event Action<Contract>? ContractCreationRequested;
    public event Action<Contract>? SonKontrolRequested;

    // Gösterge panelindeki durum kartlarından ("Aktif", "Onay Bekliyor" vb.) bu ekrana
    // geçilirken belirli bir filtrenin baştan uygulanmış gelmesi için opsiyonel parametre.
    public ContractListViewModel(ContractService contractService, User currentUser,
                                 string? initialFilter = null, string? initialType = null)
    {
        _contractService = contractService;
        _currentUser = currentUser;
        if (!string.IsNullOrEmpty(initialFilter))
            SelectedFilter = initialFilter;

        // Panelden tür dağılımına tıklanarak gelindiyse, durum filtresi "tümü"
        // kalır: kullanıcı o türdeki HER sözleşmeyi görmek istiyor.
        _initialType = initialType;

        _ = InitializeAsync();
    }

    private readonly string? _initialType;

    private async Task InitializeAsync()
    {
        await LoadTypeOptionsAsync();

        // Tür ataması seçenekler geldikten SONRA yapılıyor; aksi halde ComboBox
        // listesinde bulunmayan bir değer atanır ve seçim boş görünürdü.
        if (!string.IsNullOrEmpty(_initialType) && TypeOptions.Contains(_initialType))
            SelectedType = _initialType;   // OnSelectedTypeChanged yüklemeyi tetikler
        else
            await LoadAsync();
    }

    private async Task LoadTypeOptionsAsync()
    {
        try
        {
            var types = await _contractService.GetContractTypeOptionsAsync(_currentUser);
            TypeOptions = new ObservableCollection<string>(new[] { AllTypes }.Concat(types));
        }
        catch
        {
            // Tür listesi alınamazsa filtre yalnızca "Tüm türler" ile çalışır;
            // ekranın geri kalanı etkilenmemeli.
            TypeOptions = new ObservableCollection<string> { AllTypes };
        }
    }

    [RelayCommand]
    private void EditRequest(ContractCardViewModel card)
    {
        EditRequested?.Invoke(card.RawContract);
    }

    [RelayCommand]
    private void ViewDetails(ContractCardViewModel card)
    {
        ViewDetailsRequested?.Invoke(card.RawContract);
    }

    [RelayCommand]
    private void RequestContractCreation(ContractCardViewModel card)
    {
        ContractCreationRequested?.Invoke(card.RawContract);
    }

    [RelayCommand]
    private void RequestSonKontrol(ContractCardViewModel card)
    {
        SonKontrolRequested?.Invoke(card.RawContract);
    }

    // Talep reddi. Gerekçe ve "yeniden gönderilebilir mi" bilgisi diyalogdan geldiği
    // için bu metot bir RelayCommand değil; görünüm kod-arkası diyaloğu açıp çağırır.
    public async Task<bool> RejectRequestAsync(ContractCardViewModel card, string note, bool allowResubmit)
    {
        if (IsBusy) return false;

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            await _contractService.RejectRequestAsync(card.RawContract, _currentUser, note, allowResubmit);

            // Kart durum değiştirdiği için (Talep → İade/Reddedildi) liste yeniden çekilir;
            // kapatılan talep varsayılan "Tümü" filtresinde artık görünmez.
            await LoadAsync();
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Talep reddedilirken bir hata oluştu: " + ex.Message;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // --- Excel'e aktarma ---
    //
    // Ekrandaki filtreler aynen uygulanır ama SAYFA DEĞİL, eşleşen tüm kayıtlar
    // aktarılır — aktarmanın amacı analiz. Dosya seçim penceresi kod-arkasından
    // açıldığı için yol buraya iletiliyor.

    public string SuggestedExportFileName => Exporting.ExcelExporter.SuggestFileName("Sozlesmeler");

    public async Task ExportToExcelAsync(string destinationPath)
    {
        if (IsBusy) return;

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var rows = await _contractService.GetContractsForExportAsync(_currentUser, SelectedFilter, SearchText, TypeFilter);

            Exporting.ExcelExporter.ExportContracts(rows, destinationPath, "Sözleşmeler");
            await _contractService.LogExportAsync(_currentUser, "Sözleşme listesi", rows.Count);

            if (rows.Count >= ContractService.MaxExportRows)
                ErrorMessage = $"Aktarma {ContractService.MaxExportRows} kayıtla sınırlandı. " +
                               "Tümünü almak için filtreyi daraltıp tekrar deneyin.";

            Printing.DocumentPrinter.Open(destinationPath);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Excel'e aktarılamadı: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Ekran açıkken başka bir kullanıcının eklediği/güncellediği sözleşmeleri
    // görebilmek için üstteki "Yenile" butonuna bağlanır. Bulunulan sayfayı korur.
    [RelayCommand]
    private async Task Refresh() => await LoadAsync();

    [RelayCommand]
    private async Task SetFilter(string filter)
    {
        SelectedFilter = filter;
        CurrentPage = 1;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ClearFilters()
    {
        // SearchText'i doğrudan atamak OnSearchTextChanged üzerinden ikinci bir yükleme
        // tetikleyeceği için, önce bekleyen debounce iptal edilir; tek bir yükleme yapılır.
        _searchDebounceCts?.Cancel();
        SelectedFilter = "tumu";
        SearchText = string.Empty;
        _searchDebounceCts?.Cancel();

        // SelectedType'ı doğrudan atamak OnSelectedTypeChanged üzerinden ikinci bir
        // yükleme tetikler; arama kutusundaki desenin aynısı.
        if (SelectedType != AllTypes)
        {
            SelectedType = AllTypes;
            return;
        }

        CurrentPage = 1;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task NextPage()
    {
        if (!CanGoNext) return;
        CurrentPage++;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task PreviousPage()
    {
        if (!CanGoPrevious) return;
        CurrentPage--;
        await LoadAsync();
    }

    private void DebouncedReloadFirstPage()
    {
        _searchDebounceCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchDebounceCts = cts;

        // Task.Run KULLANILMIYOR: bu metot UI thread'inde çağrıldığı için await sonrası
        // da UI thread'ine dönülür. Arka plana atılsaydı ObservableProperty'leri UI
        // thread'i dışından güncellemiş olurduk ve Avalonia hata fırlatırdı.
        _ = DelayThenReloadAsync(cts.Token);
    }

    private async Task DelayThenReloadAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(350, token);
        }
        catch (OperationCanceledException)
        {
            // Kullanıcı yazmaya devam etti; bu bekleme iptal edildi.
            return;
        }

        if (token.IsCancellationRequested) return;
        CurrentPage = 1;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var token = ++_loadToken;
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var (contracts, totalCount) = await _contractService.GetContractsPagedAsync(
                _currentUser, SelectedFilter, SearchText, CurrentPage, PageSize, TypeFilter);

            // Bu sorgu başlatıldıktan sonra yenisi başlatıldıysa sonucu yok say.
            if (token != _loadToken) return;

            TotalCount = totalCount;

            // Filtre daralıp sayfa sayısı azaldıysa geçerli sayfayı sınıra çek.
            if (CurrentPage > TotalPages) CurrentPage = TotalPages;

            FilteredContracts = new ObservableCollection<ContractCardViewModel>(
                contracts.Select(c => new ContractCardViewModel(c, _currentUser)));
        }
        catch (Exception ex)
        {
            if (token != _loadToken) return;
            ErrorMessage = "Sözleşmeler yüklenirken bir hata oluştu: " + ex.Message;
        }
        finally
        {
            if (token == _loadToken) IsLoading = false;
        }
    }
}