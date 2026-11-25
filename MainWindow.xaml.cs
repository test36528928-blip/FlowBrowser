using CefSharp;
using CefSharp.Wpf;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using Microsoft.Win32;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FlowBrowser;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private const string HomeUrl = "https://www.google.com/";
    private readonly string _appFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlowBrowser");
    private BrowserTab? _activeTab;
    private bool _started;
    private string _searchEngine = "Google";
    private bool _darkTheme;
    public ObservableCollection<BrowserTab> Tabs { get; } = new();
    public ObservableCollection<BookmarkItem> Bookmarks { get; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;
    private string SettingsPath => Path.Combine(_appFolder, "settings.json");
    private string BookmarksPath => Path.Combine(_appFolder, "bookmarks.json");
    private string HistoryPath => Path.Combine(_appFolder, "history.json");
    private string ExtensionsPath => Path.Combine(_appFolder, "Extensions");
    private string ImportedExtensionFilesPath => Path.Combine(_appFolder, "ImportedExtensionFiles");
    private static readonly Dictionary<string, string> InternalPages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["privacy"]="Gizlilik", ["downloads"]="İndirilenler", ["about"]="FlowBrowser hakkında",
        ["help"]="Yardım", ["security"]="Güvenlik", ["permissions"]="Site izinleri",
        ["appearance"]="Görünüm", ["search"]="Arama motoru", ["startup"]="Başlangıç",
        ["tabs"]="Sekmeler", ["languages"]="Diller", ["accessibility"]="Erişilebilirlik",
        ["system"]="Sistem", ["reset"]="Ayarları sıfırla", ["shortcuts"]="Klavye kısayolları",
        ["passwords"]="Parolalar", ["autofill"]="Otomatik doldurma", ["site-data"]="Site verileri",
        ["cookies"]="Çerezler", ["cache"]="Önbellek", ["notifications"]="Bildirimler",
        ["popups"]="Açılır pencereler", ["camera"]="Kamera izinleri", ["microphone"]="Mikrofon izinleri",
        ["location"]="Konum izinleri", ["protocols"]="Protokol işleyicileri", ["experiments"]="Deneysel özellikler",
        ["diagnostics"]="Tanılama", ["license"]="Lisanslar", ["update"]="Güncellemeler"
    };

    public BrowserTab? ActiveTab
    {
        get => _activeTab;
        set
        {
            if (ReferenceEquals(_activeTab, value)) return;
            if (_activeTab != null) _activeTab.IsActive = false;
            _activeTab = value;
            if (_activeTab != null) _activeTab.IsActive = true;
            OnPropertyChanged(nameof(ActiveTab));
            RefreshAddress();
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        Directory.CreateDirectory(_appFolder);
        LoadBookmarks();
        LoadSettings();
    }

    private void MinimizeWindow_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeRestoreWindow_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        UpdateWindowControlState();
    }
    private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_StateChanged(object? sender, EventArgs e) => UpdateWindowControlState();
    private void UpdateWindowControlState()
    {
        if (MaximizeWindowButton == null) return;
        bool maximized = WindowState == WindowState.Maximized;
        MaximizeWindowButton.Content = maximized ? "❐" : "□";
        MaximizeWindowButton.ToolTip = maximized ? "Geri yükle" : "Büyüt";
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_started) return;
        _started = true;
        await AddTabAsync("flowbrowser://newtab");
    }

    private Task AddTabAsync(string address)
    {
        try
        {
            var browser = new ChromiumWebBrowser();
            var tab = new BrowserTab(browser);
            Tabs.Add(tab);
            ActiveTab = tab;

            browser.AddressChanged += (_, args) => Dispatcher.Invoke(() =>
            {
                if (browser.Address.StartsWith("https://flowbrowser.local/", StringComparison.OrdinalIgnoreCase))
                {
                    var route = browser.Address.Substring("https://flowbrowser.local/".Length).Trim('/');
                    NavigateInternal(tab, route);
                    return;
                }
                tab.CurrentAddress = browser.Address;
                if (ActiveTab == tab) AddressBox.Text = browser.Address;
            });
            browser.TitleChanged += (_, args) => Dispatcher.Invoke(() =>
            {
                tab.Title = string.IsNullOrWhiteSpace(browser.Title) ? "Yeni sekme" :
                    (browser.Title.Length > 32 ? browser.Title.Substring(0, 32) + "…" : browser.Title);
                if (ActiveTab == tab) Title = tab.Title + " — FlowBrowser";
            });
            browser.LoadingStateChanged += (_, args) => Dispatcher.Invoke(() =>
            {
                if (ActiveTab == tab) StatusText.Text = args.IsLoading ? "Sayfa yükleniyor…" : "Hazır";
                if (!args.IsLoading && tab.CurrentAddress.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    SaveHistory(tab.Title, tab.CurrentAddress);
            });
            browser.PreviewMouseRightButtonUp += (_, args) =>
            {
                var menu = new ContextMenu();
                AddContextItem(menu, "Geri", () => browser.Back());
                AddContextItem(menu, "İleri", () => browser.Forward());
                AddContextItem(menu, "Yenile", () => browser.Reload());
                menu.Items.Add(new Separator());
                AddContextItem(menu, "Yeni sekme", () => AddTabAsync("flowbrowser://newtab"));
                AddContextItem(menu, "Yer imlerine ekle", () => Bookmark_Click(this, new RoutedEventArgs()));
                menu.PlacementTarget = browser;
                menu.IsOpen = true;
                args.Handled = true;
            };

            Navigate(tab, address);
        }
        catch (Exception ex)
        {
            StatusText.Text = "CEF başlatılamadı";
            MessageBox.Show("FlowBrowser CEF motorunu başlatamadı.\n\n" + ex.Message, "FlowBrowser",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        return Task.CompletedTask;
    }

    private static void AddContextItem(ContextMenu menu, string label, Action action)
    {
        var item = new MenuItem { Header = label };
        item.Click += (_, _) => action();
        menu.Items.Add(item);
    }

    private void Navigate(BrowserTab tab, string address)
    {
        if (address.StartsWith("flowbrowser://", StringComparison.OrdinalIgnoreCase))
        {
            NavigateInternal(tab, address.Substring("flowbrowser://".Length).Trim('/'));
            return;
        }
        tab.CurrentAddress = address;
        tab.View.Load(address);
    }

    private void NavigateInternal(BrowserTab tab, string route)
    {
        route = string.IsNullOrWhiteSpace(route) ? "newtab" : route.ToLowerInvariant();
        if (route == "open-settings")
        {
            Dispatcher.Invoke(() => Settings_Click(this, new RoutedEventArgs()));
            tab.CurrentAddress = "flowbrowser://settings";
            return;
        }
        if (route == "import-extension")
        {
            Dispatcher.Invoke(ImportExtensionPackage);
            tab.CurrentAddress = "flowbrowser://extensions";
            return;
        }
        string title;
        string body;
        if (route == "settings")
        {
            title = "Ayarlar";
            body = "<p>FlowBrowser ayarları</p><p>Arama motoru ve tema tercihlerini düzenlemek için aşağıdaki düğmeyi kullanın.</p><button onclick=\"location.href='https://flowbrowser.local/open-settings'\">Ayarları düzenle</button>";
        }
        else if (route == "history")
        {
            title = "Geçmiş";
            var rows = LoadHistory().OrderByDescending(x => x.VisitedAt).Take(100)
                .Select(x => "<li><b>" + System.Net.WebUtility.HtmlEncode(x.Title) + "</b><br>" +
                    System.Net.WebUtility.HtmlEncode(x.Url) + "</li>");
            body = "<ul>" + string.Join("", rows) + "</ul>";
        }
        else if (route == "bookmarks")
        {
            title = "Yer imleri";
            var rows = Bookmarks.Select(x => "<li>" + System.Net.WebUtility.HtmlEncode(x.Title) +
                " — " + System.Net.WebUtility.HtmlEncode(x.Url) + "</li>");
            body = "<ul>" + string.Join("", rows) + "</ul>";
        }
        else if (route == "extensions")
        {
            title = "Uzantılar";
            body = BuildExtensionsHtml();
        }
        else if (InternalPages.TryGetValue(route, out var pageTitle))
        {
            title = pageTitle;
            body = "<p>Bu FlowBrowser iç sayfası v2.0.0 arayüzünde hazırlandı.</p>" +
                "<p>Bu bölümün gelişmiş işlevleri henüz etkin değildir.</p>" +
                "<p><a href='https://flowbrowser.local/settings'>Ayarlar</a> · <a href='https://flowbrowser.local/extensions'>Uzantılar</a></p>";
        }
        else
        {
            title = "Yeni sekme";
            body = "<p>FlowBrowser ile web'de arama yapın veya adres girin.</p>" +
                "<form onsubmit=\"location.href='https://www.google.com/search?q='+encodeURIComponent(q.value);return false\">" +
                "<input id='q' autofocus placeholder='Google’da ara veya adres yaz'><button>Ara</button></form>" +
                "<nav><a href='https://flowbrowser.local/settings'>Ayarlar</a> · " +
                "<a href='https://flowbrowser.local/history'>Geçmiş</a> · " +
                "<a href='https://flowbrowser.local/bookmarks'>Yer imleri</a> · " +
                "<a href='https://flowbrowser.local/extensions'>Uzantılar</a></nav>" +
                "<h2>FlowBrowser sayfaları</h2><ul>" +
                string.Join("", InternalPages.Select(x => "<li><a href='https://flowbrowser.local/" + x.Key + "'>" +
                    System.Net.WebUtility.HtmlEncode(x.Value) + "</a></li>")) + "</ul>";
        }
        tab.Title = title;
        tab.CurrentAddress = "flowbrowser://" + route;
        string html = "<!doctype html><html lang='tr'><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'>" +
            "<title>" + title + "</title><style>body{font:16px Segoe UI,Arial;background:#f2f5fa;color:#172033;margin:8vh auto;max-width:760px;padding:32px}" +
            "h1{font-size:34px}a,button{color:#1769df;margin:8px;padding:10px;border:0;border-radius:8px;background:#e4edfc;text-decoration:none;cursor:pointer}" +
            "input{padding:14px;width:70%;border:1px solid #ccd5e2;border-radius:10px}</style><h1>" + title + "</h1>" +
            body + "</html>";
        tab.View.LoadHtml(html, "https://flowbrowser-content.local/");
        if (ActiveTab == tab)
        {
            AddressBox.Text = tab.CurrentAddress;
            Title = title + " — FlowBrowser";
        }
    }

    private string NormalizeAddress(string input)
    {
        input = (input ?? "").Trim();
        if (string.IsNullOrWhiteSpace(input)) return "flowbrowser://newtab";
        if (input.StartsWith("flowbrowser://", StringComparison.OrdinalIgnoreCase)) return input;
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri) &&
            (uri.Scheme == "http" || uri.Scheme == "https")) return uri.ToString();
        if (!input.Contains(' ') && input.Contains('.') &&
            !input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !input.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return "https://" + input;
        string q = Uri.EscapeDataString(input);
        if (_searchEngine == "Bing") return "https://www.bing.com/search?q=" + q;
        if (_searchEngine == "DuckDuckGo") return "https://duckduckgo.com/?q=" + q;
        return "https://www.google.com/search?q=" + q;
    }

    private void RefreshAddress()
    {
        AddressBox.Text = ActiveTab?.CurrentAddress ?? "";
        if (ActiveTab != null) Title = ActiveTab.Title + " — FlowBrowser";
    }
    private void NavigateAddress()
    {
        string address = NormalizeAddress(AddressBox.Text);
        if (ActiveTab != null) Navigate(ActiveTab, address);
        else _ = AddTabAsync(address);
        Keyboard.ClearFocus();
    }
    private void AddressBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { NavigateAddress(); e.Handled = true; }
    }

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var settings = JsonSerializer.Deserialize<BrowserSettings>(File.ReadAllText(SettingsPath));
            if (settings == null) return;
            _searchEngine = settings.SearchEngine is "Bing" or "DuckDuckGo" ? settings.SearchEngine : "Google";
            _darkTheme = settings.DarkTheme;
            ApplyTheme();
        }
        catch { }
    }
    private void SaveSettings()
    {
        try { File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new BrowserSettings(_searchEngine, _darkTheme))); }
        catch (Exception ex) { StatusText.Text = "Ayarlar kaydedilemedi: " + ex.Message; }
    }
    private void ApplyTheme()
    {
        var converter = new System.Windows.Media.BrushConverter();
        Resources["PageBrush"] = converter.ConvertFromString(_darkTheme ? "#171B24" : "#FFFFFF")!;
        Resources["SurfaceBrush"] = converter.ConvertFromString(_darkTheme ? "#202633" : "#FFFFFF")!;
        Resources["LineBrush"] = converter.ConvertFromString(_darkTheme ? "#354052" : "#DCE4EF")!;
        Resources["MutedBrush"] = converter.ConvertFromString(_darkTheme ? "#AAB6C8" : "#68778D")!;
        Background = (System.Windows.Media.Brush)Resources["PageBrush"];
    }
    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock { Text = "Varsayılan arama motoru" });
        var engine = new ComboBox { ItemsSource = new[] { "Google", "Bing", "DuckDuckGo" }, SelectedItem = _searchEngine, Padding = new Thickness(8) };
        panel.Children.Add(engine);
        panel.Children.Add(new TextBlock { Text = "Tema", Margin = new Thickness(0, 18, 0, 8) });
        var theme = new ComboBox { ItemsSource = new[] { "Açık tema", "Koyu tema" }, SelectedIndex = _darkTheme ? 1 : 0, Padding = new Thickness(8) };
        panel.Children.Add(theme);
        var save = new Button { Content = "Kaydet", Margin = new Thickness(0, 20, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
        var win = new Window { Title = "FlowBrowser — Ayarlar", Width = 380, Height = 280, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = panel };
        save.Click += (_, _) => { _searchEngine = engine.SelectedItem?.ToString() ?? "Google"; _darkTheme = theme.SelectedIndex == 1; SaveSettings(); ApplyTheme(); win.Close(); };
        panel.Children.Add(save);
        win.ShowDialog();
    }

    private void MoreMenu_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        AddContextItem(menu, "Yeni sekme", () => _ = AddTabAsync("flowbrowser://newtab"));
        AddContextItem(menu, "Geçmiş", () => Navigate(ActiveTab!, "flowbrowser://history"));
        AddContextItem(menu, "Yer imleri", () => Navigate(ActiveTab!, "flowbrowser://bookmarks"));
        AddContextItem(menu, "İndirilenler klasörü", () => Downloads_Click(this, new RoutedEventArgs()));
        AddContextItem(menu, "Ayarlar", () => Navigate(ActiveTab!, "flowbrowser://settings"));
        AddContextItem(menu, "Uzantılar", () => Navigate(ActiveTab!, "flowbrowser://extensions"));
        menu.PlacementTarget = sender as UIElement;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }


    private string BuildExtensionsHtml()
    {
        Directory.CreateDirectory(ExtensionsPath);
        Directory.CreateDirectory(ImportedExtensionFilesPath);
        var rows = new List<string>();
        foreach (var dir in Directory.GetDirectories(ExtensionsPath))
        {
            string name = Path.GetFileName(dir);
            string state = "Manifest bulunamadı";
            string manifest = Path.Combine(dir, "manifest.json");
            if (File.Exists(manifest))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(manifest));
                    var root = doc.RootElement;
                    if (root.TryGetProperty("name", out var n)) name = n.GetString() ?? name;
                    string version = root.TryGetProperty("version", out var v) ? v.GetString() ?? "?" : "?";
                    state = "Paket içe aktarıldı · v" + version + " · CEF'te etkinleştirme garanti edilmez";
                }
                catch { state = "manifest.json okunamadı"; }
            }
            rows.Add("<li><b>" + System.Net.WebUtility.HtmlEncode(name) + "</b><br>" + System.Net.WebUtility.HtmlEncode(state) + "</li>");
        }
        foreach (var file in Directory.GetFiles(ImportedExtensionFilesPath))
            rows.Add("<li><b>" + System.Net.WebUtility.HtmlEncode(Path.GetFileName(file)) + "</b><br>Dosya arşivlendi; uzantı olarak çalıştırılmıyor</li>");
        return "<p>ZIP paketleri açılarak saklanır; CRX ve diğer dosyalar arşivlenir.</p>" +
            "<p>CEF/CefSharp, Chrome Web Store uyumluluğunu veya uzantıların çalışmasını garanti etmez.</p>" +
            "<p><a href='https://flowbrowser.local/import-extension'>＋ Uzantı dosyası / ZIP içe aktar</a></p>" +
            (rows.Count == 0 ? "<p>Henüz uzantı eklenmedi.</p>" : "<ul>" + string.Join("", rows) + "</ul>");
    }

    private void ImportExtensionPackage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Uzantı paketi seç",
            Filter = "Uzantı paketleri (*.zip;*.crx)|*.zip;*.crx|Tüm dosyalar (*.*)|*.*",
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            Directory.CreateDirectory(ExtensionsPath);
            Directory.CreateDirectory(ImportedExtensionFilesPath);
            string ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();
            string safeName = Path.GetFileNameWithoutExtension(dialog.FileName);
            foreach (char ch in Path.GetInvalidFileNameChars()) safeName = safeName.Replace(ch, '_');
            string unique = safeName + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            if (ext == ".zip")
            {
                string dest = Path.Combine(ExtensionsPath, unique);
                ZipFile.ExtractToDirectory(dialog.FileName, dest);
                StatusText.Text = File.Exists(Path.Combine(dest, "manifest.json"))
                    ? "ZIP içe aktarıldı; CEF üzerinde çalışması garanti edilmez."
                    : "ZIP arşivlendi; manifest.json bulunamadı.";
            }
            else
            {
                File.Copy(dialog.FileName, Path.Combine(ImportedExtensionFilesPath, unique + ext), false);
                StatusText.Text = "Dosya arşivlendi; CRX etkinleştirilmiyor.";
            }
            if (ActiveTab != null) Navigate(ActiveTab, "flowbrowser://extensions");
        }
        catch (Exception ex)
        {
            MessageBox.Show("Uzantı paketi içe aktarılamadı:\n" + ex.Message, "FlowBrowser",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void NewTab_Click(object sender, RoutedEventArgs e) => await AddTabAsync("flowbrowser://newtab");
    private void SelectTab_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is BrowserTab tab) ActiveTab = tab;
    }
    private async void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not BrowserTab tab) return;
        int index = Tabs.IndexOf(tab);
        bool active = ActiveTab == tab;
        Tabs.Remove(tab);
        if (active) ActiveTab = Tabs.Count == 0 ? null : Tabs[Math.Clamp(index - 1, 0, Tabs.Count - 1)];
        if (Tabs.Count == 0) await AddTabAsync("flowbrowser://newtab");
    }
    private void Back_Click(object sender, RoutedEventArgs e) { if (ActiveTab?.View.CanGoBack == true) ActiveTab.View.Back(); }
    private void Forward_Click(object sender, RoutedEventArgs e) { if (ActiveTab?.View.CanGoForward == true) ActiveTab.View.Forward(); }
    private void Reload_Click(object sender, RoutedEventArgs e) => ActiveTab?.View.Reload();
    private void Home_Click(object sender, RoutedEventArgs e) { if (ActiveTab != null) Navigate(ActiveTab, "flowbrowser://newtab"); }

    private void Bookmark_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveTab == null || !Uri.TryCreate(ActiveTab.CurrentAddress, UriKind.Absolute, out var uri)) return;
        string url = ActiveTab.CurrentAddress;
        if (Bookmarks.Any(x => string.Equals(x.Url, url, StringComparison.OrdinalIgnoreCase)))
        { StatusText.Text = "Bu sayfa zaten yer imlerinde"; return; }
        string title = string.IsNullOrWhiteSpace(ActiveTab.Title) ? uri.Host : ActiveTab.Title;
        Bookmarks.Insert(0, new BookmarkItem(title, url));
        SaveBookmarks();
        StatusText.Text = "Yer imi kaydedildi";
    }
    private void OpenBookmark_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is BookmarkItem bookmark && ActiveTab != null)
            Navigate(ActiveTab, NormalizeAddress(bookmark.Url));
    }

    private void BookmarksManager_Click(object sender, RoutedEventArgs e) => Navigate(ActiveTab!, "flowbrowser://bookmarks");
    private void History_Click(object sender, RoutedEventArgs e) => Navigate(ActiveTab!, "flowbrowser://history");
    private void Downloads_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")) { UseShellExecute = true });
    }
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (e.Key == Key.L) { AddressBox.Focus(); AddressBox.SelectAll(); e.Handled = true; }
            else if (e.Key == Key.T) { _ = AddTabAsync("flowbrowser://newtab"); e.Handled = true; }
            else if (e.Key == Key.W) { if (ActiveTab != null) CloseActiveTab(); e.Handled = true; }
            else if (e.Key == Key.R) { ActiveTab?.View.Reload(); e.Handled = true; }
        }
        else if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.Left) { Back_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.Right) { Forward_Click(this, new RoutedEventArgs()); e.Handled = true; }
    }
    private async void CloseActiveTab()
    {
        if (ActiveTab == null) return;
        var tab = ActiveTab;
        int index = Tabs.IndexOf(tab);
        Tabs.Remove(tab);
        ActiveTab = Tabs.Count == 0 ? null : Tabs[Math.Clamp(index - 1, 0, Tabs.Count - 1)];
        if (Tabs.Count == 0) await AddTabAsync("flowbrowser://newtab");
    }
    private void LoadBookmarks()
    {
        try
        {
            if (!File.Exists(BookmarksPath)) return;
            var entries = JsonSerializer.Deserialize<List<BookmarkItem>>(File.ReadAllText(BookmarksPath));
            if (entries != null) foreach (var item in entries) Bookmarks.Add(item);
        }
        catch { }
    }
    private void SaveBookmarks()
    {
        try { File.WriteAllText(BookmarksPath, JsonSerializer.Serialize(Bookmarks.ToList(), new JsonSerializerOptions { WriteIndented = true })); }
        catch (Exception ex) { StatusText.Text = "Yer imleri kaydedilemedi: " + ex.Message; }
    }
    private void SaveHistory(string title, string url)
    {
        try
        {
            var entries = LoadHistory();
            if (entries.Count == 0 || entries[^1].Url != url) entries.Add(new HistoryItem(title, url, DateTime.Now));
            if (entries.Count > 500) entries = entries.OrderByDescending(x => x.VisitedAt).Take(500).ToList();
            File.WriteAllText(HistoryPath, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
    private List<HistoryItem> LoadHistory()
    {
        try { return File.Exists(HistoryPath) ? JsonSerializer.Deserialize<List<HistoryItem>>(File.ReadAllText(HistoryPath)) ?? new() : new(); }
        catch { return new(); }
    }
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed record BrowserSettings(string SearchEngine, bool DarkTheme);
