using System.Threading.Tasks;

namespace GersangStation.Setup;

internal interface IAsyncSetupStepPage
{
    Task<bool> OnNextAsync();
}
