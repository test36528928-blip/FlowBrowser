using Microsoft.Web.WebView2.Wpf;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FlowBrowser;

public sealed class BrowserTab : INotifyPropertyChanged
{
    private string _title = "Yeni sekme";
    private bool _isActive;

    public BrowserTab(WebView2 view) => View = view;

    public WebView2 View { get; }

    public string Title
    {
        get => _title;
        set { if (_title == value) return; _title = value; OnPropertyChanged(); }
    }

    public bool IsActive
    {
        get => _isActive;
        set { if (_isActive == value) return; _isActive = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed record BookmarkItem(string Title, string Url);
public sealed record HistoryItem(string Title, string Url, DateTime VisitedAt);
