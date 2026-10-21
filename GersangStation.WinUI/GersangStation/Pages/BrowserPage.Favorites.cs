using Core;
using Core.Models;
using GersangStation.Controls;
using GersangStation.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GersangStation.Main;

public sealed partial class WebViewPage
{
    private readonly SemaphoreSlim _favoriteMutationGate = new(1, 1);
    private readonly HashSet<FavoriteItemView> _favoriteViews = [];
    private bool _favoritesLoaded;
    private string? _pendingFavoriteEditUrl;

    private async void BrowserChrome_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        // TextBox/파비콘/컨텍스트 메뉴 동작은 기존 포커스 처리에 맡깁니다.
        // 빈 공간은 포커스를 받지 않아 LostFocus가 없으므로 클릭으로 직접 확정합니다.
        for (DependencyObject? node = e.OriginalSource as DependencyObject;
             node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is FavoriteItemView)
                return;
            if (ReferenceEquals(node, BrowserChrome))
                break;
        }
        await SafeExecution.RunHandledAsync(async () =>
        {
            foreach (var view in _favoriteViews.Where(view => view.IsEditing).ToArray())
                await view.CommitRenameOnOutsidePointerAsync();
        }, "BrowserPage.CommitFavoriteOnOutsidePointer");
        // Handled를 바꾸지 않아 스크롤/버튼/주소 입력의 원래 동작도 계속됩니다.
    }

    private async Task ToggleCurrentFavoriteAsync()
    {
        Uri? uri = GetCurrentPageUri();
        if (uri is null)
            return;
        string suggestedName = GetSuggestedFavoriteName(uri);
        await _favoriteMutationGate.WaitAsync();
        try
        {
            if (!CanChangeFavorites())
                return;
            BrowserFavorite? existing = FindFavoriteByUrl(uri.AbsoluteUri);
            var candidate = SnapshotFavorites();
            if (existing is not null)
            {
                candidate.RemoveAll(favorite => string.Equals(favorite.Url, uri.AbsoluteUri, StringComparison.OrdinalIgnoreCase));
                if (await PersistFavoritesAsync(candidate))
                {
                    Favorites.Remove(existing);
                    await DeleteFavoriteFaviconFileIfNeededAsync(existing);
                }
                return;
            }
            string icon = GetCurrentPageUri() == uri ? await CaptureCurrentPageFaviconAsync(uri) : string.Empty;
            var added = new BrowserFavorite(suggestedName, uri.AbsoluteUri, icon);
            candidate.Add(added);
            if (await PersistFavoritesAsync(candidate))
            {
                _pendingFavoriteEditUrl = added.Url;
                Favorites.Add(added);
            }
        }
        finally { _favoriteMutationGate.Release(); }
    }

    private bool CanChangeFavorites()
    {
        if (_favoritesLoaded)
            return true;
        if (!FavoriteRecoveryInfoBar.IsOpen)
            ShowFavoriteFailure("즐겨찾기 목록을 정상적으로 불러오지 못해 변경하지 않았습니다. 불러오기 실패 안내의 ‘다시 불러오기’를 눌러 주세요.");
        return false;
    }

    private async Task<bool> RenameFavoriteAsync(BrowserFavorite favorite, string name)
    {
        await _favoriteMutationGate.WaitAsync();
        try
        {
            if (!CanChangeFavorites())
                return false;
            var current = FindFavoriteByUrl(favorite.Url);
            if (current is null)
                return true; // 편집 중 삭제된 항목을 다시 생성하지 않습니다.
            var candidate = SnapshotFavorites();
            int index = Favorites.IndexOf(current);
            candidate[index].Name = name;
            if (!await PersistFavoritesAsync(candidate))
                return false;
            current.Name = name;
            return true;
        }
        finally { _favoriteMutationGate.Release(); }
    }

    private async Task DeleteFavoriteAsync(BrowserFavorite favorite)
    {
        await _favoriteMutationGate.WaitAsync();
        try
        {
            if (!CanChangeFavorites())
                return;
            var current = FindFavoriteByUrl(favorite.Url);
            if (current is null)
                return;
            var candidate = SnapshotFavorites();
            candidate.RemoveAt(Favorites.IndexOf(current));
            if (await PersistFavoritesAsync(candidate))
            {
                Favorites.Remove(current);
                await DeleteFavoriteFaviconFileIfNeededAsync(current);
            }
        }
        finally { _favoriteMutationGate.Release(); }
    }

    private async Task<bool> PersistFavoritesAsync(IList<BrowserFavorite> candidate, bool quiet = false)
    {
        AppDataManager.AppDataOperationResult result;
        try
        {
            result = await AppDataManager.SaveBrowserFavoritesAsync(candidate);
        }
        catch (Exception ex)
        {
            ReportFavoriteSaveFailure(ex, quiet);
            return false;
        }
        if (!result.Success)
        {
            ReportFavoriteSaveFailure(new InvalidOperationException(
                $"즐겨찾기 저장 실패: {result.Operation}, {result.Target}, {result.ErrorKind}", result.Exception), quiet);
            return false;
        }
        if (!quiet)
        {
            try { FavoriteOperationInfoBar.IsOpen = false; }
            catch (Exception ex) { App.ExceptionHandler.RecordRecoverable(ex, "BrowserPage.ClearFavoriteFeedback"); }
        }
        return true;
    }

    private void ReportFavoriteSaveFailure(Exception exception, bool quiet)
    {
        App.ExceptionHandler.RecordRecoverable(exception, "BrowserPage.SaveFavorites");
        if (!quiet)
            ShowFavoriteFailure("즐겨찾기를 저장하지 못해 기존 상태를 유지했습니다. 이름 편집 중이었다면 입력은 유지됩니다. Enter로 다시 저장하거나 Esc로 취소해 주세요. 파일 사용 여부와 저장 권한도 확인해 주세요.");
    }

    private void ShowFavoriteFailure(string message)
    {
        if (!IsLoaded)
            return;
        try
        {
            FavoriteOperationInfoBar.Message = message;
            FavoriteOperationInfoBar.IsOpen = true;
        }
        catch (Exception ex) { App.ExceptionHandler.RecordRecoverable(ex, "BrowserPage.ShowFavoriteFeedback"); }
    }

    private void FavoriteView_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FavoriteItemView view || view.Tag is not BrowserFavorite favorite)
            return;
        _favoriteViews.Add(view);
        view.Initialize(favorite);
        view.SaveNameAsync = name => RenameFavoriteAsync(view.Favorite, name);
        if (_pendingFavoriteEditUrl == favorite.Url)
        {
            _pendingFavoriteEditUrl = null;
            view.DispatcherQueue.TryEnqueueHandled(() => view.BeginRename(), "BrowserPage.BeginFavoriteRename");
        }
    }

    private void FavoriteView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FavoriteItemView view)
            _favoriteViews.Remove(view);
    }

    private void FavoriteView_NavigateRequested(object? sender, EventArgs e)
    {
        if (sender is FavoriteItemView view && TryCreateBrowsableUri(view.Favorite.Url, out var uri))
        {
            _webviewManager?.Navigate(uri!);
            WebView.Focus(FocusState.Programmatic);
        }
    }

    private async void FavoriteView_DeleteRequested(object? sender, EventArgs e)
    {
        if (sender is FavoriteItemView view)
            await SafeExecution.RunHandledAsync(() => DeleteFavoriteAsync(view.Favorite), "BrowserPage.DeleteFavorite");
    }
}
