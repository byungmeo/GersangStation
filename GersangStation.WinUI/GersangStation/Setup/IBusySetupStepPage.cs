using System.Threading.Tasks;

namespace GersangStation.Setup;

internal interface IBusySetupStepPage
{
    bool IsBusy { get; }
    Task<bool> ConfirmCancelBusyWorkAsync(string title, string message, string primaryButtonText, string closeButtonText);
}
