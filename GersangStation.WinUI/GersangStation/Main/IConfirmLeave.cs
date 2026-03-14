using System.Threading.Tasks;

namespace GersangStation.Main;

public enum LeaveReason
{
    Navigation,
    AppExit
}

internal interface IConfirmLeave
{
    Task<bool> ConfirmLeaveAsync(LeaveReason reason = LeaveReason.Navigation);
}
