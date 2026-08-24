namespace TG.Payroll.Web.Models;

public class DesignationItem
{
    public int DesignationId { get; set; }
    public string DesignationName { get; set; } = string.Empty;
    public string? DesignationNameBang { get; set; }
    public string? Grade { get; set; }
    public string? GradeBang { get; set; }
    public string? Position { get; set; }
    public int PositionPriority { get; set; } = 1;
    public decimal AttendanceBonus { get; set; }
    public string? Remarks { get; set; }
}

public class ShiftInfoItem
{
    public int ShiftId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public string InTime { get; set; } = "08:00 AM";
    public string OutTime { get; set; } = "05:00 PM";
    public string InTimeFrom { get; set; } = "07:00 AM";
    public string OutTimeFrom { get; set; } = "04:30 PM";
    public string Grace { get; set; } = "15";
    public string LunchStart { get; set; } = "01:00 PM";
    public string LunchEnd { get; set; } = "02:00 PM";
    public string DefaultStatus { get; set; } = "General";
    public string? Remarks { get; set; }
}

public class HolidayItem
{
    public int HolidayId { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public string Type { get; set; } = "Holiday";
    public string Category { get; set; } = "All";
    public string? Remarks { get; set; }
}

public class UnitItem
{
    public int UnitId { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public string? UnitNameBang { get; set; }
    public int? CompanyId { get; set; } = 1;
    public string? CompanyName { get; set; }
    public string? ShortName { get; set; }
    public string? Address { get; set; }
    public string? AddressBang { get; set; }
    public string? Remarks { get; set; }
}

public class DepartmentItem
{
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string? DepartmentNameBang { get; set; }
    public int? UnitId { get; set; }
    public string? UnitName { get; set; }
    public int ShowPriority { get; set; } = 1;
    public string? ShortName { get; set; }
    public string? Remarks { get; set; }
}

public class CategoryItem
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? CategoryNameBang { get; set; }
    public decimal TiffinAllowance { get; set; }
    public int PositionLevel { get; set; } = 1;
    public string? Remarks { get; set; }
}

public class SectionItem
{
    public int SectionId { get; set; }
    public string SectionName { get; set; } = string.Empty;
    public string? SectionNameBang { get; set; }
    public int? UnitId { get; set; }
    public string? UnitName { get; set; }
    public int ShowTogether { get; set; } = 1;
    public string? Remarks { get; set; }
}

public class LineItem
{
    public int LineId { get; set; }
    public string LineName { get; set; } = string.Empty;
    public string? LineNameBang { get; set; }
    public int? UnitId { get; set; }
    public string? UnitName { get; set; }
    public int FloorNo { get; set; } = 1;
    public int OperatorCount { get; set; }
    public int HelperCount { get; set; }
    public int IronmanCount { get; set; }
    public int OthersCount { get; set; }
    public int Position { get; set; } = 1;
    public string? Remarks { get; set; }
}

public class TransportStandItem
{
    public int StandId { get; set; }
    public string StandName { get; set; } = string.Empty;
    public string? StandForm { get; set; }
}

public class SalaryRuleItem
{
    public int RuleId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public decimal BasicPercent { get; set; }
    public decimal HouseRentPercent { get; set; }
    public decimal MedicalAllowance { get; set; }
    public decimal TransportAllowance { get; set; }
    public decimal FoodAllowance { get; set; }
    public decimal AttendanceBonus { get; set; }
    public decimal MinAttendanceBonus { get; set; }
    public decimal DearnessAllowance { get; set; }
    public string Status { get; set; } = "Active";
    public string? Remarks { get; set; }
}

public class SigningOptionItem
{
    public int SigningId { get; set; }
    public string SigningName { get; set; } = string.Empty;
    public int SigningPriority { get; set; } = 1;
    public string SigningFor { get; set; } = "PAYSLIP";
    public string SigningStatus { get; set; } = "Active";
}

public class DormitoryItem
{
    public int DormitoryId { get; set; }
    public string DormitoryName { get; set; } = string.Empty;
    public string? DormitoryNameBang { get; set; }
    public int? UnitId { get; set; }
    public string? UnitName { get; set; }
    public int? ShowTogether { get; set; } = 1;
}

public class BuildingFloorItem : DormitoryItem
{
    public int FloorId { get => DormitoryId; set => DormitoryId = value; }
    public string FloorName { get => DormitoryName; set => DormitoryName = value; }
    public string? FloorNameBang { get => DormitoryNameBang; set => DormitoryNameBang = value; }
}

