using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Bank.Data;

[Table("operations")]
public class OperationRecord : BaseModel
{
    [PrimaryKey("id", false)]
    public long Id { get; set; }

    [Column("account_id")]
    public int AccountId { get; set; }

    [Column("amount")]
    public decimal Amount { get; set; }

    [Column("date")]
    public DateTime Date { get; set; }
}
