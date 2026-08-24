namespace TG.Payroll.Web.Models;

public sealed class EmployeeDetailItem
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string BanglaEmployeeName { get; set; } = string.Empty;
    public string ErpCode { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string LineName { get; set; } = string.Empty;
    public string DesignationName { get; set; } = string.Empty;
    public string ShiftName { get; set; } = string.Empty;
    public string SalaryRuleName { get; set; } = string.Empty;
    public string FloorName { get; set; } = string.Empty;
    public DateTime? DateOfJoining { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public decimal Gross { get; set; }
    public string EmployeeStatus { get; set; } = "Active";
    public string Gender { get; set; } = string.Empty;
    public string Religion { get; set; } = string.Empty;
    public string MaritalStatus { get; set; } = string.Empty;
    public string BloodGroup { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string ContactNo { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FatherName { get; set; } = string.Empty;
    public string MotherName { get; set; } = string.Empty;
    public string SpouseName { get; set; } = string.Empty;
    public string PresentAddress { get; set; } = string.Empty;
    public string PermanentAddress { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string EmployeeGrade { get; set; } = string.Empty;
}

public sealed class EmployeeDetailFilter
{
    public string Search { get; set; } = string.Empty;
    public int? UnitId { get; set; }
    public int? CategoryId { get; set; }
    public int? DepartmentId { get; set; }
    public int? SectionId { get; set; }
    public int? DesignationId { get; set; }
    public int? ShiftId { get; set; }
    public string Status { get; set; } = "Active";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

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
