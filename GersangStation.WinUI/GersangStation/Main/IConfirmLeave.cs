using System.Threading.Tasks;

namespace GersangStation.Main
{
    internal interface IConfirmLeave
    {
        Task<bool> ConfirmLeaveAsync();
    }
}
