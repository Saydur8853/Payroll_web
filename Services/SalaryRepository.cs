using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using TG.Payroll.Web.Data;
using TG.Payroll.Web.Models;

namespace TG.Payroll.Web.Services;

public class SalaryRepository
{
    private readonly string _connectionString;

    public SalaryRepository(IConfiguration configuration)
    {
        _connectionString = DatabaseOptions.GetConnectionString(configuration);
    }

    private OracleConnection GetConnection() => new(_connectionString);

    // =========================================================================
    // 1. MONTHLY SALARY ENGINE (Calculate, Load, Save, Delete)
    // =========================================================================
    public async Task<List<SalaryProcessItem>> CalculateMonthlySalaryAsync(SalaryFilter filter, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var daysInMonth = DateTime.DaysInMonth(filter.SalaryMonth.Year, filter.SalaryMonth.Month);
        var monthStart = new DateTime(filter.SalaryMonth.Year, filter.SalaryMonth.Month, 1);
        var monthEnd = new DateTime(filter.SalaryMonth.Year, filter.SalaryMonth.Month, daysInMonth);

        var sql = """
            SELECT E.EMP_ID, E.EMP_CODE, NVL(E.EMP_NAME, '') AS EMP_NAME,
                   NVL(D.DESIGNATION_NAME, '') AS DESIGNATION_NAME,
                   NVL(DEP.DEPARTMENT_NAME, '') AS DEPARTMENT_NAME,
                   NVL(S.SECTION_NAME, '') AS SECTION_NAME,
                   NVL(L.LINE_NAME, '') AS LINE_NAME,
                   NVL(C.EMP_CATEGORY_NAME, '') AS CATEGORY_NAME,
                   E.DATE_OF_JOINING,
                   NVL(E.GROSS, 0) AS GROSS, NVL(E.BASIC, 0) AS BASIC,
                   NVL(E.ACCOUNT_NO, '') AS ACCOUNT_NO, NVL(E.ACC_TYPE, 'Cash') AS ACC_TYPE,
                   NVL((
                       SELECT COUNT(1) FROM ATTENDANCE_DETAILS AD 
                       WHERE AD.EMP_ID = E.EMP_ID 
                         AND AD.ATTD_DATE BETWEEN :mStart AND :mEnd 
                         AND AD.STATUS = 'P'
                   ), 0) AS PRESENT_DAYS,
                   NVL((
                       SELECT COUNT(1) FROM ATTENDANCE_DETAILS AD 
                       WHERE AD.EMP_ID = E.EMP_ID 
                         AND AD.ATTD_DATE BETWEEN :mStart AND :mEnd 
                         AND AD.STATUS = 'A'
                   ), 0) AS ABSENT_DAYS,
                   NVL((
                       SELECT COUNT(1) FROM ATTENDANCE_DETAILS AD 
                       WHERE AD.EMP_ID = E.EMP_ID 
                         AND AD.ATTD_DATE BETWEEN :mStart AND :mEnd 
                         AND AD.STATUS = 'L'
                   ), 0) AS LEAVE_DAYS,
                   NVL((
                       SELECT COUNT(1) FROM ATTENDANCE_DETAILS AD 
                       WHERE AD.EMP_ID = E.EMP_ID 
                         AND AD.ATTD_DATE BETWEEN :mStart AND :mEnd 
                         AND AD.STATUS = 'H'
                   ), 0) AS HOLIDAY_DAYS,
                   NVL((
                       SELECT COUNT(1) FROM ATTENDANCE_DETAILS AD 
                       WHERE AD.EMP_ID = E.EMP_ID 
                         AND AD.ATTD_DATE BETWEEN :mStart AND :mEnd 
                         AND AD.STATUS = 'W'
                   ), 0) AS WEEKEND_DAYS,
                   NVL((
                       SELECT SUM(AD.OVER_TIME) FROM ATTENDANCE_DETAILS AD 
                       WHERE AD.EMP_ID = E.EMP_ID 
                         AND AD.ATTD_DATE BETWEEN :mStart AND :mEnd
                   ), 0) AS OT_HOURS,
                   NVL((
                       SELECT COUNT(1) FROM ATTENDANCE_DETAILS AD 
                       WHERE AD.EMP_ID = E.EMP_ID 
                         AND AD.ATTD_DATE BETWEEN :mStart AND :mEnd
                         AND AD.NIGHT_STATUS = 'Y'
                   ), 0) AS NIGHT_DAYS,
                   NVL((
                       SELECT SUM(NVL(PAYABLE, 0)) FROM ADVANCE_SALARY ADV
                       WHERE ADV.EMP_ID = E.EMP_ID
                         AND TRUNC(ADV.ADVANCE_FOR, 'MM') = TRUNC(:salMonth, 'MM')
                   ), 0) AS ADVANCE_DEDUCTION,
                   NVL((
                       SELECT SUM(NVL(DED_HOURS, 0)) FROM PUNISHMENT P
                       WHERE P.EMP_ID = E.EMP_ID
                         AND P.DT BETWEEN :mStart AND :mEnd
                   ), 0) AS PUNISH_HOURS,
                   NVL((
                       SELECT SUM(CASE WHEN UPPER(ADD_TYPE) = 'DUES' THEN ADD_AMOUNT ELSE 0 END) 
                       FROM ADVANCE_DUES_DEDUCTION ADD_D
                       WHERE ADD_D.EMP_ID = E.EMP_ID
                         AND TRUNC(ADD_D.MONTH_YEAR, 'MM') = TRUNC(:salMonth, 'MM')
                   ), 0) AS DUES_ADDITION,
                   NVL((
                       SELECT SUM(CASE WHEN UPPER(ADD_TYPE) = 'DEDUCTION' THEN ADD_AMOUNT ELSE 0 END) 
                       FROM ADVANCE_DUES_DEDUCTION ADD_D
                       WHERE ADD_D.EMP_ID = E.EMP_ID
                         AND TRUNC(ADD_D.MONTH_YEAR, 'MM') = TRUNC(:salMonth, 'MM')
                   ), 0) AS OTHER_DEDUCTION
            FROM EMP_OFFICIAL E
            LEFT JOIN DESIGNATION D ON E.DESIGNATION_ID = D.DESIGNATION_ID
            LEFT JOIN DEPARTMENT DEP ON E.DEPARTMENT_ID = DEP.DEPARTMENT_ID
            LEFT JOIN SECTION S ON E.SECTION_ID = S.SECTION_ID
            LEFT JOIN LINE L ON E.LINE_ID = L.LINE_ID
            LEFT JOIN EMP_CATEGORY C ON E.EMP_CATEGORY_ID = C.EMP_CATEGORY_ID
            WHERE E.EMP_STATUS IN ('Active', 'Maternity')
            """;

        if (filter.UnitId.HasValue && filter.UnitId.Value > 0) sql += " AND E.UNIT_ID = :unitId";
        if (filter.DepartmentId.HasValue && filter.DepartmentId.Value > 0) sql += " AND E.DEPARTMENT_ID = :deptId";
        if (filter.SectionId.HasValue && filter.SectionId.Value > 0) sql += " AND E.SECTION_ID = :secId";
        if (filter.LineId.HasValue && filter.LineId.Value > 0) sql += " AND E.LINE_ID = :lineId";
        if (filter.CategoryId.HasValue && filter.CategoryId.Value > 0) sql += " AND E.EMP_CATEGORY_ID = :catId";
        if (!string.IsNullOrWhiteSpace(filter.EmpCode)) sql += " AND E.EMP_CODE = :empCode";

        sql += " ORDER BY DEP.DEPARTMENT_NAME, S.SECTION_NAME, TO_NUMBER(REGEXP_SUBSTR(E.EMP_CODE, '^[0-9]+'))";

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("mStart", monthStart));
        cmd.Parameters.Add(new OracleParameter("mEnd", monthEnd));
        cmd.Parameters.Add(new OracleParameter("salMonth", filter.SalaryMonth));
        if (filter.UnitId.HasValue && filter.UnitId.Value > 0) cmd.Parameters.Add(new OracleParameter("unitId", filter.UnitId.Value));
        if (filter.DepartmentId.HasValue && filter.DepartmentId.Value > 0) cmd.Parameters.Add(new OracleParameter("deptId", filter.DepartmentId.Value));
        if (filter.SectionId.HasValue && filter.SectionId.Value > 0) cmd.Parameters.Add(new OracleParameter("secId", filter.SectionId.Value));
        if (filter.LineId.HasValue && filter.LineId.Value > 0) cmd.Parameters.Add(new OracleParameter("lineId", filter.LineId.Value));
        if (filter.CategoryId.HasValue && filter.CategoryId.Value > 0) cmd.Parameters.Add(new OracleParameter("catId", filter.CategoryId.Value));
        if (!string.IsNullOrWhiteSpace(filter.EmpCode)) cmd.Parameters.Add(new OracleParameter("empCode", filter.EmpCode.Trim()));

