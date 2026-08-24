using System;
using System.Collections.Generic;

namespace TG.Payroll.Web.Models;

public class SalaryProcessItem
{
    public int EmpId { get; set; }
    public string EmpCode { get; set; } = string.Empty;
    public string EmpName { get; set; } = string.Empty;
    public string DesignationName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string LineName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public DateTime? DateOfJoining { get; set; }
    public decimal Gross { get; set; }
    public decimal Basic { get; set; }
    public int PresentDays { get; set; }
    public int AbsentDays { get; set; }
    public int LeaveDays { get; set; }
    public int HolidayDays { get; set; }
    public int WeekendDays { get; set; }
    public int TotalPayDays { get; set; }
    public decimal EarningSalary { get; set; }
    public decimal AbsentAmount { get; set; }
    public decimal AttBonus { get; set; }
    public decimal OverTimeHours { get; set; }
    public decimal OtRate { get; set; }
    public decimal OtAmount { get; set; }
    public int NightDays { get; set; }
    public decimal NightAmount { get; set; }
    public decimal AdvanceDeduction { get; set; }
    public decimal DuesAmount { get; set; }
    public decimal PunishmentDeduction { get; set; }
    public decimal OtherDeduction { get; set; }
    public decimal Stamp { get; set; } = 10;
    public decimal TotalDeduction { get; set; }
    public decimal Payable { get; set; }
    public decimal NetPayable { get; set; }
    public decimal BankPayable { get; set; }
    public decimal CashPayable { get; set; }
    public decimal NetCashPayable { get; set; }
    public string? AccountNo { get; set; }
    public string AccType { get; set; } = "Cash";
    public DateTime SalaryFor { get; set; }
    public bool IsSelected { get; set; } = true;
    public bool IsPaid { get; set; }
}

public class AdvanceDuesDeductionItem
{
    public int AddId { get; set; }
    public int EmpId { get; set; }
    public string EmpCode { get; set; } = string.Empty;
    public string EmpName { get; set; } = string.Empty;
    public string DesignationName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string AddType { get; set; } = "Advance"; // Advance, Dues, Deduction, Loan
    public decimal AddAmount { get; set; }
    public DateTime MonthYear { get; set; } = DateTime.Today;
    public string? Remarks { get; set; }
    public bool IsSelected { get; set; } = true;
}

public class HourPunishmentItem
{
    public int DedHourId { get; set; }
    public int EmpId { get; set; }
    public string EmpCode { get; set; } = string.Empty;
    public string EmpName { get; set; } = string.Empty;
    public string DesignationName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public decimal DedHours { get; set; }
    public DateTime Dt { get; set; } = DateTime.Today;
    public string? Remarks { get; set; } = "LUNCH OUT";
    public bool IsSelected { get; set; } = true;
}

public class LeaveEntryItem
{
    public int LeaveId { get; set; }
    public int EmpId { get; set; }
    public string EmpCode { get; set; } = string.Empty;
    public string EmpName { get; set; } = string.Empty;
    public string DesignationName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string LeaveType { get; set; } = "CL"; // CL, SL, EL, ML, LWP, SPL
    public DateTime FromDate { get; set; } = DateTime.Today;
    public DateTime ToDate { get; set; } = DateTime.Today;
    public int GrantDays { get; set; } = 1;
    public string IsOffday { get; set; } = "N";
    public string? Remarks { get; set; }
    public DateTime LeaveEntryDate { get; set; } = DateTime.Today;
    public bool IsSelected { get; set; } = true;
}

public class LeaveBalanceInfo
{
    public int EntitledCl { get; set; } = 10;
    public int TakenCl { get; set; }
    public int BalanceCl => Math.Max(0, EntitledCl - TakenCl);

    public int EntitledSl { get; set; } = 14;
    public int TakenSl { get; set; }
    public int BalanceSl => Math.Max(0, EntitledSl - TakenSl);

    public int EntitledEl { get; set; } = 15;
    public int TakenEl { get; set; }
    public int BalanceEl => Math.Max(0, EntitledEl - TakenEl);
}

public class LeaveProcessItem
{
    public int EmpId { get; set; }
    public string EmpCode { get; set; } = string.Empty;
    public string EmpName { get; set; } = string.Empty;
    public string DesignationName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public int ClCount { get; set; }
    public int SlCount { get; set; }
    public int ElCount { get; set; }
    public int TotalLeaveDays { get; set; }
    public DateTime LeaveMonth { get; set; } = DateTime.Today;
    public string? Remarks { get; set; }
    public bool IsSelected { get; set; } = true;
}

public class MaternityLeaveItem
{
    public int MaternityId { get; set; }
    public int EmpId { get; set; }
    public string EmpCode { get; set; } = string.Empty;
    public string EmpName { get; set; } = string.Empty;
    public string DesignationName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public decimal Gross { get; set; }
    public decimal Basic { get; set; }
    public DateTime FromDate { get; set; } = DateTime.Today;
    public DateTime ToDate { get; set; } = DateTime.Today.AddDays(112);
    public int TotalDays { get; set; } = 112; // 16 weeks standard
    public decimal TotalPayable { get; set; }
    public decimal FirstPayment { get; set; }
    public decimal SecondPayment { get; set; }
    public DateTime? FirstPaymentDate { get; set; }
    public DateTime? SecondPaymentDate { get; set; }
    public string Status { get; set; } = "Active";
    public string? Remarks { get; set; }
    public bool IsSelected { get; set; } = true;
}

public class MicroLeaveItem
{
    public int WeekendId { get; set; }
    public int EmpId { get; set; }
    public string EmpCode { get; set; } = string.Empty;
    public string EmpName { get; set; } = string.Empty;
    public string DesignationName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string WeekendType { get; set; } = "Friday";
    public DateTime FromDate { get; set; } = DateTime.Today;
    public DateTime ToDate { get; set; } = DateTime.Today;
    public int TotalDays { get; set; } = 1;
    public string? Remarks { get; set; }
    public bool IsSelected { get; set; } = true;
}

public class SalaryFilter
{
    public int? UnitId { get; set; }
    public int? DepartmentId { get; set; }
    public int? SectionId { get; set; }
    public int? LineId { get; set; }
    public int? CategoryId { get; set; }
    public string? EmpCode { get; set; }
    public DateTime SalaryMonth { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    public DateTime StartDate { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    public DateTime EndDate { get; set; } = DateTime.Today;
    public string SalaryType { get; set; } = "REGULAR"; // REGULAR, MICRO, BINARY, BUYER
}
