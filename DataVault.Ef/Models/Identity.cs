using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataVault.Ef.Models;

[Table("Identity")]
[PrimaryKey("Id")]
public class Identity
{
    public Guid Id { get; set; }
}
