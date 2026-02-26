namespace Core.Models;

public record ClientSettings(string InstallPath, string Client2Path, string Client3Path)
{
    public ClientSettings() : this("", "", "") { }
}
