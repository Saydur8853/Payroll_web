namespace TG.Payroll.Web.Models;

public class AttendanceRecordItem
{
    public int AttendanceId { get; set; }
    public int EmpId { get; set; }
    public string EmpCode { get; set; } = string.Empty;
    public string EmpName { get; set; } = string.Empty;
    public string DesignationName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string LineName { get; set; } = string.Empty;
    public int ShiftId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public DateTime AttdDate { get; set; } = DateTime.Today;
    public string? InTime { get; set; }
    public string? OutTime { get; set; }
    public decimal LateMinutes { get; set; }
    public decimal WorkingHours { get; set; }
    public decimal OverTimeHours { get; set; }
    public string Status { get; set; } = "P"; // P, A, L, H, W
    public string Status2 { get; set; } = "P";
    public string NightStatus { get; set; } = "N"; // Y, N
    public string AttdLocked { get; set; } = "N"; // Y, N
    public string? AttdRemarks { get; set; }
    public bool IsInTimeManual { get; set; }
    public bool IsOutTimeManual { get; set; }
    public bool IsSelected { get; set; } = true;
}

public class RawAttendanceItem
{
    public int EmpId { get; set; }
    public string EmpCode { get; set; } = string.Empty;
    public string EmpName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string ShiftName { get; set; } = string.Empty;
    public DateTime AttdDate { get; set; } = DateTime.Today;
    public string? RawInTime { get; set; }
    public string? RawOutTime { get; set; }
    public string Status { get; set; } = "P";
    public decimal OverTime { get; set; }
    public string? MachineNo { get; set; }
    public string AttdLocked { get; set; } = "N";
    public bool IsManual { get; set; }
    public string? Remarks { get; set; }
}

public class MicroAttendanceItem
{
    public int EmpId { get; set; }
    public string EmpCode { get; set; } = string.Empty;
    public string EmpName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string ShiftName { get; set; } = string.Empty;
    public DateTime AttdDate { get; set; } = DateTime.Today;
    public string? InTime { get; set; }
    public string? OutTime { get; set; }
    public string? OutTime2 { get; set; }
    public string Status { get; set; } = "P";
    public decimal OverTime { get; set; }
    public decimal OverTime2 { get; set; }
    public string? ShiftOutTime { get; set; }
    public string? MachineNo { get; set; }
    public string? Remarks { get; set; }
    public bool IsSelected { get; set; } = true;
}

public class AttendanceFilter
{
    public DateTime FromDate { get; set; } = DateTime.Today;
    public DateTime ToDate { get; set; } = DateTime.Today;
    public int? UnitId { get; set; }
    public int? DepartmentId { get; set; }
    public int? SectionId { get; set; }
    public int? LineId { get; set; }
    public int? ShiftId { get; set; }
    public int? CategoryId { get; set; }
    public string? EmpCode { get; set; }
    public string? Status { get; set; } = ""; // "", "P", "A", "L", "H", "W"
}

public class AttendanceSummaryStats
{
    public int TotalCount { get; set; }
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public int LeaveCount { get; set; }
    public int HolidayCount { get; set; }
    public int WeekendCount { get; set; }
    public decimal TotalOtHours { get; set; }
    public int NightCount { get; set; }
}

public class ShiftOptionItem
{
    public int ShiftId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
}

public class RawPunchLogItem
{
    public string EmpCode { get; set; } = string.Empty;
    public string? CardNo { get; set; }
    public DateTime PunchDateTime { get; set; }
    public string? MachineNo { get; set; } = "1";
    public string? PunchType { get; set; } // IN, OUT, CHECK
}

public class AttendanceProcessResult
{
    public int TotalPunches { get; set; }
    public int ProcessedEmployees { get; set; }
    public int UpdatedPresent { get; set; }
    public int UpdatedAbsent { get; set; }
    public int UpdatedLeave { get; set; }
    public int UpdatedWeekendHoliday { get; set; }
    public int SkippedLogs { get; set; }
    public string SummaryMessage { get; set; } = string.Empty;
}
