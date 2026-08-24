using System.Globalization;
using System.Text;
using TG.Payroll.Web.Models;

namespace TG.Payroll.Web.Services;

public static class EmployeeCsvHelper
{
    public static string EscapeCsv(object? value)
    {
        if (value is null) return string.Empty;
        var s = value.ToString() ?? string.Empty;
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
        {
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
        return s;
    }

    public static byte[] GetUtf8BomBytes(string text)
    {
        var encoding = new UTF8Encoding(true);
        var preamble = encoding.GetPreamble();
        var data = encoding.GetBytes(text);
        var result = new byte[preamble.Length + data.Length];
        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(data, 0, result, preamble.Length, data.Length);
        return result;
    }

    public static string NormalizeHeaderName(string header)
    {
        if (string.IsNullOrWhiteSpace(header)) return string.Empty;
        return new string(header.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    public static string ExtractCsvVal(List<string> rowData, Dictionary<string, int> headerMap, params string[] aliases)
    {
        foreach (var alias in aliases)
        {
            var norm = NormalizeHeaderName(alias);
            if (headerMap.TryGetValue(norm, out var idx) && idx < rowData.Count)
            {
                return rowData[idx]?.Trim() ?? string.Empty;
            }
        }
        return string.Empty;
    }

    public static bool TryParseFlexibleDate(string? input, out DateTime parsedDate)
    {
        parsedDate = DateTime.Today;
        if (string.IsNullOrWhiteSpace(input)) return false;
        var trimmed = input.Trim();
        string[] formats = [
            "yyyy-MM-dd", "dd-MM-yyyy", "dd/MM/yyyy", "MM/dd/yyyy", "d/M/yyyy", "d-M-yyyy",
            "yyyy/MM/dd", "dd-MMM-yyyy", "dd MMM yyyy", "yyyy.MM.dd", "dd.MM.yyyy",
            "yyyy-M-d", "d-MMM-yy", "dd-MMM-yy"
        ];
        return DateTime.TryParseExact(trimmed, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate)
            || DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate)
            || DateTime.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsedDate);
    }

    public static List<List<string>> ParseCsv(string csvContent)
    {
        var rows = new List<List<string>>();
        var currentRow = new List<string>();
        var currentField = new StringBuilder();
        bool insideQuotes = false;

        for (int i = 0; i < csvContent.Length; i++)
        {
            char c = csvContent[i];
            if (insideQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < csvContent.Length && csvContent[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i++;
                    }
                    else
                    {
                        insideQuotes = false;
                    }
                }
                else
                {
                    currentField.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    insideQuotes = true;
                }
                else if (c == ',')
                {
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                }
                else if (c == '\r')
                {
                    if (i + 1 < csvContent.Length && csvContent[i + 1] == '\n')
                    {
                        i++;
                    }
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    if (currentRow.Count > 0 && currentRow.Any(f => !string.IsNullOrWhiteSpace(f)))
                    {
                        rows.Add(currentRow);
                    }
                    currentRow = new List<string>();
                }
                else if (c == '\n')
                {
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    if (currentRow.Count > 0 && currentRow.Any(f => !string.IsNullOrWhiteSpace(f)))
                    {
                        rows.Add(currentRow);
                    }
                    currentRow = new List<string>();
                }
                else
                {
                    currentField.Append(c);
                }
            }
        }

        if (currentField.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentField.ToString());
            if (currentRow.Any(f => !string.IsNullOrWhiteSpace(f)))
            {
                rows.Add(currentRow);
            }
        }

