namespace Core.Models;

public record Account(string Id, string Nickname)
{
    public override string ToString() => $"[Id: {Id}, Nickname: {Nickname}]";
    public string? GetPassword() => PasswordVaultHelper.GetPassword(Id);
}