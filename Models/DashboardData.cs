namespace TG.Payroll.Web.Models;

public sealed class DashboardData
{
    public int TotalEmployees { get; set; }
    public int ActiveEmployees { get; set; }
    public int InactiveEmployees { get; set; }
    public int NewJoiners { get; set; }
    public int ClosedEmployees { get; set; }
    public int Releases { get; set; }
    public int Workers { get; set; }
    public int Staff { get; set; }
    public int Officers { get; set; }
    public int Male { get; set; }
    public int Female { get; set; }
    public int CashPay { get; set; }
    public int BankPay { get; set; }
    public int MobilePay { get; set; }
    public int TaxHolders { get; set; }
    public int QuarterHolders { get; set; }
    public int Increments { get; set; }
    public int OnLeave { get; set; }
    public int Maternity { get; set; }
}
