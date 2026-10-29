using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FlowBrowser;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private const string HomeUrl = "https://www.google.com/";
    private readonly string _appFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlowBrowser");
    private CoreWebView2Environment? _environment;
    private BrowserTab? _activeTab;
    private bool _started;

    public ObservableCollection<BrowserTab> Tabs { get; } = new();
    public ObservableCollection<BookmarkItem> Bookmarks { get; } = new();

    public BrowserTab? ActiveTab
    {
        get => _activeTab;
        set
        {
            if (ReferenceEquals(_activeTab, value)) return;
            if (_activeTab is not null) _activeTab.IsActive = false;
            _activeTab = value;
            if (_activeTab is not null) _activeTab.IsActive = true;
            OnPropertyChanged(nameof(ActiveTab));
            RefreshAddress();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private string DownloadsFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    private string BookmarksPath => Path.Combine(_appFolder, "bookmarks.json");
    private string HistoryPath => Path.Combine(_appFolder, "history.json");

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        Directory.CreateDirectory(_appFolder);
        Directory.CreateDirectory(DownloadsFolder);
        LoadBookmarks();
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
        if (MaximizeWindowButton is null) return;
        bool maximized = WindowState == WindowState.Maximized;
        MaximizeWindowButton.Content = maximized ? "❐" : "□";
        MaximizeWindowButton.ToolTip = maximized ? "Geri yükle" : "Büyüt";
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_started) return;
        _started = true;
        await AddTabAsync(HomeUrl);
    }

    private async Task AddTabAsync(string url)
    {
        try
        {
            var view = new WebView2();
            var tab = new BrowserTab(view);
            Tabs.Add(tab);
            ActiveTab = tab;

            _environment ??= await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: Path.Combine(_appFolder, "WebView2"));
            await view.EnsureCoreWebView2Async(_environment);

            var core = view.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreDevToolsEnabled = true;
            core.Settings.IsZoomControlEnabled = true;

            core.NavigationStarting += (_, args) =>
            {
                if (ActiveTab == tab)
                {
                    StatusText.Text = "Sayfa yükleniyor…";
                    if (Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri)) AddressBox.Text = uri.ToString();
                }
            };
            core.NavigationCompleted += (_, args) =>
            {
                RefreshTabTitle(tab);
                if (ActiveTab == tab) StatusText.Text = args.IsSuccess ? "Hazır" : "Sayfa yüklenemedi";
                if (args.IsSuccess && view.Source is not null) SaveHistory(tab.Title, view.Source.ToString());
                RefreshAddress();
            };
            core.DocumentTitleChanged += (_, _) => RefreshTabTitle(tab);
            core.NewWindowRequested += async (_, args) =>
            {
                args.Handled = true;
                if (Uri.TryCreate(args.Uri, UriKind.Absolute, out var target)) await AddTabAsync(target.ToString());
            };
            core.DownloadStarting += (_, args) =>
            {
                Directory.CreateDirectory(DownloadsFolder);
                var fileName = Path.GetFileName(args.ResultFilePath);
                if (string.IsNullOrWhiteSpace(fileName)) fileName = "download";
                args.ResultFilePath = Path.Combine(DownloadsFolder, fileName);
                StatusText.Text = "İndirme başlatıldı: " + fileName;
            };

            view.Source = new Uri(NormalizeAddress(url));
            RefreshAddress();
        }
        catch (Exception ex)
        {
            StatusText.Text = "WebView2 başlatılamadı";
            MessageBox.Show("FlowBrowser açılamadı. Microsoft Edge WebView2 Runtime kurulu olmalı.\n\nAyrıntı: " + ex.Message,
                "FlowBrowser", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static string NormalizeAddress(string input)
    {
        input = (input ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(input)) return HomeUrl;
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)) return uri.ToString();
        if (!input.Contains(' ') && input.Contains('.') && !input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !input.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return "https://" + input;
        return "https://www.google.com/search?q=" + Uri.EscapeDataString(input);
    }

    private void RefreshTabTitle(BrowserTab tab)
    {
        var title = tab.View.CoreWebView2?.DocumentTitle;
        if (string.IsNullOrWhiteSpace(title)) title = "Yeni sekme";
        tab.Title = title.Length > 32 ? title[..32] + "…" : title;
        if (ActiveTab == tab) Title = $"{tab.Title} — FlowBrowser";
    }

    private void RefreshAddress()
    {
        if (ActiveTab?.View.Source is Uri uri) AddressBox.Text = uri.ToString();
        else AddressBox.Text = string.Empty;
    }

    private void NavigateAddress()
    {
        var destination = NormalizeAddress(AddressBox.Text);
        if (ActiveTab?.View is not null && Uri.TryCreate(destination, UriKind.Absolute, out var uri))
            ActiveTab.View.Source = uri;
        else if (ActiveTab is null) _ = AddTabAsync(destination);
        Keyboard.ClearFocus();
    }

    private void AddressBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        NavigateAddress();
        e.Handled = true;
    }

    private void MoreMenu_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        void AddItem(string label, Action action)
        {
            var item = new MenuItem { Header = label, Padding = new Thickness(16, 8, 24, 8) };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }
        AddItem("Yeni sekme", () => _ = AddTabAsync(HomeUrl));
        AddItem("Ana sayfa", () => Home_Click(this, new RoutedEventArgs()));
        AddItem("Yer imlerine ekle", () => Bookmark_Click(this, new RoutedEventArgs()));
        AddItem("Geçmiş", () => History_Click(this, new RoutedEventArgs()));
        AddItem("İndirilenler", () => Downloads_Click(this, new RoutedEventArgs()));
        menu.Items.Add(new Separator());
        AddItem("Yakınlaştır", () => { if (ActiveTab?.View.CoreWebView2 is not null) ActiveTab.View.ZoomFactor = Math.Min(ActiveTab.View.ZoomFactor + 0.1, 3.0); });
        AddItem("Uzaklaştır", () => { if (ActiveTab?.View.CoreWebView2 is not null) ActiveTab.View.ZoomFactor = Math.Max(ActiveTab.View.ZoomFactor - 0.1, 0.25); });
        AddItem("Yakınlaştırmayı sıfırla", () => { if (ActiveTab?.View.CoreWebView2 is not null) ActiveTab.View.ZoomFactor = 1.0; });
        menu.PlacementTarget = sender as UIElement;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private async void NewTab_Click(object sender, RoutedEventArgs e) => await AddTabAsync(HomeUrl);
    private void SelectTab_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is BrowserTab tab) ActiveTab = tab;
    }

    private async void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not BrowserTab tab) return;
        var index = Tabs.IndexOf(tab);
        var wasActive = ActiveTab == tab;
        Tabs.Remove(tab);
        if (wasActive) ActiveTab = Tabs.Count == 0 ? null : Tabs[Math.Clamp(index - 1, 0, Tabs.Count - 1)];
        if (Tabs.Count == 0) await AddTabAsync(HomeUrl);
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveTab?.View.CoreWebView2?.CanGoBack == true) ActiveTab.View.GoBack();
    }
    private void Forward_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveTab?.View.CoreWebView2?.CanGoForward == true) ActiveTab.View.GoForward();
    }
    private void Reload_Click(object sender, RoutedEventArgs e) => ActiveTab?.View.Reload();
    private void Home_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveTab?.View is not null) ActiveTab.View.Source = new Uri(HomeUrl);
    }

    private void Bookmark_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveTab?.View.Source is not Uri uri) return;
        var url = uri.ToString();
        if (Bookmarks.Any(b => string.Equals(b.Url, url, StringComparison.OrdinalIgnoreCase)))
        { StatusText.Text = "Bu sayfa zaten yer imlerinde"; return; }
        var title = ActiveTab.View.CoreWebView2?.DocumentTitle;
        if (string.IsNullOrWhiteSpace(title)) title = uri.Host;
        Bookmarks.Insert(0, new BookmarkItem(title, url));
        SaveBookmarks();
        StatusText.Text = "Yer imi kaydedildi";
    }

    private void OpenBookmark_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is BookmarkItem bookmark && ActiveTab?.View is not null)
            ActiveTab.View.Source = new Uri(NormalizeAddress(bookmark.Url));
    }

    private void Downloads_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(DownloadsFolder);
            Process.Start(new ProcessStartInfo("explorer.exe", DownloadsFolder) { UseShellExecute = true });
        }
        catch (Exception ex) { StatusText.Text = "İndirilenler açılamadı: " + ex.Message; }
    }

    private void History_Click(object sender, RoutedEventArgs e)
    {
        var entries = LoadHistory().OrderByDescending(h => h.VisitedAt).Take(200).ToList();
        var list = new ListBox { Margin = new Thickness(12), BorderThickness = new Thickness(0) };
        foreach (var item in entries)
        {
            var row = new StackPanel { Margin = new Thickness(4, 7, 4, 7) };
            row.Children.Add(new TextBlock { Text = item.Title, FontWeight = FontWeights.SemiBold, FontSize = 13 });
            row.Children.Add(new TextBlock { Text = item.Url, Foreground = System.Windows.Media.Brushes.SlateGray, FontSize = 11 });
            row.Children.Add(new TextBlock { Text = item.VisitedAt.ToString("g"), Foreground = System.Windows.Media.Brushes.Gray, FontSize = 10 });
            list.Items.Add(new ListBoxItem { Content = row, Tag = item, Padding = new Thickness(6) });
        }
        var window = new Window { Title = "FlowBrowser — Geçmiş", Width = 720, Height = 520, Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = System.Windows.Media.Brushes.White, Content = list };
        list.MouseDoubleClick += (_, _) =>
        {
            if (list.SelectedItem is ListBoxItem selected && selected.Tag is HistoryItem history)
            { if (ActiveTab?.View is not null) ActiveTab.View.Source = new Uri(NormalizeAddress(history.Url)); window.Close(); }
        };
        window.ShowDialog();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (e.Key == Key.L) { AddressBox.Focus(); AddressBox.SelectAll(); e.Handled = true; }
            else if (e.Key == Key.T) { _ = AddTabAsync(HomeUrl); e.Handled = true; }
            else if (e.Key == Key.W) { if (ActiveTab is not null) CloseActiveTab(); e.Handled = true; }
            else if (e.Key == Key.R) { ActiveTab?.View.Reload(); e.Handled = true; }
        }
        else if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.Left) { Back_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.Right) { Forward_Click(this, new RoutedEventArgs()); e.Handled = true; }
    }

    private async void CloseActiveTab()
    {
        if (ActiveTab is null) return;
        var tab = ActiveTab;
        var index = Tabs.IndexOf(tab);
        Tabs.Remove(tab);
        ActiveTab = Tabs.Count == 0 ? null : Tabs[Math.Clamp(index - 1, 0, Tabs.Count - 1)];
        if (Tabs.Count == 0) await AddTabAsync(HomeUrl);
    }

    private void LoadBookmarks()
    {
        try
        {
            if (!File.Exists(BookmarksPath)) return;
            var entries = JsonSerializer.Deserialize<List<BookmarkItem>>(File.ReadAllText(BookmarksPath));
            if (entries is null) return;
            foreach (var bookmark in entries) Bookmarks.Add(bookmark);
        }
        catch { /* Ignore a damaged local settings file. */ }
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
            var history = LoadHistory();
            history.Add(new HistoryItem(title, url, DateTime.Now));
            if (history.Count > 500) history = history.OrderByDescending(h => h.VisitedAt).Take(500).ToList();
            File.WriteAllText(HistoryPath, JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* Browsing should continue if history cannot be saved. */ }
    }

    private List<HistoryItem> LoadHistory()
    {
        try
        {
            if (!File.Exists(HistoryPath)) return new List<HistoryItem>();
            return JsonSerializer.Deserialize<List<HistoryItem>>(File.ReadAllText(HistoryPath)) ?? new List<HistoryItem>();
        }
        catch { return new List<HistoryItem>(); }
    }

    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