        return rows;
    }

    public static string EnsureUnicode(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        if (BanglaBijoyConverter.IsUnicodeBangla(text)) return text;
        if (BanglaBijoyConverter.LooksLikeBijoyAnsi(text))
        {
            return BanglaBijoyConverter.ConvertToUnicode(text);
        }
        return text;
    }

    public static string BuildExportCsv(List<EmployeeExportItem> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Emp Code,Name,Bangla Name,ERP Code,Unit,Category,Department,Section,Line,Designation,Shift,Salary Rule,Floor,Joining Date,Date of Birth,Gross,Status,Gender,Religion,Marital Status,Blood Group,NID,Contact No,Email,Father Name,Mother Name,Spouse Name,Present Village,Present Post,Present PS,Present District,Permanent Village,Permanent Post,Permanent PS,Permanent District,OverTime,Transport,Weekend,Proximity No,Grade");

        foreach (var e in items)
        {
            sb.AppendLine(string.Join(",",
                EscapeCsv(e.EmployeeCode),
                EscapeCsv(e.EmployeeName),
                EscapeCsv(EnsureUnicode(e.BanglaEmployeeName)),
                EscapeCsv(e.ErpCode),
                EscapeCsv(e.UnitName),
                EscapeCsv(e.CategoryName),
                EscapeCsv(e.DepartmentName),
                EscapeCsv(e.SectionName),
                EscapeCsv(e.LineName),
                EscapeCsv(e.DesignationName),
                EscapeCsv(e.ShiftName),
                EscapeCsv(e.SalaryRuleName),
                EscapeCsv(e.FloorName),
                EscapeCsv(e.DateOfJoining?.ToString("yyyy-MM-dd")),
                EscapeCsv(e.DateOfBirth?.ToString("yyyy-MM-dd")),
                EscapeCsv(e.Gross.ToString("0.##", CultureInfo.InvariantCulture)),
                EscapeCsv(e.EmployeeStatus),
                EscapeCsv(e.Gender),
                EscapeCsv(e.Religion),
                EscapeCsv(e.MaritalStatus),
                EscapeCsv(e.BloodGroup),
                EscapeCsv(e.NationalId),
                EscapeCsv(e.ContactNo),
                EscapeCsv(e.Email),
                EscapeCsv(EnsureUnicode(e.FatherName)),
                EscapeCsv(EnsureUnicode(e.MotherName)),
                EscapeCsv(EnsureUnicode(e.SpouseName)),
                EscapeCsv(EnsureUnicode(e.PresentVillage)),
                EscapeCsv(EnsureUnicode(e.PresentPost)),
                EscapeCsv(EnsureUnicode(e.PresentPoliceStation)),
                EscapeCsv(EnsureUnicode(e.PresentDistrict)),
                EscapeCsv(EnsureUnicode(e.PermanentVillage)),
                EscapeCsv(EnsureUnicode(e.PermanentPost)),
                EscapeCsv(EnsureUnicode(e.PermanentPoliceStation)),
                EscapeCsv(EnsureUnicode(e.PermanentDistrict)),
                EscapeCsv(e.OverTime ? "Y" : "N"),
                EscapeCsv(e.Transport ? "Y" : "N"),
                EscapeCsv(e.Weekend),
                EscapeCsv(e.ProximityNo),
                EscapeCsv(e.EmployeeGrade)
            ));
        }

        return sb.ToString();
    }

    public static string GenerateTemplateCsv(EmployeeLookups lookups)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Emp Code,Name,Bangla Name,ERP Code,Unit,Category,Department,Section,Line,Designation,Shift,Salary Rule,Floor,Joining Date,Date of Birth,Gross,Status,Gender,Religion,Marital Status,Blood Group,NID,Contact No,Email,Father Name,Mother Name,Spouse Name,Present Village,Present Post,Present PS,Present District,Permanent Village,Permanent Post,Permanent PS,Permanent District,OverTime,Transport,Weekend,Proximity No,Grade");

        var uName = lookups.Units.FirstOrDefault()?.Name ?? "Unit-1";
        var cName = lookups.Categories.FirstOrDefault()?.Name ?? "Staff";
        var dName = lookups.Departments.FirstOrDefault()?.Name ?? "Cutting";
        var sName = lookups.Sections.FirstOrDefault()?.Name ?? "General";
        var lName = lookups.Lines.FirstOrDefault()?.Name ?? "Line-1";
        var desName = lookups.Designations.FirstOrDefault()?.Name ?? "Operator";
        var shName = lookups.Shifts.FirstOrDefault()?.Name ?? "General Shift";
        var rName = lookups.SalaryRules.FirstOrDefault()?.Name ?? "Standard";
        var fName = lookups.Floors.FirstOrDefault()?.Name ?? "1st Floor";

        sb.AppendLine($"10001,John Doe,জন ডো,ERP-001,{EscapeCsv(uName)},{EscapeCsv(cName)},{EscapeCsv(dName)},{EscapeCsv(sName)},{EscapeCsv(lName)},{EscapeCsv(desName)},{EscapeCsv(shName)},{EscapeCsv(rName)},{EscapeCsv(fName)},2024-01-01,1995-05-15,18500,Active,MALE,ISLAM,SINGLE,B+,1234567890,01700000000,john@example.com,Father Name,Mother Name,,Mirpur-1,Mirpur,Mirpur,Dhaka,Mirpur-1,Mirpur,Mirpur,Dhaka,Y,N,Friday,RFID-101,Grade-4");
        sb.AppendLine($"10002,Jane Smith,জেন স্মিথ,ERP-002,{EscapeCsv(uName)},{EscapeCsv(cName)},{EscapeCsv(dName)},{EscapeCsv(sName)},{EscapeCsv(lName)},{EscapeCsv(desName)},{EscapeCsv(shName)},{EscapeCsv(rName)},{EscapeCsv(fName)},2024-02-01,1998-08-20,16000,Active,FEMALE,ISLAM,MARRIED,A+,9876543210,01800000000,jane@example.com,Father Name,Mother Name,Spouse Name,Uttara,Uttara,Uttara,Dhaka,Uttara,Uttara,Uttara,Dhaka,Y,Y,Friday,RFID-102,Grade-5");

        return sb.ToString();
    }

    public static string BuildStatusExportCsv(IEnumerable<EmployeeStatusItem> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Emp Code,Name,Designation,Department,Section,Line,DOJ,Gross,OverTime,Transport,Transport Stand");

        foreach (var e in items)
        {
            sb.AppendLine(string.Join(",",
                EscapeCsv(e.EmployeeCode),
                EscapeCsv(e.EmployeeName),
                EscapeCsv(e.DesignationName),
                EscapeCsv(e.DepartmentName),
                EscapeCsv(e.SectionName),
                EscapeCsv(e.LineName),
                EscapeCsv(e.DateOfJoining?.ToString("yyyy-MM-dd")),
                EscapeCsv(e.Gross.ToString("0.##", CultureInfo.InvariantCulture)),
                EscapeCsv(e.OverTime == "Y" ? "Y" : "N"),
                EscapeCsv(e.Transport == "Y" ? "Y" : "N"),
                EscapeCsv(e.TransportStand)
            ));
        }

        return sb.ToString();
    }

    public static string GenerateStatusTemplateCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Emp Code,Gross,OverTime,Transport,Transport Stand,DOJ");
        sb.AppendLine("10001,18500,Y,N,Mirpur,2024-01-01");
        sb.AppendLine("10002,16000,N,Y,Gazipur,2024-02-01");
        return sb.ToString();
    }

    public static string BuildPriorityExportCsv(IEnumerable<EmployeePriorityItem> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Emp Code,Name,Bangla Name,Designation,Department,Section,Status,Priority");

        foreach (var e in items)
        {
            sb.AppendLine(string.Join(",",
                EscapeCsv(e.EmployeeCode),
                EscapeCsv(e.EmployeeName),
                EscapeCsv(EnsureUnicode(e.BanglaEmployeeName)),
                EscapeCsv(e.DesignationName),
                EscapeCsv(e.DepartmentName),
                EscapeCsv(e.SectionName),
                EscapeCsv(e.EmployeeStatus),
                EscapeCsv(e.Priority)
            ));
        }

        return sb.ToString();
    }

    public static string GeneratePriorityTemplateCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Emp Code,Priority");
        sb.AppendLine("10001,1");
        sb.AppendLine("10002,2");
        return sb.ToString();
    }

    public static string BuildContactExportCsv(IEnumerable<EmployeeContactItem> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Emp Code,Name,Designation,Department,Primary Contact,Email,Blood Group,Emergency Contact Person,Emergency Cell,Nominee Cell,Reference Cell");

        foreach (var e in items)
        {
            sb.AppendLine(string.Join(",",
                EscapeCsv(e.EmployeeCode),
                EscapeCsv(e.EmployeeName),
                EscapeCsv(e.DesignationName),
                EscapeCsv(e.DepartmentName),
                EscapeCsv(e.ContactNo),
                EscapeCsv(e.Email),
                EscapeCsv(e.BloodGroup),
                EscapeCsv(EnsureUnicode(e.EmergencyContactName)),
                EscapeCsv(e.EmergencyCellNo),
                EscapeCsv(e.NomineeCellNo),
                EscapeCsv(e.RefCellNo)
            ));
        }

        return sb.ToString();
    }

    public static string GenerateContactTemplateCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Emp Code,Contact No,Email,Blood Group,Emergency Contact Person,Emergency Cell,Nominee Cell,Reference Cell");
        sb.AppendLine("10001,01711223344,john@example.com,B+,Father,01799887766,01811223344,01911223344");
        sb.AppendLine("10002,01822334455,jane@example.com,O+,Spouse,01899887766,01711223344,01611223344");
        return sb.ToString();
    }
}

