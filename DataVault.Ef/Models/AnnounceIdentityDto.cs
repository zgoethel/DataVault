namespace DataVault.Ef.Models;

public record AnnounceIdentityDto(
    Identity Identity,
    string Address,
    int Port,
    NodeStatus Status);