        var list = new List<SalaryProcessItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var gross = Convert.ToDecimal(reader["GROSS"]);
            var basic = Convert.ToDecimal(reader["BASIC"]);
            if (basic <= 0) basic = Math.Round(gross * 0.6m, 2);

            var present = Convert.ToInt32(reader["PRESENT_DAYS"]);
            var absent = Convert.ToInt32(reader["ABSENT_DAYS"]);
            var leave = Convert.ToInt32(reader["LEAVE_DAYS"]);
            var holiday = Convert.ToInt32(reader["HOLIDAY_DAYS"]);
            var weekend = Convert.ToInt32(reader["WEEKEND_DAYS"]);
            var otHours = Convert.ToDecimal(reader["OT_HOURS"]);
            var nightDays = Convert.ToInt32(reader["NIGHT_DAYS"]);
            var advDeduct = Convert.ToDecimal(reader["ADVANCE_DEDUCTION"]);
            var punishHours = Convert.ToDecimal(reader["PUNISH_HOURS"]);
            var dues = Convert.ToDecimal(reader["DUES_ADDITION"]);
            var otherDeduct = Convert.ToDecimal(reader["OTHER_DEDUCTION"]);

            // Total pay days
            var payDays = Math.Min(daysInMonth, present + leave + holiday + weekend);
            var perDayGross = daysInMonth > 0 ? (gross / daysInMonth) : 0;
            var perDayBasic = daysInMonth > 0 ? (basic / daysInMonth) : 0;

            var earningSalary = Math.Round(perDayGross * payDays, 2);
            var absentAmount = Math.Round(perDayGross * absent, 2);

            // Attendance Bonus (e.g. 500 Tk for workers with 0 absent)
            var attBonus = absent == 0 && present > 15 ? 500m : 0m;

            // OT Calculation: Basic / 208 * 2 * OT_Hours
            var otRate = Math.Round((basic / 208m) * 2m, 2);
            var otAmount = Math.Round(otHours * otRate, 2);

            // Night Allowance (50 Tk per night shift)
            var nightAmount = nightDays * 50m;

            // Hour punishment deduction: (Basic / 208) * punishHours
            var punishDeduct = Math.Round((basic / 208m) * punishHours, 2);

            var stamp = 10m;
            var totalDeduction = advDeduct + otherDeduct + punishDeduct;
            var payable = Math.Max(0, earningSalary + attBonus + otAmount + nightAmount + dues - totalDeduction);
            var netPayable = payable;

            var accType = Convert.ToString(reader["ACC_TYPE"]) ?? "Cash";
            var isBank = accType.Equals("Bank", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(Convert.ToString(reader["ACCOUNT_NO"]));

            var bankPayable = isBank ? netPayable : 0;
            var cashPayable = isBank ? 0 : netPayable;
            var netCashPayable = isBank ? 0 : Math.Max(0, cashPayable - stamp);

            list.Add(new SalaryProcessItem
            {
                EmpId = Convert.ToInt32(reader["EMP_ID"]),
                EmpCode = Convert.ToString(reader["EMP_CODE"]) ?? "",
                EmpName = Convert.ToString(reader["EMP_NAME"]) ?? "",
                DesignationName = Convert.ToString(reader["DESIGNATION_NAME"]) ?? "",
                DepartmentName = Convert.ToString(reader["DEPARTMENT_NAME"]) ?? "",
                SectionName = Convert.ToString(reader["SECTION_NAME"]) ?? "",
                LineName = Convert.ToString(reader["LINE_NAME"]) ?? "",
                CategoryName = Convert.ToString(reader["CATEGORY_NAME"]) ?? "",
                DateOfJoining = reader["DATE_OF_JOINING"] == DBNull.Value ? null : Convert.ToDateTime(reader["DATE_OF_JOINING"]),
                Gross = gross,
                Basic = basic,
                PresentDays = present,
                AbsentDays = absent,
                LeaveDays = leave,
                HolidayDays = holiday,
                WeekendDays = weekend,
                TotalPayDays = payDays,
                EarningSalary = earningSalary,
                AbsentAmount = absentAmount,
                AttBonus = attBonus,
                OverTimeHours = otHours,
                OtRate = otRate,
                OtAmount = otAmount,
                NightDays = nightDays,
                NightAmount = nightAmount,
                AdvanceDeduction = advDeduct,
                DuesAmount = dues,
                PunishmentDeduction = punishDeduct,
                OtherDeduction = otherDeduct,
                Stamp = stamp,
                TotalDeduction = totalDeduction,
                Payable = payable,
                NetPayable = netPayable,
                BankPayable = bankPayable,
                CashPayable = cashPayable,
                NetCashPayable = netCashPayable,
                AccountNo = Convert.ToString(reader["ACCOUNT_NO"]),
                AccType = accType,
                SalaryFor = filter.SalaryMonth
            });
        }

