namespace TG.Payroll.Web.Models;

public sealed class EmployeePriorityItem
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string BanglaEmployeeName { get; set; } = string.Empty;
    public string DesignationName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public string EmployeeStatus { get; set; } = string.Empty;
    public int Priority { get; set; }
    public int PositionPriority { get; set; }
    public int InitialPriority { get; set; }
}

public sealed class EmployeeContactItem
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string BanglaEmployeeName { get; set; } = string.Empty;
    public string DesignationName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public string EmployeeStatus { get; set; } = string.Empty;
    public string ContactNo { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string EmergencyContactName { get; set; } = string.Empty;
    public string EmergencyCellNo { get; set; } = string.Empty;
    public string NomineeCellNo { get; set; } = string.Empty;
    public string RefCellNo { get; set; } = string.Empty;
    public string PresentAddress { get; set; } = string.Empty;
    public string PermanentAddress { get; set; } = string.Empty;
    public string BloodGroup { get; set; } = string.Empty;
}
