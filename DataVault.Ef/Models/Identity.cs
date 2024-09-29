using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;

namespace DataVault.Ef.Models;

[Table("Identity")]
[PrimaryKey("Id")]
public class Identity
{
    public Guid Id { get; set; }
    public string HostName { get; set; } = "";
    public DateTime Created { get; set; }

    public static Identity FromHost() =>
        new()
        {
            Id = Guid.NewGuid(),
            HostName = Dns.GetHostName(),
            Created = DateTime.UtcNow
        };
}
