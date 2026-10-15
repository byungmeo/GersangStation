using Core.Models;
using Windows.Storage;

namespace Core;

public static partial class AppDataManager
{
    public static LastPatchRecord? LoadLastPatchRecord(GameServer server)
    {
        if (!Enum.IsDefined(server)) throw new ArgumentOutOfRangeException(nameof(server));
        if (!LocalSettings.Values.TryGetValue($"LastPatchRecord.{server}", out object? stored)
            || stored is not ApplicationDataCompositeValue value
            || !value.TryGetValue("FromVersion", out object? from) || from is not int fromVersion
            || !value.TryGetValue("ToVersion", out object? to) || to is not int toVersion
            || !value.TryGetValue("InstallPath", out object? path) || path is not string installPath)
            return null;

        var record = new LastPatchRecord(fromVersion, toVersion, installPath);
        return record.IsValid ? record : null;
    }

    public static void SaveLastPatchRecord(GameServer server, LastPatchRecord record)
    {
        if (!Enum.IsDefined(server)) throw new ArgumentOutOfRangeException(nameof(server));
        ArgumentNullException.ThrowIfNull(record);
        if (!record.IsValid) throw new ArgumentException("Invalid successful patch range.", nameof(record));

        // Commit the complete range as one settings value; never mix fields from separate runs.
        LocalSettings.Values[$"LastPatchRecord.{server}"] = new ApplicationDataCompositeValue
        {
            ["FromVersion"] = record.FromVersion,
            ["ToVersion"] = record.ToVersion,
            ["InstallPath"] = record.InstallPath
        };
    }
}
