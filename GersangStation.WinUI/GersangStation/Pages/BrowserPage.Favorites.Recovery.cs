using Core;
using GersangStation.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace GersangStation.Main;

public sealed partial class WebViewPage
{
    private bool _hasFavoriteLoadFailure;
    private bool _canResetFavorites;
    private bool _isFavoriteRecoveryInProgress;
    private string _favoriteResetTarget = "browser-favorites.json";

    public bool CanRetryFavorites => _hasFavoriteLoadFailure && !_isFavoriteRecoveryInProgress;
    public bool CanResetFavorites => _canResetFavorites && CanRetryFavorites;
    public Visibility ResetFavoritesVisibility => _canResetFavorites ? Visibility.Visible : Visibility.Collapsed;

    private void NotifyFavoriteRecoveryState()
    {
        OnPropertyChanged(nameof(CanRetryFavorites));
        OnPropertyChanged(nameof(CanResetFavorites));
        OnPropertyChanged(nameof(ResetFavoritesVisibility));
    }

    private async Task LoadFavoritesAsync()
    {
        if (_isFavoriteRecoveryInProgress)
            return;
        _isFavoriteRecoveryInProgress = true;
        NotifyFavoriteRecoveryState();
        try
        {
            await _favoriteMutationGate.WaitAsync();
            try
            {
                if (_favoriteViews.Any(view => view.IsEditing))
                    return;
                var (favorites, result) = await AppDataManager.LoadBrowserFavoritesAsync();
                if (!result.Success)
                {
                    _canResetFavorites = result.ErrorKind == AppDataManager.AppDataErrorKind.Serialization;
                    _favoriteResetTarget = string.IsNullOrWhiteSpace(result.Target) ? "browser-favorites.json" : result.Target;
                    ReportFavoriteRecoveryFailure(BuildAppDataLoadException("즐겨찾기 목록", result), resetFailure: false);
                    return;
                }
                ReplaceFavorites(favorites);
                CompleteFavoriteRecovery();
            }
            finally { _favoriteMutationGate.Release(); }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 읽기 결과가 없으면 파일 손상으로 단정하거나 초기화를 제안하지 않습니다.
            _canResetFavorites = false;
            ReportFavoriteRecoveryFailure(ex, resetFailure: false);
        }
        finally
        {
            _isFavoriteRecoveryInProgress = false;
            NotifyFavoriteRecoveryState();
        }
    }

    private void CompleteFavoriteRecovery()
    {
        _favoritesLoaded = true;
        _hasFavoriteLoadFailure = false;
        _canResetFavorites = false;
        try
        {
            FavoriteRecoveryInfoBar.IsOpen = false;
            FavoriteOperationInfoBar.IsOpen = false;
            UpdateFavoriteButtonState();
        }
        catch (Exception ex) { App.ExceptionHandler.RecordRecoverable(ex, "BrowserPage.CompleteFavoriteRecoveryUi"); }
    }

    private void ReportFavoriteRecoveryFailure(Exception exception, bool resetFailure)
    {
        _favoritesLoaded = false;
        _hasFavoriteLoadFailure = true;
        App.ExceptionHandler.RecordRecoverable(exception,
            resetFailure ? "BrowserPage.ResetFavorites" : "BrowserPage.LoadFavorites");
        try
        {
            FavoriteOperationInfoBar.IsOpen = false;
            FavoriteRecoveryInfoBar.Title = resetFailure ? "즐겨찾기 초기화 실패" : "즐겨찾기 불러오기 실패";
            FavoriteRecoveryInfoBar.Message = resetFailure
                ? "즐겨찾기를 초기화하지 못했습니다. 현재 목록은 유지됩니다. 파일 사용 여부와 저장 권한을 확인한 후 다시 시도해 주세요."
                : _canResetFavorites
                    ? "즐겨찾기 파일 형식을 읽을 수 없습니다. 현재 목록은 유지됩니다. 다시 불러오거나, 저장된 즐겨찾기를 비우려면 초기화를 선택해 주세요."
                    : "즐겨찾기를 불러오지 못했습니다. 기존 데이터를 보호하기 위해 변경하지 않습니다. 파일 접근 문제를 확인한 후 다시 불러와 주세요.";
            // 숨겨진 페이지에서는 Popup이나 전역 안내를 열지 않고 이 페이지의 상태만 보존합니다.
            FavoriteRecoveryInfoBar.IsOpen = true;
        }
        catch (Exception ex) { App.ExceptionHandler.RecordRecoverable(ex, "BrowserPage.ShowFavoriteRecoveryUi"); }
        NotifyFavoriteRecoveryState();
    }

    private async void ReloadFavoritesButton_Click(object sender, RoutedEventArgs e)
        => await SafeExecution.RunHandledAsync(LoadFavoritesAsync, "BrowserPage.ReloadFavorites");

    private async void ResetFavoritesButton_Click(object sender, RoutedEventArgs e)
        => await SafeExecution.RunHandledAsync(ResetFavoritesAsync, "BrowserPage.ResetFavorites");

    private async Task ResetFavoritesAsync()
    {
        if (!CanResetFavorites)
            return;
        _isFavoriteRecoveryInProgress = true;
        NotifyFavoriteRecoveryState();
        try
        {
            if (!IsLoaded || !await ShowResetFavoritesDialogAsync(_favoriteResetTarget))
                return;
            await _favoriteMutationGate.WaitAsync();
            try
            {
                var result = await AppDataManager.SaveBrowserFavoritesAsync([]);
                if (!result.Success)
                {
                    ReportFavoriteRecoveryFailure(new InvalidOperationException(
                        $"즐겨찾기 초기화 실패: {result.Operation}, {result.Target}, {result.ErrorKind}", result.Exception),
                        resetFailure: true);
                    return;
                }
                ReplaceFavorites([]);
                CompleteFavoriteRecovery();
            }
            finally { _favoriteMutationGate.Release(); }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ReportFavoriteRecoveryFailure(ex, resetFailure: true);
        }
        finally
        {
            _isFavoriteRecoveryInProgress = false;
            NotifyFavoriteRecoveryState();
        }
    }

    private async Task<bool> ShowResetFavoritesDialogAsync(string target)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "즐겨찾기 초기화",
            Content = $"즐겨찾기 파일({target}) 형식이 손상되어 읽을 수 없습니다.\n\n즐겨찾기를 모두 비울까요? 이 작업은 되돌릴 수 없습니다.",
            PrimaryButtonText = "초기화",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Close
        };
        return await dialog.ShowManagedAsync() == ContentDialogResult.Primary;
    }
}
