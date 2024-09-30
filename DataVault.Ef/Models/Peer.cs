using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataVault.Ef.Models;

[Table("Peer")]
[PrimaryKey("Id")]
public class Peer
{
    public Guid Id { get; set; }
    public string HostName { get; set; } = "";
    public string Address { get; set; } = "";
    public int Port { get; set; }
    public NodeStatus? Status { get; set; }
    public DateTime? StatusReported { get; set; }
    public DateTime Created { get; set; }
}
