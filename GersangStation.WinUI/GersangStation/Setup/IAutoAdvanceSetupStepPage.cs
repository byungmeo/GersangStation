using System.Threading.Tasks;

namespace GersangStation.Setup;

public interface IAutoAdvanceSetupStepPage
{
    Task WaitForCompletionAsync();
}

