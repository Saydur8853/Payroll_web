namespace TG.Payroll.Web.Models;

/// <summary>Maps the existing Oracle USERS table; it is not a new table.</summary>
public sealed class PayrollUser
{
    public int UserId { get; set; }
    public string? UserName { get; set; }
    public decimal? Admin { get; set; }
    public decimal? CallingId { get; set; }
    public string? Password { get; set; }
    public string? CompanyId { get; set; }
    public decimal? LoginStatus { get; set; }
    public decimal? IsLock { get; set; }
    public int? EmpId { get; set; }
    public string? EmpCode { get; set; }
    public string? Remarks { get; set; }
    public string? Photo { get; set; }
    public string? PrivilegeArray { get; set; }
    public string? DefaultMenuCalander { get; set; }
}
