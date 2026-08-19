using System.ComponentModel.DataAnnotations;

namespace TG.Payroll.Web.Models;

public sealed class EmployeeInformation
{
    public int EmployeeId { get; set; }

    [Required, StringLength(15)]
    public string EmployeeCode { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string EmployeeName { get; set; } = string.Empty;

    [StringLength(30)] public string ErpCode { get; set; } = string.Empty;
    [StringLength(150)] public string BanglaEmployeeName { get; set; } = string.Empty;
    public int UnitId { get; set; }
    public int CategoryId { get; set; }
    public int DepartmentId { get; set; }
    public int SectionId { get; set; }
    public int LineId { get; set; }
    public int DesignationId { get; set; }
    public int ShiftId { get; set; }
    public int SalaryRuleId { get; set; }
    public int FloorId { get; set; }
    public DateTime DateOfJoining { get; set; } = DateTime.Today;
    public DateTime? DateOfBirth { get; set; } = DateTime.Today.AddYears(-18);
    public DateTime? CloseDate { get; set; }
    [Range(0, double.MaxValue)] public decimal Gross { get; set; }
    public string EmployeeStatus { get; set; } = "Active";
    public string StatusReason { get; set; } = string.Empty;
    public string Weekend { get; set; } = "N/A";
    public string ProximityNo { get; set; } = string.Empty;
    public string LicenseNo { get; set; } = string.Empty;
    public string EmployeeGrade { get; set; } = string.Empty;
    public string BeneficiaryName { get; set; } = string.Empty;
    public string BanglaBeneficiaryName { get; set; } = string.Empty;
    public string RelationWithBeneficiary { get; set; } = string.Empty;
    public string NomineeCellNo { get; set; } = string.Empty;
    public string BankAccountType { get; set; } = "N";
    public string AccountNo { get; set; } = string.Empty;
    public bool Transport { get; set; }
    public bool OverTime { get; set; } = true;
    public bool QuarterHolder { get; set; }
    public bool TaxHolder { get; set; }
    public bool EarnLeaveHolder { get; set; }
    public string EarnLeaveSegment { get; set; } = "None";
    public bool Contractual { get; set; }

    public string FatherName { get; set; } = string.Empty;
    public string BanglaFatherName { get; set; } = string.Empty;
    public string MotherName { get; set; } = string.Empty;
    public string BanglaMotherName { get; set; } = string.Empty;
    public string SpouseName { get; set; } = string.Empty;
    public string BanglaSpouseName { get; set; } = string.Empty;
    public string Gender { get; set; } = "MALE";
    public string Religion { get; set; } = "ISLAM";
    public string MaritalStatus { get; set; } = "SINGLE";
    public string BloodGroup { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string ContactNo { get; set; } = string.Empty;
    [EmailAddress] public string Email { get; set; } = string.Empty;
    public string Education { get; set; } = string.Empty;
    public string EmploymentExperience { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;

    public string PresentVillage { get; set; } = string.Empty;
    public string BanglaPresentVillage { get; set; } = string.Empty;
    public string PresentPost { get; set; } = string.Empty;
    public string BanglaPresentPost { get; set; } = string.Empty;
    public string PresentPoliceStation { get; set; } = string.Empty;
    public string BanglaPresentPoliceStation { get; set; } = string.Empty;
    public string PresentDistrict { get; set; } = string.Empty;
    public string BanglaPresentDistrict { get; set; } = string.Empty;

    public string PermanentVillage { get; set; } = string.Empty;
    public string BanglaPermanentVillage { get; set; } = string.Empty;
    public string PermanentPost { get; set; } = string.Empty;
    public string BanglaPermanentPost { get; set; } = string.Empty;
    public string PermanentPoliceStation { get; set; } = string.Empty;
    public string BanglaPermanentPoliceStation { get; set; } = string.Empty;
    public string PermanentDistrict { get; set; } = string.Empty;
    public string BanglaPermanentDistrict { get; set; } = string.Empty;

    public byte[]? Photo { get; set; }
    public byte[]? Signature { get; set; }
}

public sealed record EmployeeListItem(
    int EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    string Department,
    string Designation,
    string Status,
    DateTime? JoiningDate);

public sealed record LookupOption(int Id, string Name);

public sealed class EmployeeLookups
{
    public List<LookupOption> Units { get; init; } = [];
    public List<LookupOption> Categories { get; init; } = [];
    public List<LookupOption> Departments { get; init; } = [];
    public List<LookupOption> Sections { get; init; } = [];
    public List<LookupOption> Lines { get; init; } = [];
    public List<LookupOption> Designations { get; init; } = [];
    public List<LookupOption> Shifts { get; init; } = [];
    public List<LookupOption> SalaryRules { get; init; } = [];
    public List<LookupOption> Floors { get; init; } = [];
}

public sealed class SalaryRuleDetail
{
    public int RuleId { get; set; }
    public decimal RuleBasic { get; set; }
    public decimal RuleHouseRent { get; set; }
    public decimal RuleMedical { get; set; }
    public decimal RuleTransport { get; set; }
    public decimal RuleFood { get; set; }
}

public sealed class EmployeeLeaveSummary
{
    public decimal CasualLeave { get; set; }
    public decimal SickLeave { get; set; }
    public decimal EarnLeave { get; set; }
    public decimal CalculatedEl { get; set; }
    public decimal ObservedEl { get; set; }
    public decimal CarryEl { get; set; }
    public bool HasData { get; set; }
}

public sealed class CharacterCertificateItem
{
    public DateTime RecordDate { get; set; }
    public string MonthYear { get; set; } = string.Empty;
    public string Rating { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
}

public sealed class EmployeeStatusFilter
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int? UnitId { get; set; }
    public int? CategoryId { get; set; }
    public int? DepartmentId { get; set; }
    public int? SectionId { get; set; }
    public int? LineId { get; set; }
    public int? DesignationId { get; set; }
    public string? SearchQuery { get; set; }
}

public sealed class EmployeeStatusItem
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string DesignationName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string LineName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public DateTime? DateOfJoining { get; set; }
    public decimal Gross { get; set; }
    public int Age { get; set; }
    public string OverTime { get; set; } = "N";
    public string Transport { get; set; } = "N";
    public string TransportStand { get; set; } = string.Empty;

    // Change Tracking
    public bool IsEdited { get; set; }
    public decimal OriginalGross { get; set; }
    public string OriginalOverTime { get; set; } = "N";
    public string OriginalTransport { get; set; } = "N";
    public DateTime? OriginalDateOfJoining { get; set; }
    public string OriginalTransportStand { get; set; } = string.Empty;
}
