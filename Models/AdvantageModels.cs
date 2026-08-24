using System;
using System.Collections.Generic;

namespace TG.Payroll.Web.Models;

public class AdvanceSalaryItem
{
    public int EmpId { get; set; }
    public string EmpCode { get; set; } = string.Empty;
    public string EmpName { get; set; } = string.Empty;
    public string DesignationName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string LineName { get; set; } = string.Empty;
    public decimal Gross { get; set; }
    public decimal Basic { get; set; }
    public int PresentDays { get; set; }
    public decimal OverTimeHours { get; set; }
    public decimal EarningSalary { get; set; }
    public decimal DearnessAllowance { get; set; }
    public decimal TotalPayable { get; set; }
    public decimal AdvanceAmount { get; set; }
    public decimal BankPayable { get; set; }
    public decimal CashPayable { get; set; }
    public DateTime AdvanceFor { get; set; }
    public string AdvanceName { get; set; } = "Monthly Advance";
    public string? AccountNo { get; set; }
    public string? AccType { get; set; }
    public string? Remarks { get; set; }
    public bool IsSelected { get; set; } = true;
}

public class FestivalBonusItem
{
    public int BonusId { get; set; }
    public int EmpId { get; set; }
    public string EmpCode { get; set; } = string.Empty;
    public string EmpName { get; set; } = string.Empty;
    public string DesignationName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string LineName { get; set; } = string.Empty;
    public decimal Gross { get; set; }
    public decimal Basic { get; set; }
    public decimal BonusAmount { get; set; }
    public decimal Stamp { get; set; } = 10;
    public decimal BankPayable { get; set; }
    public decimal CashPayable { get; set; }
    public decimal NetCashPayable { get; set; }
    public DateTime FestivalFor { get; set; }
    public string FestivalName { get; set; } = "Eid-ul-Fitr";
    public string? BonusDescription { get; set; }
    public string? ServicePeriod { get; set; }
    public string? AccountNo { get; set; }
    public string? AccType { get; set; }
    public bool IsSelected { get; set; } = true;
}

public class IncrementItem
{
    public int IncrementId { get; set; }
    public int EmpId { get; set; }
    public string EmpCode { get; set; } = string.Empty;
    public string EmpName { get; set; } = string.Empty;
    public string DesignationName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string? Grade { get; set; }
    public DateTime? JoiningDate { get; set; }
    public decimal PreGross { get; set; }
    public decimal PreBasic { get; set; }
    public decimal IncrementAmount { get; set; }
    public decimal PromotedGross { get; set; }
    public decimal PromotedBasic { get; set; }
    public DateTime EffectiveDate { get; set; } = DateTime.Today;
    public string IncrementType { get; set; } = "INCREMENT";
    public string IncrementStatus { get; set; } = "CONFIRM";
    public string? LastIncrDate { get; set; }
    public decimal LastIncrAmount { get; set; }
    public string? IncrDesc { get; set; }
    public string? ServicePeriod { get; set; }
    public bool IsSelected { get; set; } = true;
}

public class PromotionItem
{
    public int IncrementId { get; set; }
    public int EmpId { get; set; }
    public string EmpCode { get; set; } = string.Empty;
    public string EmpName { get; set; } = string.Empty;
    public int PreDesignationId { get; set; }
    public string PreDesignation { get; set; } = string.Empty;
    public string? PreGrade { get; set; }
    public decimal PreGross { get; set; }
    public int PromDesignationId { get; set; }
    public string PromDesignation { get; set; } = string.Empty;
    public string? PromGrade { get; set; }
    public decimal PromGross { get; set; }
    public decimal PromotionAmount { get; set; }
    public DateTime EffectiveDate { get; set; } = DateTime.Today;
    public DateTime PromotionDate { get; set; } = DateTime.Today;
    public string IncrStatus { get; set; } = "CONFIRM";
    public string? IncrDesc { get; set; }
    public string? DepartmentName { get; set; }
}

public class EarnLeaveProcessItem
{
    public int EmpId { get; set; }
    public string EmpCode { get; set; } = string.Empty;
    public string EmpName { get; set; } = string.Empty;
    public string DesignationName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public decimal Gross { get; set; }
    public decimal Basic { get; set; }
    public string ElSegment { get; set; } = "Worker EL";
    public DateTime ElProcessDate { get; set; } = DateTime.Today;
    public DateTime FromDate { get; set; }
    public DateTime LastCountingDate { get; set; }
    public int TotalPresentDays { get; set; }
    public int TotalElDays { get; set; }
    public int ElTaken { get; set; }
    public int NetElDays { get; set; }
    public decimal ElRate { get; set; }
    public decimal NetPayable { get; set; }
    public string? Remarks { get; set; }
    public bool IsSelected { get; set; } = true;
}

public class AllowanceItem
{
    public int EmpId { get; set; }
    public string EmpCode { get; set; } = string.Empty;
    public string EmpName { get; set; } = string.Empty;
    public string DesignationName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public decimal Gross { get; set; }
    public decimal Basic { get; set; }
    public decimal BankAmount { get; set; }
    public decimal CashAmount { get; set; }
    public decimal AllowanceAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public DateTime? AllowDate { get; set; }
    public string Status { get; set; } = "Y";
    public bool IsSelected { get; set; } = true;
}

public class InsuranceItem
{
    public int EmpId { get; set; }
    public string EmpCode { get; set; } = string.Empty;
    public string EmpName { get; set; } = string.Empty;
    public string DesignationName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public DateTime? DateOfJoining { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public int Age { get; set; }
    public decimal Gross { get; set; }
    public string InsuranceHolder { get; set; } = "Y";
    public string? NomineeName { get; set; }
    public string? NomineeRelation { get; set; }
    public string? EmpStatus { get; set; }
}

public class AdvantageFilter
{
    public int? UnitId { get; set; }
    public int? DepartmentId { get; set; }
    public int? SectionId { get; set; }
    public int? LineId { get; set; }
    public int? CategoryId { get; set; }
    public string? EmpCode { get; set; }
    public DateTime EffectDate { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    public DateTime StartDate { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    public DateTime EndDate { get; set; } = DateTime.Today;
    public string TitleOrType { get; set; } = string.Empty;
    public decimal PercentageOrAmount { get; set; } = 100;
}
