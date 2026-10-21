using Core;
using Core.Models;
using GameMaintenance;
using System;
using System.IO;
using System.Threading.Tasks;

namespace GersangStation.Services;

internal sealed record MultiClientCreationResult(bool Success, string Message,
    Exception? Exception = null, bool Canceled = false);

/// <summary>설치 설정과 사이드바가 공유하는 2·3클라이언트 일괄 생성 절차입니다.</summary>
internal static class MultiClientCreationService
{
    internal static async Task<MultiClientCreationResult> CreateFromSavedSettingsAsync(GameServer server)
    {
        try
        {
            var (settings, result) = await AppDataManager.LoadServerClientSettingsAsync(server);
            if (!result.Success)
                return Fail(server, "게임 설치 경로 설정을 불러오지 못했습니다. 설치 설정을 확인한 후 다시 시도해 주세요.",
                    result.Exception ?? new IOException($"설정 읽기 실패: {result.Operation}, {result.Target}, {result.ErrorKind}"));
            return await CreateAsync(server, settings);
        }
        catch (Exception ex)
        {
            return Fail(server, "설정을 불러오지 못해 다클라를 생성할 수 없습니다. 잠시 후 다시 시도해 주세요.", ex);
        }
    }

    internal static async Task<MultiClientCreationResult> CreateAsync(GameServer server, ClientSettings settings)
    {
        try
        {
            // 승인/대기 중 설정이 바뀌어도 두 경로 모두 같은 요청 스냅샷을 사용합니다.
            var snapshot = new ClientSettings { InstallPath = settings.InstallPath?.Trim() ?? string.Empty };
            var validation = GameClientHelper.TryValidateInstallPath(server, snapshot.InstallPath);
            if (!validation.Success)
                return Fail(server, "게임 설치 경로가 올바르지 않습니다. 게임 설치 및 경로 지정에서 경로를 확인해 주세요.", validation.Exception);

            var version = PatchManager.TryGetCurrentClientVersion(snapshot.InstallPath);
            if (!version.Success || version.Version is null or <= 0)
                return Fail(server, "현재 게임 버전을 확인할 수 없습니다. 게임 설치 경로와 버전 파일을 확인해 주세요.", version.Exception);

            await GameMaintenanceService.CreateMultiClientAsync(server, snapshot, new CreateSymbolMultiClientArgs
            {
                InstallPath = snapshot.InstallPath,
                DestPath2 = snapshot.Client2Path,
                DestPath3 = snapshot.Client3Path,
                LayoutPolicy = version.Version.Value >= GameClientHelper.MultiClientLayoutBoundaryVersion
                    ? GameClientHelper.MultiClientLayoutPolicy.V34100OrLater
                    : GameClientHelper.MultiClientLayoutPolicy.Legacy
            });
            return new(true, "2·3클라이언트 생성을 완료했습니다.");
        }
        catch (MaintenanceCanceledException ex) { return new(false, ex.Message, Canceled: true); }
        catch (Exception ex)
        {
            return Fail(server, "2·3클라이언트를 모두 생성하지 못했습니다. 일부 파일이 생성되었을 수 있으니 설치 경로를 확인한 후 다시 시도해 주세요.", ex);
        }
    }

    private static MultiClientCreationResult Fail(GameServer server, string message, Exception? exception)
    {
        if (exception is OperationCanceledException)
            exception = new IOException("다클라 생성이 예기치 않게 중단되었습니다.", exception);
        if (exception is not null)
            App.ExceptionHandler.RecordRecoverable(exception, $"MultiClientCreation.{server}");
        return new(false, message, exception);
    }
}