        return list;
    }

    public async Task<List<SalaryProcessItem>> GetSavedSalariesAsync(SalaryFilter filter, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var sql = """
            SELECT S.EMP_ID, S.EMP_CODE, NVL(E.EMP_NAME, S.EMP_NAME) AS EMP_NAME,
                   NVL(D.DESIGNATION_NAME, '') AS DESIGNATION_NAME,
                   NVL(DEP.DEPARTMENT_NAME, '') AS DEPARTMENT_NAME,
                   NVL(SEC.SECTION_NAME, '') AS SECTION_NAME,
                   NVL(L.LINE_NAME, '') AS LINE_NAME,
                   NVL(S.GROSS, 0) AS GROSS, NVL(S.BASIC, 0) AS BASIC,
                   NVL(S.PRESENT, 0) AS PRESENT, NVL(S.ABSENT, 0) AS ABSENT,
                   NVL(S.LEAVE, 0) AS LEAVE, NVL(S.HOLIDAY, 0) AS HOLIDAY,
                   NVL(S.WEEKEND, 0) AS WEEKEND,
                   NVL(S.EARNING_SALARY, 0) AS EARNING_SALARY,
                   NVL(S.ATT_BONUS, 0) AS ATT_BONUS,
                   NVL(S.OVER_TIME, 0) AS OVER_TIME, NVL(S.OT_RATE, 0) AS OT_RATE,
                   NVL(S.OTAMOUNT, 0) AS OTAMOUNT,
                   NVL(S.NIGHT, 0) AS NIGHT, NVL(S.NIGHT_AMOUNT, 0) AS NIGHT_AMOUNT,
                   NVL(S.ADVANCE, 0) AS ADVANCE, NVL(S.DUES, 0) AS DUES,
                   NVL(S.DEDUCTION, 0) AS DEDUCTION, NVL(S.STAMP, 10) AS STAMP,
                   NVL(S.TOTAL_DEDUCTION, 0) AS TOTAL_DEDUCTION,
                   NVL(S.NET_PAYABLE, 0) AS NET_PAYABLE,
                   NVL(S.BANK_PAYABLE, 0) AS BANK_PAYABLE,
                   NVL(S.CASH_PAYABLE, 0) AS CASH_PAYABLE,
                   NVL(S.NET_CASH_PAYABLE, 0) AS NET_CASH_PAYABLE,
                   S.ACCOUNT_NO, S.SALARY_FOR, S.IS_ACTIVE
            FROM SALARY S
            LEFT JOIN EMP_OFFICIAL E ON S.EMP_ID = E.EMP_ID
            LEFT JOIN DESIGNATION D ON E.DESIGNATION_ID = D.DESIGNATION_ID
            LEFT JOIN DEPARTMENT DEP ON E.DEPARTMENT_ID = DEP.DEPARTMENT_ID
            LEFT JOIN SECTION SEC ON E.SECTION_ID = SEC.SECTION_ID
            LEFT JOIN LINE L ON E.LINE_ID = L.LINE_ID
            WHERE TRUNC(S.SALARY_FOR, 'MM') = TRUNC(:salMonth, 'MM')
            """;

        if (filter.DepartmentId.HasValue && filter.DepartmentId.Value > 0) sql += " AND E.DEPARTMENT_ID = :deptId";
        if (filter.SectionId.HasValue && filter.SectionId.Value > 0) sql += " AND E.SECTION_ID = :secId";
        if (!string.IsNullOrWhiteSpace(filter.EmpCode)) sql += " AND S.EMP_CODE = :empCode";

        sql += " ORDER BY DEP.DEPARTMENT_NAME, SEC.SECTION_NAME, TO_NUMBER(REGEXP_SUBSTR(S.EMP_CODE, '^[0-9]+'))";

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("salMonth", filter.SalaryMonth));
        if (filter.DepartmentId.HasValue && filter.DepartmentId.Value > 0) cmd.Parameters.Add(new OracleParameter("deptId", filter.DepartmentId.Value));
        if (filter.SectionId.HasValue && filter.SectionId.Value > 0) cmd.Parameters.Add(new OracleParameter("secId", filter.SectionId.Value));
        if (!string.IsNullOrWhiteSpace(filter.EmpCode)) cmd.Parameters.Add(new OracleParameter("empCode", filter.EmpCode.Trim()));

        var list = new List<SalaryProcessItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new SalaryProcessItem
            {
                EmpId = Convert.ToInt32(reader["EMP_ID"]),
                EmpCode = Convert.ToString(reader["EMP_CODE"]) ?? "",
                EmpName = Convert.ToString(reader["EMP_NAME"]) ?? "",
                DesignationName = Convert.ToString(reader["DESIGNATION_NAME"]) ?? "",
                DepartmentName = Convert.ToString(reader["DEPARTMENT_NAME"]) ?? "",
                SectionName = Convert.ToString(reader["SECTION_NAME"]) ?? "",
                LineName = Convert.ToString(reader["LINE_NAME"]) ?? "",
                Gross = Convert.ToDecimal(reader["GROSS"]),
                Basic = Convert.ToDecimal(reader["BASIC"]),
                PresentDays = Convert.ToInt32(reader["PRESENT"]),
                AbsentDays = Convert.ToInt32(reader["ABSENT"]),
                LeaveDays = Convert.ToInt32(reader["LEAVE"]),
                HolidayDays = Convert.ToInt32(reader["HOLIDAY"]),
                WeekendDays = Convert.ToInt32(reader["WEEKEND"]),
                EarningSalary = Convert.ToDecimal(reader["EARNING_SALARY"]),
                AttBonus = Convert.ToDecimal(reader["ATT_BONUS"]),
                OverTimeHours = Convert.ToDecimal(reader["OVER_TIME"]),
                OtRate = Convert.ToDecimal(reader["OT_RATE"]),
                OtAmount = Convert.ToDecimal(reader["OTAMOUNT"]),
                NightDays = Convert.ToInt32(reader["NIGHT"]),
                NightAmount = Convert.ToDecimal(reader["NIGHT_AMOUNT"]),
                AdvanceDeduction = Convert.ToDecimal(reader["ADVANCE"]),
                DuesAmount = Convert.ToDecimal(reader["DUES"]),
                OtherDeduction = Convert.ToDecimal(reader["DEDUCTION"]),
                Stamp = Convert.ToDecimal(reader["STAMP"]),
                TotalDeduction = Convert.ToDecimal(reader["TOTAL_DEDUCTION"]),
                NetPayable = Convert.ToDecimal(reader["NET_PAYABLE"]),
                BankPayable = Convert.ToDecimal(reader["BANK_PAYABLE"]),
                CashPayable = Convert.ToDecimal(reader["CASH_PAYABLE"]),
                NetCashPayable = Convert.ToDecimal(reader["NET_CASH_PAYABLE"]),
                AccountNo = Convert.ToString(reader["ACCOUNT_NO"]),
                SalaryFor = Convert.ToDateTime(reader["SALARY_FOR"])
            });
        }
        return list;
    }

    public async Task SaveMonthlySalaryAsync(List<SalaryProcessItem> items, CancellationToken ct = default)
    {
        if (items.Count == 0) return;
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var trans = conn.BeginTransaction();

        try
        {
            const string delSql = "DELETE FROM SALARY WHERE TRUNC(SALARY_FOR, 'MM') = TRUNC(:salMonth, 'MM') AND EMP_ID = :empId";
            await using var delCmd = new OracleCommand(delSql, conn) { Transaction = trans, BindByName = true };

            const string insSql = """
                INSERT INTO SALARY (
                    EMP_ID, EMP_CODE, EMP_NAME, GROSS, BASIC, DOJ,
                    PRESENT, ABSENT, LEAVE, HOLIDAY, WEEKEND,
                    EARNING_SALARY, ATT_BONUS, OVER_TIME, OT_RATE, OTAMOUNT,
                    NIGHT, NIGHT_AMOUNT, ADVANCE, DUES, DEDUCTION, STAMP,
                    TOTAL_DEDUCTION, NET_PAYABLE, PAYABLE, BANK_PAYABLE,
                    CASH_PAYABLE, NET_CASH_PAYABLE, ACCOUNT_NO, SALARY_FOR, IS_ACTIVE
                ) VALUES (
                    :empId, :empCode, :empName, :gross, :basic, :doj,
                    :pres, :abs, :leave, :hol, :wnd,
                    :earning, :attBns, :ot, :otRate, :otAmt,
                    :night, :nightAmt, :adv, :dues, :ded, :stamp,
                    :totDed, :netPay, :payable, :bank,
                    :cash, :netCash, :accNo, :salFor, 'Y'
                )
                """;
            await using var insCmd = new OracleCommand(insSql, conn) { Transaction = trans, BindByName = true };

            foreach (var item in items.Where(i => i.IsSelected))
            {
                delCmd.Parameters.Clear();
                delCmd.Parameters.Add(new OracleParameter("salMonth", item.SalaryFor));
                delCmd.Parameters.Add(new OracleParameter("empId", item.EmpId));
                await delCmd.ExecuteNonQueryAsync(ct);

                insCmd.Parameters.Clear();
                insCmd.Parameters.Add(new OracleParameter("empId", item.EmpId));
                insCmd.Parameters.Add(new OracleParameter("empCode", item.EmpCode));
                insCmd.Parameters.Add(new OracleParameter("empName", item.EmpName));
                insCmd.Parameters.Add(new OracleParameter("gross", item.Gross));
                insCmd.Parameters.Add(new OracleParameter("basic", item.Basic));
                insCmd.Parameters.Add(new OracleParameter("doj", item.DateOfJoining ?? (object)DBNull.Value));
                insCmd.Parameters.Add(new OracleParameter("pres", item.PresentDays));
                insCmd.Parameters.Add(new OracleParameter("abs", item.AbsentDays));
                insCmd.Parameters.Add(new OracleParameter("leave", item.LeaveDays));
                insCmd.Parameters.Add(new OracleParameter("hol", item.HolidayDays));
                insCmd.Parameters.Add(new OracleParameter("wnd", item.WeekendDays));
                insCmd.Parameters.Add(new OracleParameter("earning", item.EarningSalary));
                insCmd.Parameters.Add(new OracleParameter("attBns", item.AttBonus));
                insCmd.Parameters.Add(new OracleParameter("ot", item.OverTimeHours));
                insCmd.Parameters.Add(new OracleParameter("otRate", item.OtRate));
                insCmd.Parameters.Add(new OracleParameter("otAmt", item.OtAmount));
                insCmd.Parameters.Add(new OracleParameter("night", item.NightDays));
                insCmd.Parameters.Add(new OracleParameter("nightAmt", item.NightAmount));
                insCmd.Parameters.Add(new OracleParameter("adv", item.AdvanceDeduction));
                insCmd.Parameters.Add(new OracleParameter("dues", item.DuesAmount));
                insCmd.Parameters.Add(new OracleParameter("ded", item.OtherDeduction + item.PunishmentDeduction));
                insCmd.Parameters.Add(new OracleParameter("stamp", item.Stamp));
                insCmd.Parameters.Add(new OracleParameter("totDed", item.TotalDeduction));
                insCmd.Parameters.Add(new OracleParameter("netPay", item.NetPayable));
                insCmd.Parameters.Add(new OracleParameter("payable", item.Payable));
                insCmd.Parameters.Add(new OracleParameter("bank", item.BankPayable));
                insCmd.Parameters.Add(new OracleParameter("cash", item.CashPayable));
                insCmd.Parameters.Add(new OracleParameter("netCash", item.NetCashPayable));
                insCmd.Parameters.Add(new OracleParameter("accNo", item.AccountNo ?? (object)DBNull.Value));
                insCmd.Parameters.Add(new OracleParameter("salFor", item.SalaryFor));
                await insCmd.ExecuteNonQueryAsync(ct);
            }

            await trans.CommitAsync(ct);
        }
        catch
        {
            await trans.RollbackAsync(ct);
            throw;
        }
    }

    public async Task DeleteMonthlySalaryAsync(DateTime salaryMonth, int? empId = null, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        var sql = "DELETE FROM SALARY WHERE TRUNC(SALARY_FOR, 'MM') = TRUNC(:salMonth, 'MM')";
        if (empId.HasValue && empId.Value > 0) sql += " AND EMP_ID = :empId";

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("salMonth", salaryMonth));
        if (empId.HasValue && empId.Value > 0) cmd.Parameters.Add(new OracleParameter("empId", empId.Value));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // =========================================================================
    // 2. ADVANCE DUES DEDUCTION
    // =========================================================================
    public async Task<List<AdvanceDuesDeductionItem>> GetAdvanceDuesDeductionsAsync(DateTime month, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var sql = """
            SELECT A.ADD_ID, A.EMP_ID, E.EMP_CODE, NVL(E.EMP_NAME, '') AS EMP_NAME,
                   NVL(D.DESIGNATION_NAME, '') AS DESIGNATION_NAME,
                   NVL(DEP.DEPARTMENT_NAME, '') AS DEPARTMENT_NAME,
                   A.ADD_TYPE, NVL(A.ADD_AMOUNT, 0) AS ADD_AMOUNT,
                   A.MONTH_YEAR, A.REMARKS
            FROM ADVANCE_DUES_DEDUCTION A
            LEFT JOIN EMP_OFFICIAL E ON A.EMP_ID = E.EMP_ID
            LEFT JOIN DESIGNATION D ON E.DESIGNATION_ID = D.DESIGNATION_ID
            LEFT JOIN DEPARTMENT DEP ON E.DEPARTMENT_ID = DEP.DEPARTMENT_ID
            WHERE TRUNC(A.MONTH_YEAR, 'MM') = TRUNC(:mMonth, 'MM')
            ORDER BY DEP.DEPARTMENT_NAME, TO_NUMBER(REGEXP_SUBSTR(E.EMP_CODE, '^[0-9]+'))
            """;

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("mMonth", month));

        var list = new List<AdvanceDuesDeductionItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new AdvanceDuesDeductionItem
            {
                AddId = Convert.ToInt32(reader["ADD_ID"]),
                EmpId = Convert.ToInt32(reader["EMP_ID"]),
                EmpCode = Convert.ToString(reader["EMP_CODE"]) ?? "",
                EmpName = Convert.ToString(reader["EMP_NAME"]) ?? "",
                DesignationName = Convert.ToString(reader["DESIGNATION_NAME"]) ?? "",
                DepartmentName = Convert.ToString(reader["DEPARTMENT_NAME"]) ?? "",
                AddType = Convert.ToString(reader["ADD_TYPE"]) ?? "Advance",
                AddAmount = Convert.ToDecimal(reader["ADD_AMOUNT"]),
                MonthYear = Convert.ToDateTime(reader["MONTH_YEAR"]),
                Remarks = Convert.ToString(reader["REMARKS"])
            });
        }
        return list;
    }

    public async Task SaveAdvanceDuesDeductionAsync(AdvanceDuesDeductionItem item, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        const string nextIdSql = "SELECT NVL(MAX(ADD_ID), 0) + 1 FROM ADVANCE_DUES_DEDUCTION";
        await using var idCmd = new OracleCommand(nextIdSql, conn);
        var nextId = Convert.ToInt32(await idCmd.ExecuteScalarAsync(ct));

        const string insSql = """
            INSERT INTO ADVANCE_DUES_DEDUCTION (
                ADD_ID, EMP_ID, ADD_TYPE, ADD_AMOUNT, MONTH_YEAR, REMARKS
            ) VALUES (
                :id, :empId, :addType, :amount, :mYear, :remarks
            )
            """;
        await using var cmd = new OracleCommand(insSql, conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("id", nextId));
        cmd.Parameters.Add(new OracleParameter("empId", item.EmpId));
        cmd.Parameters.Add(new OracleParameter("addType", item.AddType));
        cmd.Parameters.Add(new OracleParameter("amount", item.AddAmount));
        cmd.Parameters.Add(new OracleParameter("mYear", item.MonthYear));
        cmd.Parameters.Add(new OracleParameter("remarks", item.Remarks ?? (object)DBNull.Value));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAdvanceDuesDeductionAsync(int addId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        const string sql = "DELETE FROM ADVANCE_DUES_DEDUCTION WHERE ADD_ID = :id";
        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("id", addId));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // =========================================================================
    // 3. HOUR PUNISHMENT
    // =========================================================================
    public async Task<List<HourPunishmentItem>> GetHourPunishmentsAsync(DateTime month, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var sql = """
            SELECT P.DED_HOUR_ID, P.EMP_ID, P.EMP_CODE, NVL(E.EMP_NAME, '') AS EMP_NAME,
                   NVL(D.DESIGNATION_NAME, '') AS DESIGNATION_NAME,
                   NVL(DEP.DEPARTMENT_NAME, '') AS DEPARTMENT_NAME,
                   NVL(P.DED_HOURS, 0) AS DED_HOURS, P.DT, P.REMARKS
            FROM PUNISHMENT P
            LEFT JOIN EMP_OFFICIAL E ON P.EMP_ID = E.EMP_ID
            LEFT JOIN DESIGNATION D ON E.DESIGNATION_ID = D.DESIGNATION_ID
            LEFT JOIN DEPARTMENT DEP ON E.DEPARTMENT_ID = DEP.DEPARTMENT_ID
            WHERE TRUNC(P.DT, 'MM') = TRUNC(:mMonth, 'MM')
            ORDER BY P.DT DESC, TO_NUMBER(REGEXP_SUBSTR(P.EMP_CODE, '^[0-9]+'))
            """;

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("mMonth", month));

        var list = new List<HourPunishmentItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new HourPunishmentItem
            {
                DedHourId = Convert.ToInt32(reader["DED_HOUR_ID"]),
                EmpId = Convert.ToInt32(reader["EMP_ID"]),
                EmpCode = Convert.ToString(reader["EMP_CODE"]) ?? "",
                EmpName = Convert.ToString(reader["EMP_NAME"]) ?? "",
                DesignationName = Convert.ToString(reader["DESIGNATION_NAME"]) ?? "",
                DepartmentName = Convert.ToString(reader["DEPARTMENT_NAME"]) ?? "",
                DedHours = Convert.ToDecimal(reader["DED_HOURS"]),
                Dt = Convert.ToDateTime(reader["DT"]),
                Remarks = Convert.ToString(reader["REMARKS"])
            });
        }
        return list;
    }

    public async Task SaveHourPunishmentAsync(HourPunishmentItem item, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        const string nextIdSql = "SELECT NVL(MAX(DED_HOUR_ID), 0) + 1 FROM PUNISHMENT";
        await using var idCmd = new OracleCommand(nextIdSql, conn);
        var nextId = Convert.ToInt32(await idCmd.ExecuteScalarAsync(ct));

        const string insSql = """
            INSERT INTO PUNISHMENT (
                DED_HOUR_ID, EMP_ID, EMP_CODE, DED_HOURS, DT, REMARKS
            ) VALUES (
                :id, :empId, :empCode, :dedHours, :dt, :remarks
            )
            """;
        await using var cmd = new OracleCommand(insSql, conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("id", nextId));
        cmd.Parameters.Add(new OracleParameter("empId", item.EmpId));
        cmd.Parameters.Add(new OracleParameter("empCode", item.EmpCode));
        cmd.Parameters.Add(new OracleParameter("dedHours", item.DedHours));
        cmd.Parameters.Add(new OracleParameter("dt", item.Dt));
        cmd.Parameters.Add(new OracleParameter("remarks", item.Remarks ?? "LUNCH OUT"));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteHourPunishmentAsync(int dedHourId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        const string sql = "DELETE FROM PUNISHMENT WHERE DED_HOUR_ID = :id";
        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("id", dedHourId));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // =========================================================================
    // 4. MATERNITY LEAVE PROCESS
    // =========================================================================
    public async Task<List<MaternityLeaveItem>> GetMaternityLeavesAsync(CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var sql = """
            SELECT M.MATERNITY_ID, M.EMP_ID, E.EMP_CODE, NVL(E.EMP_NAME, '') AS EMP_NAME,
                   NVL(D.DESIGNATION_NAME, '') AS DESIGNATION_NAME,
                   NVL(DEP.DEPARTMENT_NAME, '') AS DEPARTMENT_NAME,
                   NVL(M.GROSS, E.GROSS) AS GROSS, NVL(M.BASIC, E.BASIC) AS BASIC,
                   M.FROM_DATE, M.TO_DATE, NVL(M.TOTAL_DAYS, 112) AS TOTAL_DAYS,
                   NVL(M.TOTAL_PAYABLE, 0) AS TOTAL_PAYABLE,
                   NVL(M.FIRST_PAYMENT, 0) AS FIRST_PAYMENT,
                   NVL(M.SECOND_PAYMENT, 0) AS SECOND_PAYMENT,
                   M.FIRST_PAYMENT_DATE, M.SECOND_PAYMENT_DATE,
                   NVL(M.STATUS, 'Active') AS STATUS, M.REMARKS
            FROM MATERNITY_LEAVE M
            JOIN EMP_OFFICIAL E ON M.EMP_ID = E.EMP_ID
            LEFT JOIN DESIGNATION D ON E.DESIGNATION_ID = D.DESIGNATION_ID
            LEFT JOIN DEPARTMENT DEP ON E.DEPARTMENT_ID = DEP.DEPARTMENT_ID
            ORDER BY M.FROM_DATE DESC
            """;

        var list = new List<MaternityLeaveItem>();
        await using var cmd = new OracleCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new MaternityLeaveItem
            {
                MaternityId = Convert.ToInt32(reader["MATERNITY_ID"]),
                EmpId = Convert.ToInt32(reader["EMP_ID"]),
                EmpCode = Convert.ToString(reader["EMP_CODE"]) ?? "",
                EmpName = Convert.ToString(reader["EMP_NAME"]) ?? "",
                DesignationName = Convert.ToString(reader["DESIGNATION_NAME"]) ?? "",
                DepartmentName = Convert.ToString(reader["DEPARTMENT_NAME"]) ?? "",
                Gross = Convert.ToDecimal(reader["GROSS"]),
                Basic = Convert.ToDecimal(reader["BASIC"]),
                FromDate = Convert.ToDateTime(reader["FROM_DATE"]),
                ToDate = Convert.ToDateTime(reader["TO_DATE"]),
                TotalDays = Convert.ToInt32(reader["TOTAL_DAYS"]),
                TotalPayable = Convert.ToDecimal(reader["TOTAL_PAYABLE"]),
                FirstPayment = Convert.ToDecimal(reader["FIRST_PAYMENT"]),
                SecondPayment = Convert.ToDecimal(reader["SECOND_PAYMENT"]),
                FirstPaymentDate = reader["FIRST_PAYMENT_DATE"] == DBNull.Value ? null : Convert.ToDateTime(reader["FIRST_PAYMENT_DATE"]),
                SecondPaymentDate = reader["SECOND_PAYMENT_DATE"] == DBNull.Value ? null : Convert.ToDateTime(reader["SECOND_PAYMENT_DATE"]),
                Status = Convert.ToString(reader["STATUS"]) ?? "Active",
                Remarks = Convert.ToString(reader["REMARKS"])
            });
        }
        return list;
    }

    public async Task SaveMaternityLeaveAsync(MaternityLeaveItem item, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var trans = conn.BeginTransaction();

        try
        {
            const string nextIdSql = "SELECT NVL(MAX(MATERNITY_ID), 0) + 1 FROM MATERNITY_LEAVE";
            await using var idCmd = new OracleCommand(nextIdSql, conn) { Transaction = trans };
            var nextId = Convert.ToInt32(await idCmd.ExecuteScalarAsync(ct));

            const string insSql = """
                INSERT INTO MATERNITY_LEAVE (
                    MATERNITY_ID, EMP_ID, GROSS, BASIC, FROM_DATE, TO_DATE,
                    TOTAL_DAYS, TOTAL_PAYABLE, FIRST_PAYMENT, SECOND_PAYMENT,
                    FIRST_PAYMENT_DATE, SECOND_PAYMENT_DATE, STATUS, REMARKS
                ) VALUES (
                    :id, :empId, :gross, :basic, :fromDate, :toDate,
                    :totDays, :totPay, :p1, :p2,
                    :p1Date, :p2Date, :status, :remarks
                )
                """;
            await using var insCmd = new OracleCommand(insSql, conn) { Transaction = trans, BindByName = true };
            insCmd.Parameters.Add(new OracleParameter("id", nextId));
            insCmd.Parameters.Add(new OracleParameter("empId", item.EmpId));
            insCmd.Parameters.Add(new OracleParameter("gross", item.Gross));
            insCmd.Parameters.Add(new OracleParameter("basic", item.Basic));
            insCmd.Parameters.Add(new OracleParameter("fromDate", item.FromDate));
            insCmd.Parameters.Add(new OracleParameter("toDate", item.ToDate));
            insCmd.Parameters.Add(new OracleParameter("totDays", item.TotalDays));
            insCmd.Parameters.Add(new OracleParameter("totPay", item.TotalPayable));
            insCmd.Parameters.Add(new OracleParameter("p1", item.FirstPayment));
            insCmd.Parameters.Add(new OracleParameter("p2", item.SecondPayment));
            insCmd.Parameters.Add(new OracleParameter("p1Date", item.FirstPaymentDate ?? (object)DBNull.Value));
            insCmd.Parameters.Add(new OracleParameter("p2Date", item.SecondPaymentDate ?? (object)DBNull.Value));
            insCmd.Parameters.Add(new OracleParameter("status", item.Status));
            insCmd.Parameters.Add(new OracleParameter("remarks", item.Remarks ?? (object)DBNull.Value));
            await insCmd.ExecuteNonQueryAsync(ct);

            // Update EMP_OFFICIAL status to 'Maternity'
            const string updEmpSql = "UPDATE EMP_OFFICIAL SET EMP_STATUS = 'Maternity' WHERE EMP_ID = :empId";
            await using var updCmd = new OracleCommand(updEmpSql, conn) { Transaction = trans, BindByName = true };
            updCmd.Parameters.Add(new OracleParameter("empId", item.EmpId));
            await updCmd.ExecuteNonQueryAsync(ct);

            await trans.CommitAsync(ct);
        }
        catch
        {
            await trans.RollbackAsync(ct);
            throw;
        }
    }

    // =========================================================================
    // 5. WEEKEND / MICRO LEAVE SETUP
    // =========================================================================
    public async Task<List<MicroLeaveItem>> GetMicroLeavesAsync(CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var sql = """
            SELECT W.WEEKEND_ID, W.EMP_ID, E.EMP_CODE, NVL(E.EMP_NAME, '') AS EMP_NAME,
                   NVL(D.DESIGNATION_NAME, '') AS DESIGNATION_NAME,
                   NVL(DEP.DEPARTMENT_NAME, '') AS DEPARTMENT_NAME,
                   W.WEEKEND, W.FROM_DATE, W.TO_DATE, NVL(W.TOTAL_DAYS, 1) AS TOTAL_DAYS,
                   W.REMARKS
            FROM WEEKEND_SETUP W
            LEFT JOIN EMP_OFFICIAL E ON W.EMP_ID = E.EMP_ID
            LEFT JOIN DESIGNATION D ON E.DESIGNATION_ID = D.DESIGNATION_ID
            LEFT JOIN DEPARTMENT DEP ON E.DEPARTMENT_ID = DEP.DEPARTMENT_ID
            ORDER BY W.FROM_DATE DESC
            """;

        var list = new List<MicroLeaveItem>();
        await using var cmd = new OracleCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new MicroLeaveItem
            {
                WeekendId = Convert.ToInt32(reader["WEEKEND_ID"]),
                EmpId = Convert.ToInt32(reader["EMP_ID"]),
                EmpCode = Convert.ToString(reader["EMP_CODE"]) ?? "",
                EmpName = Convert.ToString(reader["EMP_NAME"]) ?? "",
                DesignationName = Convert.ToString(reader["DESIGNATION_NAME"]) ?? "",
                DepartmentName = Convert.ToString(reader["DEPARTMENT_NAME"]) ?? "",
                WeekendType = Convert.ToString(reader["WEEKEND"]) ?? "Friday",
                FromDate = Convert.ToDateTime(reader["FROM_DATE"]),
                ToDate = Convert.ToDateTime(reader["TO_DATE"]),
                TotalDays = Convert.ToInt32(reader["TOTAL_DAYS"]),
                Remarks = Convert.ToString(reader["REMARKS"])
            });
        }
        return list;
    }

    public async Task SaveMicroLeaveAsync(MicroLeaveItem item, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        const string nextIdSql = "SELECT NVL(MAX(WEEKEND_ID), 0) + 1 FROM WEEKEND_SETUP";
        await using var idCmd = new OracleCommand(nextIdSql, conn);
        var nextId = Convert.ToInt32(await idCmd.ExecuteScalarAsync(ct));

        const string insSql = """
            INSERT INTO WEEKEND_SETUP (
                WEEKEND_ID, EMP_ID, WEEKEND, FROM_DATE, TO_DATE, TOTAL_DAYS, REMARKS
            ) VALUES (
                :id, :empId, :wType, :fromDate, :toDate, :totDays, :remarks
            )
            """;
        await using var cmd = new OracleCommand(insSql, conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("id", nextId));
        cmd.Parameters.Add(new OracleParameter("empId", item.EmpId));
        cmd.Parameters.Add(new OracleParameter("wType", item.WeekendType));
        cmd.Parameters.Add(new OracleParameter("fromDate", item.FromDate));
        cmd.Parameters.Add(new OracleParameter("toDate", item.ToDate));
        cmd.Parameters.Add(new OracleParameter("totDays", item.TotalDays));
        cmd.Parameters.Add(new OracleParameter("remarks", item.Remarks ?? (object)DBNull.Value));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // =========================================================================
    // 6. LEAVE PROCESS & POSTING
    // =========================================================================
    public async Task<List<LeaveEntryItem>> GetLeaveEntriesAsync(DateTime? fromDate, DateTime? toDate, string? empCode = null, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var sql = """
            SELECT L.LEAVE_ID, L.EMP_ID, NVL(L.EMP_CODE, E.EMP_CODE) AS EMP_CODE,
                   NVL(E.EMP_NAME, '') AS EMP_NAME,
                   NVL(D.DESIGNATION_NAME, '') AS DESIGNATION_NAME,
                   NVL(DEP.DEPARTMENT_NAME, '') AS DEPARTMENT_NAME,
                   L.TYPE AS LEAVE_TYPE, L.FROM_DATE, L.TO_DATE,
                   NVL(L.GRANT_DAYS, 1) AS GRANT_DAYS,
                   NVL(L.IS_OFFDAY, 'N') AS IS_OFFDAY,
                   L.REMARKS, L.LEAVE_ENTRY_DATE
            FROM LEAVE L
            LEFT JOIN EMP_OFFICIAL E ON L.EMP_ID = E.EMP_ID
            LEFT JOIN DESIGNATION D ON E.DESIGNATION_ID = D.DESIGNATION_ID
            LEFT JOIN DEPARTMENT DEP ON E.DEPARTMENT_ID = DEP.DEPARTMENT_ID
            WHERE 1 = 1
            """;

        if (fromDate.HasValue) sql += " AND L.FROM_DATE >= :fDate";
        if (toDate.HasValue) sql += " AND L.FROM_DATE <= :tDate";
        if (!string.IsNullOrWhiteSpace(empCode)) sql += " AND (E.EMP_CODE = :empCode OR L.EMP_CODE = :empCode)";
        sql += " ORDER BY L.FROM_DATE DESC, TO_NUMBER(REGEXP_SUBSTR(E.EMP_CODE, '^[0-9]+'))";

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        if (fromDate.HasValue) cmd.Parameters.Add(new OracleParameter("fDate", fromDate.Value));
        if (toDate.HasValue) cmd.Parameters.Add(new OracleParameter("tDate", toDate.Value));
        if (!string.IsNullOrWhiteSpace(empCode)) cmd.Parameters.Add(new OracleParameter("empCode", empCode.Trim()));

        var list = new List<LeaveEntryItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new LeaveEntryItem
            {
                LeaveId = Convert.ToInt32(reader["LEAVE_ID"]),
                EmpId = Convert.ToInt32(reader["EMP_ID"]),
                EmpCode = Convert.ToString(reader["EMP_CODE"]) ?? "",
                EmpName = Convert.ToString(reader["EMP_NAME"]) ?? "",
                DesignationName = Convert.ToString(reader["DESIGNATION_NAME"]) ?? "",
                DepartmentName = Convert.ToString(reader["DEPARTMENT_NAME"]) ?? "",
                LeaveType = Convert.ToString(reader["LEAVE_TYPE"]) ?? "CL",
                FromDate = Convert.ToDateTime(reader["FROM_DATE"]),
                ToDate = Convert.ToDateTime(reader["TO_DATE"]),
                GrantDays = Convert.ToInt32(reader["GRANT_DAYS"]),
                IsOffday = Convert.ToString(reader["IS_OFFDAY"]) ?? "N",
                Remarks = Convert.ToString(reader["REMARKS"]),
                LeaveEntryDate = reader["LEAVE_ENTRY_DATE"] == DBNull.Value ? DateTime.Today : Convert.ToDateTime(reader["LEAVE_ENTRY_DATE"])
            });
        }
        return list;
    }

    public async Task<LeaveBalanceInfo> GetEmployeeLeaveBalanceAsync(int empId, int year, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var yearStart = new DateTime(year, 1, 1);
        var yearEnd = new DateTime(year, 12, 31);

        const string sql = """
            SELECT 
                NVL(SUM(CASE WHEN UPPER(TYPE) = 'CL' THEN GRANT_DAYS ELSE 0 END), 0) AS TAKEN_CL,
                NVL(SUM(CASE WHEN UPPER(TYPE) IN ('SL', 'ML') THEN GRANT_DAYS ELSE 0 END), 0) AS TAKEN_SL,
                NVL(SUM(CASE WHEN UPPER(TYPE) = 'EL' THEN GRANT_DAYS ELSE 0 END), 0) AS TAKEN_EL
            FROM LEAVE
            WHERE EMP_ID = :empId
              AND FROM_DATE BETWEEN :yStart AND :yEnd
            """;

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("empId", empId));
        cmd.Parameters.Add(new OracleParameter("yStart", yearStart));
        cmd.Parameters.Add(new OracleParameter("yEnd", yearEnd));

        var bal = new LeaveBalanceInfo();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            bal.TakenCl = Convert.ToInt32(reader["TAKEN_CL"]);
            bal.TakenSl = Convert.ToInt32(reader["TAKEN_SL"]);
            bal.TakenEl = Convert.ToInt32(reader["TAKEN_EL"]);
        }
        return bal;
    }

    public async Task SaveLeaveEntryAsync(LeaveEntryItem item, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var trans = conn.BeginTransaction();

        try
        {
            const string nextIdSql = "SELECT NVL(MAX(LEAVE_ID), 0) + 1 FROM LEAVE";
            await using var idCmd = new OracleCommand(nextIdSql, conn) { Transaction = trans };
            var nextId = Convert.ToInt32(await idCmd.ExecuteScalarAsync(ct));

            const string insSql = """
                INSERT INTO LEAVE (
                    LEAVE_ID, EMP_ID, EMP_CODE, TYPE, FROM_DATE, TO_DATE,
                    GRANT_DAYS, IS_OFFDAY, REMARKS, LEAVE_ENTRY_DATE
                ) VALUES (
                    :id, :empId, :empCode, :lType, :fromDate, :toDate,
                    :grantDays, :isOff, :remarks, :entryDate
                )
                """;
            await using var insCmd = new OracleCommand(insSql, conn) { Transaction = trans, BindByName = true };
            insCmd.Parameters.Add(new OracleParameter("id", nextId));
            insCmd.Parameters.Add(new OracleParameter("empId", item.EmpId));
            insCmd.Parameters.Add(new OracleParameter("empCode", item.EmpCode));
            insCmd.Parameters.Add(new OracleParameter("lType", item.LeaveType));
            insCmd.Parameters.Add(new OracleParameter("fromDate", item.FromDate));
            insCmd.Parameters.Add(new OracleParameter("toDate", item.ToDate));
            insCmd.Parameters.Add(new OracleParameter("grantDays", item.GrantDays));
            insCmd.Parameters.Add(new OracleParameter("isOff", item.IsOffday));
            insCmd.Parameters.Add(new OracleParameter("remarks", item.Remarks ?? (object)DBNull.Value));
            insCmd.Parameters.Add(new OracleParameter("entryDate", DateTime.Today));
            await insCmd.ExecuteNonQueryAsync(ct);

            // Update ATTENDANCE_DETAILS for the leave range to STATUS='L'
            const string updAttdSql = """
                UPDATE ATTENDANCE_DETAILS 
                SET STATUS = 'L', STATUS2 = 'L', ATTD_REMARKS = :lType, ATTD_LOCKED = 'Y'
                WHERE EMP_ID = :empId AND ATTD_DATE BETWEEN :fromDate AND :toDate
                """;
            await using var updAttdCmd = new OracleCommand(updAttdSql, conn) { Transaction = trans, BindByName = true };
            updAttdCmd.Parameters.Add(new OracleParameter("lType", item.LeaveType));
            updAttdCmd.Parameters.Add(new OracleParameter("empId", item.EmpId));
            updAttdCmd.Parameters.Add(new OracleParameter("fromDate", item.FromDate));
            updAttdCmd.Parameters.Add(new OracleParameter("toDate", item.ToDate));
            await updAttdCmd.ExecuteNonQueryAsync(ct);

            await trans.CommitAsync(ct);
        }
        catch
        {
            await trans.RollbackAsync(ct);
            throw;
        }
    }

    public async Task DeleteLeaveEntryAsync(int leaveId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var trans = conn.BeginTransaction();

        try
        {
            const string getSql = "SELECT EMP_ID, FROM_DATE, TO_DATE FROM LEAVE WHERE LEAVE_ID = :id";
            await using var getCmd = new OracleCommand(getSql, conn) { Transaction = trans, BindByName = true };
            getCmd.Parameters.Add(new OracleParameter("id", leaveId));
            await using var reader = await getCmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var empId = Convert.ToInt32(reader["EMP_ID"]);
                var fromDate = Convert.ToDateTime(reader["FROM_DATE"]);
                var toDate = Convert.ToDateTime(reader["TO_DATE"]);

                const string delSql = "DELETE FROM LEAVE WHERE LEAVE_ID = :id";
                await using var delCmd = new OracleCommand(delSql, conn) { Transaction = trans, BindByName = true };
                delCmd.Parameters.Add(new OracleParameter("id", leaveId));
                await delCmd.ExecuteNonQueryAsync(ct);

                // Restore attendance
                const string updAttdSql = """
                    UPDATE ATTENDANCE_DETAILS 
                    SET STATUS = 'A', STATUS2 = 'A', ATTD_REMARKS = NULL, ATTD_LOCKED = 'N'
                    WHERE EMP_ID = :empId AND ATTD_DATE BETWEEN :fromDate AND :toDate AND STATUS = 'L'
                    """;
                await using var updAttdCmd = new OracleCommand(updAttdSql, conn) { Transaction = trans, BindByName = true };
                updAttdCmd.Parameters.Add(new OracleParameter("empId", empId));
                updAttdCmd.Parameters.Add(new OracleParameter("fromDate", fromDate));
                updAttdCmd.Parameters.Add(new OracleParameter("toDate", toDate));
                await updAttdCmd.ExecuteNonQueryAsync(ct);
            }

            await trans.CommitAsync(ct);
        }
        catch
        {
            await trans.RollbackAsync(ct);
            throw;
        }
    }

    // =========================================================================
    // 7. BINARY MANUAL QUERY RUNNER & COMPLIANCE AUDITOR
    // =========================================================================
    public async Task<DataTable> ExecuteBinaryQueryAsync(string sql, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("Please provide a valid SQL query.");

        var cleanSql = sql.Trim().TrimEnd(';', '/', ' ', '\t', '\r', '\n').Trim();
        if (string.IsNullOrWhiteSpace(cleanSql))
            throw new ArgumentException("Please provide a valid SQL query statement.");

        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new OracleCommand(cleanSql, conn);
        var dt = new DataTable();
        using var adapter = new OracleDataAdapter(cmd);
        adapter.Fill(dt);
        return dt;
    }
}
