using Core.Models;
using GersangStation.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Threading.Tasks;
using Windows.System;

namespace GersangStation.Controls;

public sealed partial class FavoriteItemView : UserControl
{
    private bool _editing;
    private bool _committing;
    private bool _isComposing;
    internal bool IsEditing => _editing;
    public BrowserFavorite Favorite { get; private set; } = new();
    internal Func<string, Task<bool>>? SaveNameAsync { get; set; }
    public event EventHandler? NavigateRequested;
    public event EventHandler? DeleteRequested;

    public FavoriteItemView()
    {
        InitializeComponent();
        NameEditor.TextCompositionStarted += (_, _) => _isComposing = true;
        NameEditor.TextCompositionEnded += (_, _) => _isComposing = false;
    }

    internal void Initialize(BrowserFavorite favorite)
    {
        Favorite = favorite;
        RefreshDisplay();
    }

    internal void RefreshDisplay()
    {
        NameText.Text = Favorite.Name;
        AutomationProperties.SetName(LinkButton, Favorite.Name);
        ToolTipService.SetToolTip(LinkButton, Favorite.Url);
        FaviconImage.Source = Uri.TryCreate(Favorite.DisplayFaviconUrl, UriKind.Absolute, out var uri)
            ? new BitmapImage(uri) : null;
    }

    internal void BeginRename()
    {
        if (_committing || !IsLoaded)
            return;
        if (!_editing)
            NameEditor.Text = Favorite.Name;
        _editing = true;
        NameEditor.Visibility = Visibility.Visible;
        NameText.Visibility = Visibility.Collapsed;
        NameEditor.StartBringIntoView();
        NameEditor.Focus(FocusState.Programmatic);
        NameEditor.SelectAll();
    }

    internal void CancelRename()
    {
        if (!_committing)
            EndRename(focusLink: false);
    }

    internal Task CommitRenameOnOutsidePointerAsync() => CommitRenameAsync(focusLink: false);

    private void EndRename(bool focusLink)
    {
        bool hadFocus = NameEditor.FocusState != FocusState.Unfocused;
        _editing = false;
        RefreshDisplay();
        NameEditor.Visibility = Visibility.Collapsed;
        NameText.Visibility = Visibility.Visible;
        if (focusLink && hadFocus && IsLoaded)
            LinkButton.Focus(FocusState.Programmatic);
    }

    private async Task CommitRenameAsync(bool focusLink)
    {
        if (!_editing || _committing)
            return;
        string name = NameEditor.Text.Trim();
        if (name.Length == 0 || name == Favorite.Name)
        {
            EndRename(focusLink);
            return;
        }
        _committing = true;
        NameEditor.IsReadOnly = true;
        try
        {
            if (SaveNameAsync is not null && await SaveNameAsync(name))
                EndRename(focusLink);
        }
        finally
        {
            NameEditor.IsReadOnly = false;
            _committing = false;
        }
    }

    private void LinkButton_Click(object sender, RoutedEventArgs e) => NavigateRequested?.Invoke(this, EventArgs.Empty);
    private void Delete_Click(object sender, RoutedEventArgs e) => DeleteRequested?.Invoke(this, EventArgs.Empty);
    private void Rename_Click(object sender, RoutedEventArgs e)
        => DispatcherQueue.TryEnqueueHandled(BeginRename, "FavoriteItemView.BeginRenameAfterFlyout");
    private void LinkButton_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.F2) { e.Handled = true; BeginRename(); }
    }
    private async void NameEditor_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        // 한글 등 IME 조합 확정 키를 이름 저장/취소로 가로채지 않습니다.
        if (_isComposing)
            return;
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            await SafeExecution.RunHandledAsync(() => CommitRenameAsync(focusLink: true), "FavoriteItemView.CommitName");
        }
        else if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            if (!_committing) EndRename(focusLink: true);
        }
    }
    private async void NameEditor_LostFocus(object sender, RoutedEventArgs e)
        => await SafeExecution.RunHandledAsync(() => CommitRenameAsync(focusLink: false), "FavoriteItemView.CommitNameOnBlur");
}
