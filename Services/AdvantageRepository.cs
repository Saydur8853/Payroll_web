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

public class AdvantageRepository
{
    private readonly string _connectionString;

    public AdvantageRepository(IConfiguration configuration)
    {
        _connectionString = DatabaseOptions.GetConnectionString(configuration);
    }

    private OracleConnection GetConnection() => new(_connectionString);

    // ==========================================
    // 1. ADVANCE SALARY PROCESS
    // ==========================================
    public async Task<List<AdvanceSalaryItem>> GetAdvanceSalariesAsync(DateTime advanceFor, int? unitId = null, int? deptId = null, int? secId = null, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var sql = """
            SELECT A.EMP_ID, A.EMP_CODE, NVL(E.EMP_NAME, '') AS EMP_NAME,
                   NVL(D.DESIGNATION_NAME, '') AS DESIGNATION_NAME,
                   NVL(DEP.DEPARTMENT_NAME, '') AS DEPARTMENT_NAME,
                   NVL(S.SECTION_NAME, '') AS SECTION_NAME,
                   NVL(L.LINE_NAME, '') AS LINE_NAME,
                   NVL(A.GROSS, 0) AS GROSS, NVL(A.BASIC, 0) AS BASIC,
                   NVL(A.PRESENT, 0) AS PRESENT, NVL(A.OVER_TIME, 0) AS OVER_TIME,
                   NVL(A.EARNING_SALARY, 0) AS EARNING_SALARY,
                   NVL(A.DEAR_ALLOW, 0) AS DEAR_ALLOW,
                   NVL(A.TOTAL_PAYABLE, 0) AS TOTAL_PAYABLE,
                   NVL(A.PAYABLE, 0) AS PAYABLE,
                   NVL(A.BANK_PAYABLE, 0) AS BANK_PAYABLE,
                   NVL(A.CASH_PAYABLE, 0) AS CASH_PAYABLE,
                   A.ADVANCE_FOR, NVL(A.ADVANCE_NAME, 'Advance') AS ADVANCE_NAME,
                   A.ACCOUNT_NO, A.ACC_TYPE, A.REMARKS
            FROM ADVANCE_SALARY A
            LEFT JOIN EMP_OFFICIAL E ON A.EMP_ID = E.EMP_ID
            LEFT JOIN DESIGNATION D ON E.DESIGNATION_ID = D.DESIGNATION_ID
            LEFT JOIN DEPARTMENT DEP ON E.DEPARTMENT_ID = DEP.DEPARTMENT_ID
            LEFT JOIN SECTION S ON E.SECTION_ID = S.SECTION_ID
            LEFT JOIN LINE L ON E.LINE_ID = L.LINE_ID
            WHERE TRUNC(A.ADVANCE_FOR, 'MM') = TRUNC(:advFor, 'MM')
            """;

        if (unitId.HasValue && unitId.Value > 0) sql += " AND E.UNIT_ID = :unitId";
        if (deptId.HasValue && deptId.Value > 0) sql += " AND E.DEPARTMENT_ID = :deptId";
        if (secId.HasValue && secId.Value > 0) sql += " AND E.SECTION_ID = :secId";
        sql += " ORDER BY DEP.DEPARTMENT_NAME, S.SECTION_NAME, TO_NUMBER(REGEXP_SUBSTR(A.EMP_CODE, '^[0-9]+'))";

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("advFor", advanceFor));
        if (unitId.HasValue && unitId.Value > 0) cmd.Parameters.Add(new OracleParameter("unitId", unitId.Value));
        if (deptId.HasValue && deptId.Value > 0) cmd.Parameters.Add(new OracleParameter("deptId", deptId.Value));
        if (secId.HasValue && secId.Value > 0) cmd.Parameters.Add(new OracleParameter("secId", secId.Value));

        var list = new List<AdvanceSalaryItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new AdvanceSalaryItem
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
                OverTimeHours = Convert.ToDecimal(reader["OVER_TIME"]),
                EarningSalary = Convert.ToDecimal(reader["EARNING_SALARY"]),
                DearnessAllowance = Convert.ToDecimal(reader["DEAR_ALLOW"]),
                TotalPayable = Convert.ToDecimal(reader["TOTAL_PAYABLE"]),
                AdvanceAmount = Convert.ToDecimal(reader["PAYABLE"]),
                BankPayable = Convert.ToDecimal(reader["BANK_PAYABLE"]),
                CashPayable = Convert.ToDecimal(reader["CASH_PAYABLE"]),
                AdvanceFor = reader["ADVANCE_FOR"] == DBNull.Value ? advanceFor : Convert.ToDateTime(reader["ADVANCE_FOR"]),
                AdvanceName = Convert.ToString(reader["ADVANCE_NAME"]) ?? "Monthly Advance",
                AccountNo = Convert.ToString(reader["ACCOUNT_NO"]),
                AccType = Convert.ToString(reader["ACC_TYPE"]),
                Remarks = Convert.ToString(reader["REMARKS"])
            });
        }
        return list;
    }

    public async Task<List<AdvanceSalaryItem>> CalculateAdvanceSalariesAsync(AdvantageFilter filter, int roundTo = 100, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var sql = """
            SELECT E.EMP_ID, E.EMP_CODE, NVL(E.EMP_NAME, '') AS EMP_NAME,
                   NVL(D.DESIGNATION_NAME, '') AS DESIGNATION_NAME,
                   NVL(DEP.DEPARTMENT_NAME, '') AS DEPARTMENT_NAME,
                   NVL(S.SECTION_NAME, '') AS SECTION_NAME,
                   NVL(L.LINE_NAME, '') AS LINE_NAME,
                   NVL(E.GROSS, 0) AS GROSS, NVL(E.BASIC, 0) AS BASIC,
                   NVL(E.ACCOUNT_NO, '') AS ACCOUNT_NO, NVL(E.ACC_TYPE, 'Cash') AS ACC_TYPE,
                   NVL((
                       SELECT COUNT(1) FROM ATTENDANCE_DETAILS AD 
                       WHERE AD.EMP_ID = E.EMP_ID 
                         AND AD.ATTD_DATE BETWEEN :startDate AND :endDate 
                         AND AD.STATUS IN ('P', 'L', 'W', 'H')
                   ), 0) AS PRESENT_DAYS,
                   NVL((
                       SELECT SUM(AD.OVER_TIME) FROM ATTENDANCE_DETAILS AD 
                       WHERE AD.EMP_ID = E.EMP_ID 
                         AND AD.ATTD_DATE BETWEEN :startDate AND :endDate
                   ), 0) AS OT_HOURS
            FROM EMP_OFFICIAL E
            LEFT JOIN DESIGNATION D ON E.DESIGNATION_ID = D.DESIGNATION_ID
            LEFT JOIN DEPARTMENT DEP ON E.DEPARTMENT_ID = DEP.DEPARTMENT_ID
            LEFT JOIN SECTION S ON E.SECTION_ID = S.SECTION_ID
            LEFT JOIN LINE L ON E.LINE_ID = L.LINE_ID
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
        cmd.Parameters.Add(new OracleParameter("startDate", filter.StartDate));
        cmd.Parameters.Add(new OracleParameter("endDate", filter.EndDate));
        if (filter.UnitId.HasValue && filter.UnitId.Value > 0) cmd.Parameters.Add(new OracleParameter("unitId", filter.UnitId.Value));
        if (filter.DepartmentId.HasValue && filter.DepartmentId.Value > 0) cmd.Parameters.Add(new OracleParameter("deptId", filter.DepartmentId.Value));
        if (filter.SectionId.HasValue && filter.SectionId.Value > 0) cmd.Parameters.Add(new OracleParameter("secId", filter.SectionId.Value));
        if (filter.LineId.HasValue && filter.LineId.Value > 0) cmd.Parameters.Add(new OracleParameter("lineId", filter.LineId.Value));
        if (filter.CategoryId.HasValue && filter.CategoryId.Value > 0) cmd.Parameters.Add(new OracleParameter("catId", filter.CategoryId.Value));
        if (!string.IsNullOrWhiteSpace(filter.EmpCode)) cmd.Parameters.Add(new OracleParameter("empCode", filter.EmpCode.Trim()));

        var daysInMonth = DateTime.DaysInMonth(filter.EffectDate.Year, filter.EffectDate.Month);
        var list = new List<AdvanceSalaryItem>();

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var gross = Convert.ToDecimal(reader["GROSS"]);
            var basic = Convert.ToDecimal(reader["BASIC"]);
            var presentDays = Convert.ToInt32(reader["PRESENT_DAYS"]);
            var otHours = Convert.ToDecimal(reader["OT_HOURS"]);
            var accType = Convert.ToString(reader["ACC_TYPE"]) ?? "Cash";

            // If basic is 0, estimate basic ~ 60% of gross
            if (basic <= 0) basic = Math.Round(gross * 0.6m, 2);

            var earning = daysInMonth > 0 ? Math.Round((gross / daysInMonth) * presentDays, 2) : 0;
            var payable = earning;

            // Rounding
            if (roundTo > 0 && payable > 0)
            {
                payable = Math.Floor(payable / roundTo) * roundTo;
            }

            var isBank = accType.Equals("Bank", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(Convert.ToString(reader["ACCOUNT_NO"]));
            var bankAmt = isBank ? payable : 0;
            var cashAmt = isBank ? 0 : payable;

            list.Add(new AdvanceSalaryItem
            {
                EmpId = Convert.ToInt32(reader["EMP_ID"]),
                EmpCode = Convert.ToString(reader["EMP_CODE"]) ?? "",
                EmpName = Convert.ToString(reader["EMP_NAME"]) ?? "",
                DesignationName = Convert.ToString(reader["DESIGNATION_NAME"]) ?? "",
                DepartmentName = Convert.ToString(reader["DEPARTMENT_NAME"]) ?? "",
                SectionName = Convert.ToString(reader["SECTION_NAME"]) ?? "",
                LineName = Convert.ToString(reader["LINE_NAME"]) ?? "",
                Gross = gross,
                Basic = basic,
                PresentDays = presentDays,
                OverTimeHours = otHours,
                EarningSalary = earning,
                TotalPayable = payable,
                AdvanceAmount = payable,
                BankPayable = bankAmt,
                CashPayable = cashAmt,
                AdvanceFor = filter.EffectDate,
                AdvanceName = string.IsNullOrWhiteSpace(filter.TitleOrType) ? "Monthly Advance" : filter.TitleOrType,
                AccountNo = Convert.ToString(reader["ACCOUNT_NO"]),
                AccType = accType
            });
        }
        return list;
    }

    public async Task SaveAdvanceSalariesAsync(List<AdvanceSalaryItem> items, CancellationToken ct = default)
    {
        if (items.Count == 0) return;
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var trans = conn.BeginTransaction();

        try
        {
            var first = items[0];
            const string delSql = "DELETE FROM ADVANCE_SALARY WHERE TRUNC(ADVANCE_FOR, 'MM') = TRUNC(:advFor, 'MM') AND EMP_ID = :empId";
            await using var delCmd = new OracleCommand(delSql, conn) { Transaction = trans, BindByName = true };

            const string insSql = """
                INSERT INTO ADVANCE_SALARY (
                    EMP_ID, EMP_CODE, GROSS, BASIC, PRESENT, OVER_TIME,
                    EARNING_SALARY, DEAR_ALLOW, TOTAL_PAYABLE, PAYABLE,
                    BANK_PAYABLE, CASH_PAYABLE, ADVANCE_FOR, ADVANCE_NAME,
                    ACCOUNT_NO, ACC_TYPE, REMARKS, IS_PAID
                ) VALUES (
                    :empId, :empCode, :gross, :basic, :present, :ot,
                    :earning, :dear, :total, :payable,
                    :bank, :cash, :advFor, :advName,
                    :accNo, :accType, :remarks, 'N'
                )
                """;
            await using var insCmd = new OracleCommand(insSql, conn) { Transaction = trans, BindByName = true };

            foreach (var item in items.Where(i => i.IsSelected))
            {
                delCmd.Parameters.Clear();
                delCmd.Parameters.Add(new OracleParameter("advFor", item.AdvanceFor));
                delCmd.Parameters.Add(new OracleParameter("empId", item.EmpId));
                await delCmd.ExecuteNonQueryAsync(ct);

                insCmd.Parameters.Clear();
                insCmd.Parameters.Add(new OracleParameter("empId", item.EmpId));
                insCmd.Parameters.Add(new OracleParameter("empCode", item.EmpCode));
                insCmd.Parameters.Add(new OracleParameter("gross", item.Gross));
                insCmd.Parameters.Add(new OracleParameter("basic", item.Basic));
                insCmd.Parameters.Add(new OracleParameter("present", item.PresentDays));
                insCmd.Parameters.Add(new OracleParameter("ot", item.OverTimeHours));
                insCmd.Parameters.Add(new OracleParameter("earning", item.EarningSalary));
                insCmd.Parameters.Add(new OracleParameter("dear", item.DearnessAllowance));
                insCmd.Parameters.Add(new OracleParameter("total", item.TotalPayable));
                insCmd.Parameters.Add(new OracleParameter("payable", item.AdvanceAmount));
                insCmd.Parameters.Add(new OracleParameter("bank", item.BankPayable));
                insCmd.Parameters.Add(new OracleParameter("cash", item.CashPayable));
                insCmd.Parameters.Add(new OracleParameter("advFor", item.AdvanceFor));
                insCmd.Parameters.Add(new OracleParameter("advName", item.AdvanceName));
                insCmd.Parameters.Add(new OracleParameter("accNo", item.AccountNo ?? (object)DBNull.Value));
                insCmd.Parameters.Add(new OracleParameter("accType", item.AccType ?? (object)DBNull.Value));
                insCmd.Parameters.Add(new OracleParameter("remarks", item.Remarks ?? (object)DBNull.Value));
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

    public async Task DeleteAdvanceSalariesAsync(DateTime advanceFor, int? empId = null, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        var sql = "DELETE FROM ADVANCE_SALARY WHERE TRUNC(ADVANCE_FOR, 'MM') = TRUNC(:advFor, 'MM')";
        if (empId.HasValue && empId.Value > 0) sql += " AND EMP_ID = :empId";

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("advFor", advanceFor));
        if (empId.HasValue && empId.Value > 0) cmd.Parameters.Add(new OracleParameter("empId", empId.Value));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ==========================================
    // 2. FESTIVAL BONUS PROCESS
    // ==========================================
    public async Task<List<FestivalBonusItem>> GetFestivalBonusesAsync(DateTime festivalFor, int? unitId = null, int? deptId = null, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var sql = """
            SELECT B.BONUS_ID, B.EMP_ID, E.EMP_CODE, NVL(E.EMP_NAME, '') AS EMP_NAME,
                   NVL(D.DESIGNATION_NAME, '') AS DESIGNATION_NAME,
                   NVL(DEP.DEPARTMENT_NAME, '') AS DEPARTMENT_NAME,
                   NVL(S.SECTION_NAME, '') AS SECTION_NAME,
                   NVL(L.LINE_NAME, '') AS LINE_NAME,
                   NVL(B.GROSS, 0) AS GROSS, NVL(B.BASIC, 0) AS BASIC,
                   NVL(B.BONUS, 0) AS BONUS, NVL(B.STAMP, 10) AS STAMP,
                   NVL(B.BANK_PAYABLE, 0) AS BANK_PAYABLE,
                   NVL(B.CASH_PAYABLE, 0) AS CASH_PAYABLE,
                   NVL(B.NET_CASH_PAYABLE, 0) AS NET_CASH_PAYABLE,
                   B.FESTIVAL_FOR, NVL(B.FESTIVAL_NAME, 'Festival Bonus') AS FESTIVAL_NAME,
                   B.BONUS_DESCRIPTION, B.SERVICE_PERIOD, B.ACCOUNT_NO, B.ACC_TYPE
            FROM FESTIVAL_BONUS B
            LEFT JOIN EMP_OFFICIAL E ON B.EMP_ID = E.EMP_ID
            LEFT JOIN DESIGNATION D ON E.DESIGNATION_ID = D.DESIGNATION_ID
            LEFT JOIN DEPARTMENT DEP ON E.DEPARTMENT_ID = DEP.DEPARTMENT_ID
            LEFT JOIN SECTION S ON E.SECTION_ID = S.SECTION_ID
            LEFT JOIN LINE L ON E.LINE_ID = L.LINE_ID
            WHERE TRUNC(B.FESTIVAL_FOR, 'MM') = TRUNC(:festFor, 'MM')
            """;

        if (unitId.HasValue && unitId.Value > 0) sql += " AND E.UNIT_ID = :unitId";
        if (deptId.HasValue && deptId.Value > 0) sql += " AND E.DEPARTMENT_ID = :deptId";
        sql += " ORDER BY DEP.DEPARTMENT_NAME, S.SECTION_NAME, TO_NUMBER(REGEXP_SUBSTR(E.EMP_CODE, '^[0-9]+'))";

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("festFor", festivalFor));
        if (unitId.HasValue && unitId.Value > 0) cmd.Parameters.Add(new OracleParameter("unitId", unitId.Value));
        if (deptId.HasValue && deptId.Value > 0) cmd.Parameters.Add(new OracleParameter("deptId", deptId.Value));

        var list = new List<FestivalBonusItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new FestivalBonusItem
            {
                BonusId = Convert.ToInt32(reader["BONUS_ID"]),
                EmpId = Convert.ToInt32(reader["EMP_ID"]),
                EmpCode = Convert.ToString(reader["EMP_CODE"]) ?? "",
                EmpName = Convert.ToString(reader["EMP_NAME"]) ?? "",
                DesignationName = Convert.ToString(reader["DESIGNATION_NAME"]) ?? "",
                DepartmentName = Convert.ToString(reader["DEPARTMENT_NAME"]) ?? "",
                SectionName = Convert.ToString(reader["SECTION_NAME"]) ?? "",
                LineName = Convert.ToString(reader["LINE_NAME"]) ?? "",
                Gross = Convert.ToDecimal(reader["GROSS"]),
                Basic = Convert.ToDecimal(reader["BASIC"]),
                BonusAmount = Convert.ToDecimal(reader["BONUS"]),
                Stamp = Convert.ToDecimal(reader["STAMP"]),
                BankPayable = Convert.ToDecimal(reader["BANK_PAYABLE"]),
                CashPayable = Convert.ToDecimal(reader["CASH_PAYABLE"]),
                NetCashPayable = Convert.ToDecimal(reader["NET_CASH_PAYABLE"]),
                FestivalFor = reader["FESTIVAL_FOR"] == DBNull.Value ? festivalFor : Convert.ToDateTime(reader["FESTIVAL_FOR"]),
                FestivalName = Convert.ToString(reader["FESTIVAL_NAME"]) ?? "Bonus",
                BonusDescription = Convert.ToString(reader["BONUS_DESCRIPTION"]),
                ServicePeriod = Convert.ToString(reader["SERVICE_PERIOD"]),
                AccountNo = Convert.ToString(reader["ACCOUNT_NO"]),
                AccType = Convert.ToString(reader["ACC_TYPE"])
            });
        }
        return list;
    }

    public async Task<List<FestivalBonusItem>> CalculateFestivalBonusesAsync(AdvantageFilter filter, string bonusBasis = "BASIC_100", CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var sql = """
            SELECT E.EMP_ID, E.EMP_CODE, NVL(E.EMP_NAME, '') AS EMP_NAME,
                   NVL(D.DESIGNATION_NAME, '') AS DESIGNATION_NAME,
                   NVL(DEP.DEPARTMENT_NAME, '') AS DEPARTMENT_NAME,
                   NVL(S.SECTION_NAME, '') AS SECTION_NAME,
                   NVL(L.LINE_NAME, '') AS LINE_NAME,
                   NVL(E.GROSS, 0) AS GROSS, NVL(E.BASIC, 0) AS BASIC,
                   E.DATE_OF_JOINING,
                   NVL(E.ACCOUNT_NO, '') AS ACCOUNT_NO, NVL(E.ACC_TYPE, 'Cash') AS ACC_TYPE
            FROM EMP_OFFICIAL E
            LEFT JOIN DESIGNATION D ON E.DESIGNATION_ID = D.DESIGNATION_ID
            LEFT JOIN DEPARTMENT DEP ON E.DEPARTMENT_ID = DEP.DEPARTMENT_ID
            LEFT JOIN SECTION S ON E.SECTION_ID = S.SECTION_ID
            LEFT JOIN LINE L ON E.LINE_ID = L.LINE_ID
            WHERE E.EMP_STATUS IN ('Active', 'Maternity')
            """;

        if (filter.UnitId.HasValue && filter.UnitId.Value > 0) sql += " AND E.UNIT_ID = :unitId";
        if (filter.DepartmentId.HasValue && filter.DepartmentId.Value > 0) sql += " AND E.DEPARTMENT_ID = :deptId";
        if (filter.SectionId.HasValue && filter.SectionId.Value > 0) sql += " AND E.SECTION_ID = :secId";
        if (filter.CategoryId.HasValue && filter.CategoryId.Value > 0) sql += " AND E.EMP_CATEGORY_ID = :catId";
        if (!string.IsNullOrWhiteSpace(filter.EmpCode)) sql += " AND E.EMP_CODE = :empCode";

        sql += " ORDER BY DEP.DEPARTMENT_NAME, S.SECTION_NAME, TO_NUMBER(REGEXP_SUBSTR(E.EMP_CODE, '^[0-9]+'))";

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        if (filter.UnitId.HasValue && filter.UnitId.Value > 0) cmd.Parameters.Add(new OracleParameter("unitId", filter.UnitId.Value));
        if (filter.DepartmentId.HasValue && filter.DepartmentId.Value > 0) cmd.Parameters.Add(new OracleParameter("deptId", filter.DepartmentId.Value));
        if (filter.SectionId.HasValue && filter.SectionId.Value > 0) cmd.Parameters.Add(new OracleParameter("secId", filter.SectionId.Value));
        if (filter.CategoryId.HasValue && filter.CategoryId.Value > 0) cmd.Parameters.Add(new OracleParameter("catId", filter.CategoryId.Value));
        if (!string.IsNullOrWhiteSpace(filter.EmpCode)) cmd.Parameters.Add(new OracleParameter("empCode", filter.EmpCode.Trim()));

        var list = new List<FestivalBonusItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var gross = Convert.ToDecimal(reader["GROSS"]);
            var basic = Convert.ToDecimal(reader["BASIC"]);
            if (basic <= 0) basic = Math.Round(gross * 0.6m, 2);

            DateTime? joinDate = reader["DATE_OF_JOINING"] == DBNull.Value ? null : Convert.ToDateTime(reader["DATE_OF_JOINING"]);
            var servicePeriod = "N/A";
            if (joinDate.HasValue)
            {
                var days = (filter.EffectDate - joinDate.Value).TotalDays;
                var months = Math.Max(0, (int)(days / 30.4375));
                var years = months / 12;
                var remMonths = months % 12;
                servicePeriod = years > 0 ? $"{years}y {remMonths}m" : $"{remMonths}m";
            }

            decimal bonus = 0;
            if (bonusBasis == "BASIC_100") bonus = basic;
            else if (bonusBasis == "BASIC_50") bonus = Math.Round(basic * 0.5m, 2);
            else if (bonusBasis == "GROSS_50") bonus = Math.Round(gross * 0.5m, 2);
            else if (bonusBasis == "GROSS_100") bonus = gross;
            else if (filter.PercentageOrAmount > 0) bonus = Math.Round(basic * (filter.PercentageOrAmount / 100m), 2);
            else bonus = basic;

            var accType = Convert.ToString(reader["ACC_TYPE"]) ?? "Cash";
            var isBank = accType.Equals("Bank", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(Convert.ToString(reader["ACCOUNT_NO"]));
            var stamp = 10m;
            var bankPayable = isBank ? bonus : 0;
            var cashPayable = isBank ? 0 : bonus;
            var netCashPayable = isBank ? 0 : Math.Max(0, cashPayable - stamp);

            list.Add(new FestivalBonusItem
            {
                EmpId = Convert.ToInt32(reader["EMP_ID"]),
                EmpCode = Convert.ToString(reader["EMP_CODE"]) ?? "",
                EmpName = Convert.ToString(reader["EMP_NAME"]) ?? "",
                DesignationName = Convert.ToString(reader["DESIGNATION_NAME"]) ?? "",
                DepartmentName = Convert.ToString(reader["DEPARTMENT_NAME"]) ?? "",
                SectionName = Convert.ToString(reader["SECTION_NAME"]) ?? "",
                LineName = Convert.ToString(reader["LINE_NAME"]) ?? "",
                Gross = gross,
                Basic = basic,
                BonusAmount = bonus,
                Stamp = stamp,
                BankPayable = bankPayable,
                CashPayable = cashPayable,
                NetCashPayable = netCashPayable,
                FestivalFor = filter.EffectDate,
                FestivalName = string.IsNullOrWhiteSpace(filter.TitleOrType) ? "Eid-ul-Fitr" : filter.TitleOrType,
                ServicePeriod = servicePeriod,
                AccountNo = Convert.ToString(reader["ACCOUNT_NO"]),
                AccType = accType
            });
        }
        return list;
    }

    public async Task SaveFestivalBonusesAsync(List<FestivalBonusItem> items, CancellationToken ct = default)
    {
        if (items.Count == 0) return;
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var trans = conn.BeginTransaction();

        try
        {
            const string nextIdSql = "SELECT NVL(MAX(BONUS_ID), 0) + 1 FROM FESTIVAL_BONUS";
            await using var nextIdCmd = new OracleCommand(nextIdSql, conn) { Transaction = trans };
            var currentId = Convert.ToInt32(await nextIdCmd.ExecuteScalarAsync(ct));

            const string delSql = "DELETE FROM FESTIVAL_BONUS WHERE TRUNC(FESTIVAL_FOR, 'MM') = TRUNC(:festFor, 'MM') AND EMP_ID = :empId";
            await using var delCmd = new OracleCommand(delSql, conn) { Transaction = trans, BindByName = true };

            const string insSql = """
                INSERT INTO FESTIVAL_BONUS (
                    BONUS_ID, EMP_ID, GROSS, BASIC, BONUS, STAMP,
                    BANK_PAYABLE, CASH_PAYABLE, NET_CASH_PAYABLE,
                    FESTIVAL_FOR, FESTIVAL_NAME, BONUS_DESCRIPTION,
                    SERVICE_PERIOD, ACCOUNT_NO, ACC_TYPE
                ) VALUES (
                    :bId, :empId, :gross, :basic, :bonus, :stamp,
                    :bank, :cash, :netCash,
                    :festFor, :festName, :bDesc,
                    :serv, :accNo, :accType
                )
                """;
            await using var insCmd = new OracleCommand(insSql, conn) { Transaction = trans, BindByName = true };

            foreach (var item in items.Where(i => i.IsSelected))
            {
                delCmd.Parameters.Clear();
                delCmd.Parameters.Add(new OracleParameter("festFor", item.FestivalFor));
                delCmd.Parameters.Add(new OracleParameter("empId", item.EmpId));
                await delCmd.ExecuteNonQueryAsync(ct);

                insCmd.Parameters.Clear();
                insCmd.Parameters.Add(new OracleParameter("bId", currentId++));
                insCmd.Parameters.Add(new OracleParameter("empId", item.EmpId));
                insCmd.Parameters.Add(new OracleParameter("gross", item.Gross));
                insCmd.Parameters.Add(new OracleParameter("basic", item.Basic));
                insCmd.Parameters.Add(new OracleParameter("bonus", item.BonusAmount));
                insCmd.Parameters.Add(new OracleParameter("stamp", item.Stamp));
                insCmd.Parameters.Add(new OracleParameter("bank", item.BankPayable));
                insCmd.Parameters.Add(new OracleParameter("cash", item.CashPayable));
                insCmd.Parameters.Add(new OracleParameter("netCash", item.NetCashPayable));
                insCmd.Parameters.Add(new OracleParameter("festFor", item.FestivalFor));
                insCmd.Parameters.Add(new OracleParameter("festName", item.FestivalName));
                insCmd.Parameters.Add(new OracleParameter("bDesc", item.BonusDescription ?? (object)DBNull.Value));
                insCmd.Parameters.Add(new OracleParameter("serv", item.ServicePeriod ?? (object)DBNull.Value));
                insCmd.Parameters.Add(new OracleParameter("accNo", item.AccountNo ?? (object)DBNull.Value));
                insCmd.Parameters.Add(new OracleParameter("accType", item.AccType ?? (object)DBNull.Value));
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

    public async Task DeleteFestivalBonusesAsync(DateTime festivalFor, int? empId = null, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        var sql = "DELETE FROM FESTIVAL_BONUS WHERE TRUNC(FESTIVAL_FOR, 'MM') = TRUNC(:festFor, 'MM')";
        if (empId.HasValue && empId.Value > 0) sql += " AND EMP_ID = :empId";

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("festFor", festivalFor));
        if (empId.HasValue && empId.Value > 0) cmd.Parameters.Add(new OracleParameter("empId", empId.Value));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ==========================================
    // 3. INCREMENT ENTRY
    // ==========================================
    public async Task<List<IncrementItem>> GetIncrementsAsync(AdvantageFilter filter, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var sql = """
            SELECT I.INCREMENT_ID, I.EMP_ID, E.EMP_CODE, NVL(E.EMP_NAME, '') AS EMP_NAME,
                   NVL(D.DESIGNATION_NAME, '') AS DESIGNATION_NAME,
                   NVL(DEP.DEPARTMENT_NAME, '') AS DEPARTMENT_NAME,
                   NVL(S.SECTION_NAME, '') AS SECTION_NAME,
                   E.EMP_GRADE, E.DATE_OF_JOINING,
                   NVL(I.PRE_GROSS, E.GROSS) AS PRE_GROSS,
                   NVL(I.PRE_BASIC, E.BASIC) AS PRE_BASIC,
                   NVL(I.INCREMENT_AMOUNT, 0) AS INCREMENT_AMOUNT,
                   NVL(I.PROM_GROSS, E.GROSS) AS PROM_GROSS,
                   NVL(I.PROM_BASIC, E.BASIC) AS PROM_BASIC,
                   NVL(I.EFFECTIVE_DATE, SYSDATE) AS EFFECTIVE_DATE,
                   NVL(I.INCREMENT_TYPE, 'INCREMENT') AS INCREMENT_TYPE,
                   NVL(I.INCR_STATUS, 'CONFIRM') AS INCR_STATUS,
                   I.LAST_INCR_DATE, NVL(I.LAST_INCR_AMOUNT, 0) AS LAST_INCR_AMOUNT,
                   I.INCR_DESC, I.SERVICE_PERIOD
            FROM INCREMENT_TBL I
            JOIN EMP_OFFICIAL E ON I.EMP_ID = E.EMP_ID
            LEFT JOIN DESIGNATION D ON E.DESIGNATION_ID = D.DESIGNATION_ID
            LEFT JOIN DEPARTMENT DEP ON E.DEPARTMENT_ID = DEP.DEPARTMENT_ID
            LEFT JOIN SECTION S ON E.SECTION_ID = S.SECTION_ID
            WHERE I.INCREMENT_TYPE = 'INCREMENT'
            """;

        if (filter.DepartmentId.HasValue && filter.DepartmentId.Value > 0) sql += " AND E.DEPARTMENT_ID = :deptId";
        if (filter.SectionId.HasValue && filter.SectionId.Value > 0) sql += " AND E.SECTION_ID = :secId";
        if (!string.IsNullOrWhiteSpace(filter.EmpCode)) sql += " AND E.EMP_CODE = :empCode";

        sql += " ORDER BY I.EFFECTIVE_DATE DESC, TO_NUMBER(REGEXP_SUBSTR(E.EMP_CODE, '^[0-9]+'))";

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        if (filter.DepartmentId.HasValue && filter.DepartmentId.Value > 0) cmd.Parameters.Add(new OracleParameter("deptId", filter.DepartmentId.Value));
        if (filter.SectionId.HasValue && filter.SectionId.Value > 0) cmd.Parameters.Add(new OracleParameter("secId", filter.SectionId.Value));
        if (!string.IsNullOrWhiteSpace(filter.EmpCode)) cmd.Parameters.Add(new OracleParameter("empCode", filter.EmpCode.Trim()));

        var list = new List<IncrementItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new IncrementItem
            {
                IncrementId = Convert.ToInt32(reader["INCREMENT_ID"]),
                EmpId = Convert.ToInt32(reader["EMP_ID"]),
                EmpCode = Convert.ToString(reader["EMP_CODE"]) ?? "",
                EmpName = Convert.ToString(reader["EMP_NAME"]) ?? "",
                DesignationName = Convert.ToString(reader["DESIGNATION_NAME"]) ?? "",
                DepartmentName = Convert.ToString(reader["DEPARTMENT_NAME"]) ?? "",
                SectionName = Convert.ToString(reader["SECTION_NAME"]) ?? "",
                Grade = Convert.ToString(reader["EMP_GRADE"]),
                JoiningDate = reader["DATE_OF_JOINING"] == DBNull.Value ? null : Convert.ToDateTime(reader["DATE_OF_JOINING"]),
                PreGross = Convert.ToDecimal(reader["PRE_GROSS"]),
                PreBasic = Convert.ToDecimal(reader["PRE_BASIC"]),
                IncrementAmount = Convert.ToDecimal(reader["INCREMENT_AMOUNT"]),
                PromotedGross = Convert.ToDecimal(reader["PROM_GROSS"]),
                PromotedBasic = Convert.ToDecimal(reader["PROM_BASIC"]),
                EffectiveDate = Convert.ToDateTime(reader["EFFECTIVE_DATE"]),
                IncrementType = Convert.ToString(reader["INCREMENT_TYPE"]) ?? "INCREMENT",
                IncrementStatus = Convert.ToString(reader["INCR_STATUS"]) ?? "CONFIRM",
                LastIncrDate = Convert.ToString(reader["LAST_INCR_DATE"]),
                LastIncrAmount = Convert.ToDecimal(reader["LAST_INCR_AMOUNT"]),
                IncrDesc = Convert.ToString(reader["INCR_DESC"]),
                ServicePeriod = Convert.ToString(reader["SERVICE_PERIOD"])
            });
        }
        return list;
    }

    public async Task<List<IncrementItem>> PrepareIncrementBatchAsync(AdvantageFilter filter, decimal incAmountOrPercent, bool isPercentage = false, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var sql = """
            SELECT E.EMP_ID, E.EMP_CODE, NVL(E.EMP_NAME, '') AS EMP_NAME,
                   NVL(D.DESIGNATION_NAME, '') AS DESIGNATION_NAME,
                   NVL(DEP.DEPARTMENT_NAME, '') AS DEPARTMENT_NAME,
                   NVL(S.SECTION_NAME, '') AS SECTION_NAME,
                   E.EMP_GRADE, E.DATE_OF_JOINING,
                   NVL(E.GROSS, 0) AS GROSS, NVL(E.BASIC, 0) AS BASIC,
                   (SELECT MAX(I.INCREMENT_DATE) FROM INCREMENT_TBL I WHERE I.EMP_ID = E.EMP_ID) AS LAST_INCR_DATE,
                   (SELECT NVL(SUM(I.INCREMENT_AMOUNT), 0) FROM INCREMENT_TBL I WHERE I.EMP_ID = E.EMP_ID AND I.INCREMENT_ID = (SELECT MAX(I2.INCREMENT_ID) FROM INCREMENT_TBL I2 WHERE I2.EMP_ID = E.EMP_ID)) AS LAST_INCR_AMT
            FROM EMP_OFFICIAL E
            LEFT JOIN DESIGNATION D ON E.DESIGNATION_ID = D.DESIGNATION_ID
            LEFT JOIN DEPARTMENT DEP ON E.DEPARTMENT_ID = DEP.DEPARTMENT_ID
            LEFT JOIN SECTION S ON E.SECTION_ID = S.SECTION_ID
            WHERE E.EMP_STATUS IN ('Active', 'Maternity')
            """;

        if (filter.UnitId.HasValue && filter.UnitId.Value > 0) sql += " AND E.UNIT_ID = :unitId";
        if (filter.DepartmentId.HasValue && filter.DepartmentId.Value > 0) sql += " AND E.DEPARTMENT_ID = :deptId";
        if (filter.SectionId.HasValue && filter.SectionId.Value > 0) sql += " AND E.SECTION_ID = :secId";
        if (filter.CategoryId.HasValue && filter.CategoryId.Value > 0) sql += " AND E.EMP_CATEGORY_ID = :catId";
        if (!string.IsNullOrWhiteSpace(filter.EmpCode)) sql += " AND E.EMP_CODE = :empCode";

        sql += " ORDER BY DEP.DEPARTMENT_NAME, S.SECTION_NAME, TO_NUMBER(REGEXP_SUBSTR(E.EMP_CODE, '^[0-9]+'))";

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        if (filter.UnitId.HasValue && filter.UnitId.Value > 0) cmd.Parameters.Add(new OracleParameter("unitId", filter.UnitId.Value));
        if (filter.DepartmentId.HasValue && filter.DepartmentId.Value > 0) cmd.Parameters.Add(new OracleParameter("deptId", filter.DepartmentId.Value));
        if (filter.SectionId.HasValue && filter.SectionId.Value > 0) cmd.Parameters.Add(new OracleParameter("secId", filter.SectionId.Value));
        if (filter.CategoryId.HasValue && filter.CategoryId.Value > 0) cmd.Parameters.Add(new OracleParameter("catId", filter.CategoryId.Value));
        if (!string.IsNullOrWhiteSpace(filter.EmpCode)) cmd.Parameters.Add(new OracleParameter("empCode", filter.EmpCode.Trim()));

        var list = new List<IncrementItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var gross = Convert.ToDecimal(reader["GROSS"]);
            var basic = Convert.ToDecimal(reader["BASIC"]);
            if (basic <= 0) basic = Math.Round(gross * 0.6m, 2);

            decimal incAmt = isPercentage ? Math.Round(gross * (incAmountOrPercent / 100m), 0) : incAmountOrPercent;
            var newGross = gross + incAmt;
            var newBasic = Math.Round(basic + (incAmt * 0.6m), 0);

            list.Add(new IncrementItem
            {
                EmpId = Convert.ToInt32(reader["EMP_ID"]),
                EmpCode = Convert.ToString(reader["EMP_CODE"]) ?? "",
                EmpName = Convert.ToString(reader["EMP_NAME"]) ?? "",
                DesignationName = Convert.ToString(reader["DESIGNATION_NAME"]) ?? "",
                DepartmentName = Convert.ToString(reader["DEPARTMENT_NAME"]) ?? "",
                SectionName = Convert.ToString(reader["SECTION_NAME"]) ?? "",
                Grade = Convert.ToString(reader["EMP_GRADE"]),
                JoiningDate = reader["DATE_OF_JOINING"] == DBNull.Value ? null : Convert.ToDateTime(reader["DATE_OF_JOINING"]),
                PreGross = gross,
                PreBasic = basic,
                IncrementAmount = incAmt,
                PromotedGross = newGross,
                PromotedBasic = newBasic,
                EffectiveDate = filter.EffectDate,
                IncrementType = "INCREMENT",
                IncrementStatus = "CONFIRM",
                LastIncrDate = reader["LAST_INCR_DATE"] == DBNull.Value ? null : Convert.ToDateTime(reader["LAST_INCR_DATE"]).ToString("dd-MMM-yyyy"),
                LastIncrAmount = Convert.ToDecimal(reader["LAST_INCR_AMT"]),
                IncrDesc = string.IsNullOrWhiteSpace(filter.TitleOrType) ? "Yearly Increment" : filter.TitleOrType
            });
        }
        return list;
    }

    public async Task SaveIncrementsAsync(List<IncrementItem> items, CancellationToken ct = default)
    {
        if (items.Count == 0) return;
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var trans = conn.BeginTransaction();

        try
        {
            const string nextIdSql = "SELECT NVL(MAX(INCREMENT_ID), 0) + 1 FROM INCREMENT_TBL";
            await using var nextIdCmd = new OracleCommand(nextIdSql, conn) { Transaction = trans };
            var currentId = Convert.ToInt32(await nextIdCmd.ExecuteScalarAsync(ct));

            const string insSql = """
                INSERT INTO INCREMENT_TBL (
                    INCREMENT_ID, EMP_ID, INCREMENT_AMOUNT, INCREMENT_DATE,
                    INCREMENT_TYPE, PRE_DESIGNATION, PRE_GRADE, PRE_GROSS,
                    PRE_BASIC, PROM_GROSS, PROM_BASIC, EFFECTIVE_DATE,
                    INCR_STATUS, LAST_INCR_AMOUNT, LAST_INCR_DATE, INCR_DESC
                ) VALUES (
                    :incId, :empId, :incAmt, :incDate,
                    :incType, :preDesig, :preGrade, :preGross,
                    :preBasic, :promGross, :promBasic, :effDate,
                    :status, :lastAmt, :lastDate, :descr
                )
                """;
            await using var insCmd = new OracleCommand(insSql, conn) { Transaction = trans, BindByName = true };

            const string updateEmpSql = "UPDATE EMP_OFFICIAL SET GROSS = :gross, BASIC = :basic WHERE EMP_ID = :empId";
            await using var updCmd = new OracleCommand(updateEmpSql, conn) { Transaction = trans, BindByName = true };

            foreach (var item in items.Where(i => i.IsSelected))
            {
                insCmd.Parameters.Clear();
                insCmd.Parameters.Add(new OracleParameter("incId", currentId++));
                insCmd.Parameters.Add(new OracleParameter("empId", item.EmpId));
                insCmd.Parameters.Add(new OracleParameter("incAmt", item.IncrementAmount));
                insCmd.Parameters.Add(new OracleParameter("incDate", DateTime.Today));
                insCmd.Parameters.Add(new OracleParameter("incType", item.IncrementType));
                insCmd.Parameters.Add(new OracleParameter("preDesig", item.DesignationName));
                insCmd.Parameters.Add(new OracleParameter("preGrade", item.Grade ?? (object)DBNull.Value));
                insCmd.Parameters.Add(new OracleParameter("preGross", item.PreGross));
                insCmd.Parameters.Add(new OracleParameter("preBasic", item.PreBasic));
                insCmd.Parameters.Add(new OracleParameter("promGross", item.PromotedGross));
                insCmd.Parameters.Add(new OracleParameter("promBasic", item.PromotedBasic));
                insCmd.Parameters.Add(new OracleParameter("effDate", item.EffectiveDate));
                insCmd.Parameters.Add(new OracleParameter("status", item.IncrementStatus));
                insCmd.Parameters.Add(new OracleParameter("lastAmt", item.LastIncrAmount));
                insCmd.Parameters.Add(new OracleParameter("lastDate", item.LastIncrDate ?? (object)DBNull.Value));
                insCmd.Parameters.Add(new OracleParameter("descr", item.IncrDesc ?? (object)DBNull.Value));
                await insCmd.ExecuteNonQueryAsync(ct);

                // If CONFIRM, update official employee gross
                if (item.IncrementStatus == "CONFIRM")
                {
                    updCmd.Parameters.Clear();
                    updCmd.Parameters.Add(new OracleParameter("gross", item.PromotedGross));
                    updCmd.Parameters.Add(new OracleParameter("basic", item.PromotedBasic));
                    updCmd.Parameters.Add(new OracleParameter("empId", item.EmpId));
                    await updCmd.ExecuteNonQueryAsync(ct);
                }
            }

            await trans.CommitAsync(ct);
        }
        catch
        {
            await trans.RollbackAsync(ct);
            throw;
        }
    }

    public async Task DeleteIncrementAsync(int incrementId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var trans = conn.BeginTransaction();

        try
        {
            // Fetch the increment record to optionally restore pre_gross
            const string getSql = "SELECT EMP_ID, PRE_GROSS, PRE_BASIC, INCR_STATUS FROM INCREMENT_TBL WHERE INCREMENT_ID = :id";
            await using var getCmd = new OracleCommand(getSql, conn) { Transaction = trans, BindByName = true };
            getCmd.Parameters.Add(new OracleParameter("id", incrementId));
            await using var reader = await getCmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var empId = Convert.ToInt32(reader["EMP_ID"]);
                var preGross = Convert.ToDecimal(reader["PRE_GROSS"]);
                var preBasic = Convert.ToDecimal(reader["PRE_BASIC"]);

                const string delSql = "DELETE FROM INCREMENT_TBL WHERE INCREMENT_ID = :id";
                await using var delCmd = new OracleCommand(delSql, conn) { Transaction = trans, BindByName = true };
                delCmd.Parameters.Add(new OracleParameter("id", incrementId));
                await delCmd.ExecuteNonQueryAsync(ct);

                const string updSql = "UPDATE EMP_OFFICIAL SET GROSS = :gross, BASIC = :basic WHERE EMP_ID = :empId";
                await using var updCmd = new OracleCommand(updSql, conn) { Transaction = trans, BindByName = true };
                updCmd.Parameters.Add(new OracleParameter("gross", preGross));
                updCmd.Parameters.Add(new OracleParameter("basic", preBasic));
                updCmd.Parameters.Add(new OracleParameter("empId", empId));
                await updCmd.ExecuteNonQueryAsync(ct);
            }

            await trans.CommitAsync(ct);
        }
        catch
        {
            await trans.RollbackAsync(ct);
            throw;
        }
    }

    // ==========================================
    // 4. PROMOTION ENTRY
    // ==========================================
    public async Task<List<PromotionItem>> GetPromotionsAsync(AdvantageFilter filter, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var sql = """
            SELECT I.INCREMENT_ID, I.EMP_ID, E.EMP_CODE, NVL(E.EMP_NAME, '') AS EMP_NAME,
                   NVL(I.PRE_DESIGNATION, '') AS PRE_DESIGNATION,
                   NVL(I.PRE_GRADE, '') AS PRE_GRADE,
                   NVL(I.PRE_GROSS, 0) AS PRE_GROSS,
                   NVL(I.PROM_DESIGNATION, '') AS PROM_DESIGNATION,
                   NVL(I.PROM_GRADE, '') AS PROM_GRADE,
                   NVL(I.PROM_GROSS, 0) AS PROM_GROSS,
                   NVL(I.INCREMENT_AMOUNT, 0) AS PROMOTION_AMOUNT,
                   NVL(I.EFFECTIVE_DATE, SYSDATE) AS EFFECTIVE_DATE,
                   NVL(I.INCREMENT_DATE, SYSDATE) AS PROMOTION_DATE,
                   NVL(I.INCR_STATUS, 'CONFIRM') AS INCR_STATUS,
                   I.INCR_DESC,
                   NVL(DEP.DEPARTMENT_NAME, '') AS DEPARTMENT_NAME
            FROM INCREMENT_TBL I
            JOIN EMP_OFFICIAL E ON I.EMP_ID = E.EMP_ID
            LEFT JOIN DEPARTMENT DEP ON E.DEPARTMENT_ID = DEP.DEPARTMENT_ID
            WHERE I.INCREMENT_TYPE = 'PROMOTION'
            """;

        if (filter.DepartmentId.HasValue && filter.DepartmentId.Value > 0) sql += " AND E.DEPARTMENT_ID = :deptId";
        if (!string.IsNullOrWhiteSpace(filter.EmpCode)) sql += " AND E.EMP_CODE = :empCode";
        sql += " ORDER BY I.EFFECTIVE_DATE DESC";

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        if (filter.DepartmentId.HasValue && filter.DepartmentId.Value > 0) cmd.Parameters.Add(new OracleParameter("deptId", filter.DepartmentId.Value));
        if (!string.IsNullOrWhiteSpace(filter.EmpCode)) cmd.Parameters.Add(new OracleParameter("empCode", filter.EmpCode.Trim()));

        var list = new List<PromotionItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new PromotionItem
            {
                IncrementId = Convert.ToInt32(reader["INCREMENT_ID"]),
                EmpId = Convert.ToInt32(reader["EMP_ID"]),
                EmpCode = Convert.ToString(reader["EMP_CODE"]) ?? "",
                EmpName = Convert.ToString(reader["EMP_NAME"]) ?? "",
                PreDesignation = Convert.ToString(reader["PRE_DESIGNATION"]) ?? "",
                PreGrade = Convert.ToString(reader["PRE_GRADE"]),
                PreGross = Convert.ToDecimal(reader["PRE_GROSS"]),
                PromDesignation = Convert.ToString(reader["PROM_DESIGNATION"]) ?? "",
                PromGrade = Convert.ToString(reader["PROM_GRADE"]),
                PromGross = Convert.ToDecimal(reader["PROM_GROSS"]),
                PromotionAmount = Convert.ToDecimal(reader["PROMOTION_AMOUNT"]),
                EffectiveDate = Convert.ToDateTime(reader["EFFECTIVE_DATE"]),
                PromotionDate = Convert.ToDateTime(reader["PROMOTION_DATE"]),
                IncrStatus = Convert.ToString(reader["INCR_STATUS"]) ?? "CONFIRM",
                IncrDesc = Convert.ToString(reader["INCR_DESC"]),
                DepartmentName = Convert.ToString(reader["DEPARTMENT_NAME"])
            });
        }
        return list;
    }

    public async Task SavePromotionAsync(PromotionItem item, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var trans = conn.BeginTransaction();

        try
        {
            const string nextIdSql = "SELECT NVL(MAX(INCREMENT_ID), 0) + 1 FROM INCREMENT_TBL";
            await using var nextIdCmd = new OracleCommand(nextIdSql, conn) { Transaction = trans };
            var nextId = Convert.ToInt32(await nextIdCmd.ExecuteScalarAsync(ct));

            const string insSql = """
                INSERT INTO INCREMENT_TBL (
                    INCREMENT_ID, EMP_ID, INCREMENT_AMOUNT, INCREMENT_DATE,
                    INCREMENT_TYPE, PRE_DESIGNATION, PRE_GRADE, PRE_GROSS,
                    PROM_DESIGNATION, PROM_GRADE, PROM_GROSS, PROM_DESIGNATION_ID,
                    EFFECTIVE_DATE, INCR_STATUS, INCR_DESC
                ) VALUES (
                    :incId, :empId, :incAmt, :incDate,
                    'PROMOTION', :preDesig, :preGrade, :preGross,
                    :promDesig, :promGrade, :promGross, :promDesigId,
                    :effDate, 'CONFIRM', :descr
                )
                """;
            await using var insCmd = new OracleCommand(insSql, conn) { Transaction = trans, BindByName = true };
            insCmd.Parameters.Add(new OracleParameter("incId", nextId));
            insCmd.Parameters.Add(new OracleParameter("empId", item.EmpId));
            insCmd.Parameters.Add(new OracleParameter("incAmt", item.PromotionAmount));
            insCmd.Parameters.Add(new OracleParameter("incDate", DateTime.Today));
            insCmd.Parameters.Add(new OracleParameter("preDesig", item.PreDesignation));
            insCmd.Parameters.Add(new OracleParameter("preGrade", item.PreGrade ?? (object)DBNull.Value));
            insCmd.Parameters.Add(new OracleParameter("preGross", item.PreGross));
            insCmd.Parameters.Add(new OracleParameter("promDesig", item.PromDesignation));
            insCmd.Parameters.Add(new OracleParameter("promGrade", item.PromGrade ?? (object)DBNull.Value));
            insCmd.Parameters.Add(new OracleParameter("promGross", item.PromGross));
            insCmd.Parameters.Add(new OracleParameter("promDesigId", item.PromDesignationId));
            insCmd.Parameters.Add(new OracleParameter("effDate", item.EffectiveDate));
            insCmd.Parameters.Add(new OracleParameter("descr", item.IncrDesc ?? (object)DBNull.Value));
            await insCmd.ExecuteNonQueryAsync(ct);

            const string updEmpSql = """
                UPDATE EMP_OFFICIAL 
                SET GROSS = :gross, DESIGNATION_ID = :desigId, EMP_GRADE = :grade 
                WHERE EMP_ID = :empId
                """;
            await using var updCmd = new OracleCommand(updEmpSql, conn) { Transaction = trans, BindByName = true };
            updCmd.Parameters.Add(new OracleParameter("gross", item.PromGross));
            updCmd.Parameters.Add(new OracleParameter("desigId", item.PromDesignationId));
            updCmd.Parameters.Add(new OracleParameter("grade", item.PromGrade ?? (object)DBNull.Value));
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

    // ==========================================
    // 5. EARN LEAVE PROCESS
    // ==========================================
    public async Task<List<EarnLeaveProcessItem>> GetEarnLeavesAsync(DateTime processDate, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var sql = """
            SELECT EL.EMP_ID, EL.EMP_CODE, NVL(E.EMP_NAME, '') AS EMP_NAME,
                   NVL(D.DESIGNATION_NAME, '') AS DESIGNATION_NAME,
                   NVL(DEP.DEPARTMENT_NAME, '') AS DEPARTMENT_NAME,
                   NVL(S.SECTION_NAME, '') AS SECTION_NAME,
                   NVL(EL.GROSS, E.GROSS) AS GROSS,
                   NVL(E.BASIC, 0) AS BASIC,
                   NVL(EL.EL_SEGMENT, 'Worker EL') AS EL_SEGMENT,
                   EL.EL_PROCESS_DATE, EL.FROM_DATE, EL.LAST_COUNTING_DATE,
                   NVL(EL.TOTAL_DAYS, 0) AS TOTAL_DAYS,
                   NVL(EL.TOTAL_EL, 0) AS TOTAL_EL,
                   NVL(EL.EL_TAKEN, 0) AS EL_TAKEN,
                   NVL(EL.EL_RATE, 0) AS EL_RATE,
                   NVL(EL.NET_PAYABLE, 0) AS NET_PAYABLE,
                   EL.REMARKS
            FROM EARN_LEAVE_PROCESS EL
            LEFT JOIN EMP_OFFICIAL E ON EL.EMP_ID = E.EMP_ID
            LEFT JOIN DESIGNATION D ON E.DESIGNATION_ID = D.DESIGNATION_ID
            LEFT JOIN DEPARTMENT DEP ON E.DEPARTMENT_ID = DEP.DEPARTMENT_ID
            LEFT JOIN SECTION S ON E.SECTION_ID = S.SECTION_ID
            WHERE TRUNC(EL.EL_PROCESS_DATE, 'MM') = TRUNC(:procDate, 'MM')
            ORDER BY DEP.DEPARTMENT_NAME, S.SECTION_NAME, TO_NUMBER(REGEXP_SUBSTR(EL.EMP_CODE, '^[0-9]+'))
            """;

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("procDate", processDate));

        var list = new List<EarnLeaveProcessItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var totalEl = Convert.ToInt32(reader["TOTAL_EL"]);
            var elTaken = Convert.ToInt32(reader["EL_TAKEN"]);
            list.Add(new EarnLeaveProcessItem
            {
                EmpId = Convert.ToInt32(reader["EMP_ID"]),
                EmpCode = Convert.ToString(reader["EMP_CODE"]) ?? "",
                EmpName = Convert.ToString(reader["EMP_NAME"]) ?? "",
                DesignationName = Convert.ToString(reader["DESIGNATION_NAME"]) ?? "",
                DepartmentName = Convert.ToString(reader["DEPARTMENT_NAME"]) ?? "",
                SectionName = Convert.ToString(reader["SECTION_NAME"]) ?? "",
                Gross = Convert.ToDecimal(reader["GROSS"]),
                Basic = Convert.ToDecimal(reader["BASIC"]),
                ElSegment = Convert.ToString(reader["EL_SEGMENT"]) ?? "Worker EL",
                ElProcessDate = reader["EL_PROCESS_DATE"] == DBNull.Value ? processDate : Convert.ToDateTime(reader["EL_PROCESS_DATE"]),
                FromDate = reader["FROM_DATE"] == DBNull.Value ? processDate.AddMonths(-12) : Convert.ToDateTime(reader["FROM_DATE"]),
                LastCountingDate = reader["LAST_COUNTING_DATE"] == DBNull.Value ? processDate : Convert.ToDateTime(reader["LAST_COUNTING_DATE"]),
                TotalPresentDays = Convert.ToInt32(reader["TOTAL_DAYS"]),
                TotalElDays = totalEl,
                ElTaken = elTaken,
                NetElDays = Math.Max(0, totalEl - elTaken),
                ElRate = Convert.ToDecimal(reader["EL_RATE"]),
                NetPayable = Convert.ToDecimal(reader["NET_PAYABLE"]),
                Remarks = Convert.ToString(reader["REMARKS"])
            });
        }
        return list;
    }

    public async Task<List<EarnLeaveProcessItem>> CalculateEarnLeavesAsync(AdvantageFilter filter, int daysPerEl = 18, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var sql = """
            SELECT E.EMP_ID, E.EMP_CODE, NVL(E.EMP_NAME, '') AS EMP_NAME,
                   NVL(D.DESIGNATION_NAME, '') AS DESIGNATION_NAME,
                   NVL(DEP.DEPARTMENT_NAME, '') AS DEPARTMENT_NAME,
                   NVL(S.SECTION_NAME, '') AS SECTION_NAME,
                   NVL(E.GROSS, 0) AS GROSS, NVL(E.BASIC, 0) AS BASIC,
                   NVL((
                       SELECT COUNT(1) FROM ATTENDANCE_DETAILS AD 
                       WHERE AD.EMP_ID = E.EMP_ID 
                         AND AD.ATTD_DATE BETWEEN :startDate AND :endDate 
                         AND AD.STATUS IN ('P', 'L', 'W', 'H')
                   ), 0) AS TOTAL_PRESENT,
                   NVL((
                       SELECT NVL(SUM(GRANT_DAYS), 0) FROM LEAVE LV 
                       WHERE LV.EMP_ID = E.EMP_ID 
                         AND LV.FROM_DATE BETWEEN :startDate AND :endDate
                   ), 0) AS EL_TAKEN
            FROM EMP_OFFICIAL E
            LEFT JOIN DESIGNATION D ON E.DESIGNATION_ID = D.DESIGNATION_ID
            LEFT JOIN DEPARTMENT DEP ON E.DEPARTMENT_ID = DEP.DEPARTMENT_ID
            LEFT JOIN SECTION S ON E.SECTION_ID = S.SECTION_ID
            WHERE E.EMP_STATUS IN ('Active', 'Maternity')
            """;

        if (filter.UnitId.HasValue && filter.UnitId.Value > 0) sql += " AND E.UNIT_ID = :unitId";
        if (filter.DepartmentId.HasValue && filter.DepartmentId.Value > 0) sql += " AND E.DEPARTMENT_ID = :deptId";
        if (filter.SectionId.HasValue && filter.SectionId.Value > 0) sql += " AND E.SECTION_ID = :secId";
        if (!string.IsNullOrWhiteSpace(filter.EmpCode)) sql += " AND E.EMP_CODE = :empCode";

        sql += " ORDER BY DEP.DEPARTMENT_NAME, S.SECTION_NAME, TO_NUMBER(REGEXP_SUBSTR(E.EMP_CODE, '^[0-9]+'))";

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("startDate", filter.StartDate));
        cmd.Parameters.Add(new OracleParameter("endDate", filter.EndDate));
        if (filter.UnitId.HasValue && filter.UnitId.Value > 0) cmd.Parameters.Add(new OracleParameter("unitId", filter.UnitId.Value));
        if (filter.DepartmentId.HasValue && filter.DepartmentId.Value > 0) cmd.Parameters.Add(new OracleParameter("deptId", filter.DepartmentId.Value));
        if (filter.SectionId.HasValue && filter.SectionId.Value > 0) cmd.Parameters.Add(new OracleParameter("secId", filter.SectionId.Value));
        if (!string.IsNullOrWhiteSpace(filter.EmpCode)) cmd.Parameters.Add(new OracleParameter("empCode", filter.EmpCode.Trim()));

        var list = new List<EarnLeaveProcessItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var gross = Convert.ToDecimal(reader["GROSS"]);
            var basic = Convert.ToDecimal(reader["BASIC"]);
            var presentDays = Convert.ToInt32(reader["TOTAL_PRESENT"]);
            var elTaken = Convert.ToInt32(reader["EL_TAKEN"]);

            var totalEl = daysPerEl > 0 ? (presentDays / daysPerEl) : 0;
            var netEl = Math.Max(0, totalEl - elTaken);
            var elRate = Math.Round(gross / 30m, 2);
            var netPayable = Math.Round(netEl * elRate, 2);

            list.Add(new EarnLeaveProcessItem
            {
                EmpId = Convert.ToInt32(reader["EMP_ID"]),
                EmpCode = Convert.ToString(reader["EMP_CODE"]) ?? "",
                EmpName = Convert.ToString(reader["EMP_NAME"]) ?? "",
                DesignationName = Convert.ToString(reader["DESIGNATION_NAME"]) ?? "",
                DepartmentName = Convert.ToString(reader["DEPARTMENT_NAME"]) ?? "",
                SectionName = Convert.ToString(reader["SECTION_NAME"]) ?? "",
                Gross = gross,
                Basic = basic,
                ElSegment = string.IsNullOrWhiteSpace(filter.TitleOrType) ? "Worker EL" : filter.TitleOrType,
                ElProcessDate = filter.EffectDate,
                FromDate = filter.StartDate,
                LastCountingDate = filter.EndDate,
                TotalPresentDays = presentDays,
                TotalElDays = totalEl,
                ElTaken = elTaken,
                NetElDays = netEl,
                ElRate = elRate,
                NetPayable = netPayable
            });
        }
        return list;
    }

    public async Task SaveEarnLeavesAsync(List<EarnLeaveProcessItem> items, CancellationToken ct = default)
    {
        if (items.Count == 0) return;
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var trans = conn.BeginTransaction();

        try
        {
            const string delSql = "DELETE FROM EARN_LEAVE_PROCESS WHERE TRUNC(EL_PROCESS_DATE, 'MM') = TRUNC(:procDate, 'MM') AND EMP_ID = :empId";
            await using var delCmd = new OracleCommand(delSql, conn) { Transaction = trans, BindByName = true };

            const string insSql = """
                INSERT INTO EARN_LEAVE_PROCESS (
                    EMP_ID, EMP_CODE, GROSS, EL_SEGMENT, EL_PROCESS_DATE,
                    FROM_DATE, LAST_COUNTING_DATE, TOTAL_DAYS, TOTAL_EL,
                    EL_TAKEN, EL_RATE, NET_PAYABLE, REMARKS
                ) VALUES (
                    :empId, :empCode, :gross, :segment, :procDate,
                    :fromDate, :lastDate, :totDays, :totEl,
                    :elTaken, :elRate, :netPay, :remarks
                )
                """;
            await using var insCmd = new OracleCommand(insSql, conn) { Transaction = trans, BindByName = true };

            foreach (var item in items.Where(i => i.IsSelected))
            {
                delCmd.Parameters.Clear();
                delCmd.Parameters.Add(new OracleParameter("procDate", item.ElProcessDate));
                delCmd.Parameters.Add(new OracleParameter("empId", item.EmpId));
                await delCmd.ExecuteNonQueryAsync(ct);

                insCmd.Parameters.Clear();
                insCmd.Parameters.Add(new OracleParameter("empId", item.EmpId));
                insCmd.Parameters.Add(new OracleParameter("empCode", item.EmpCode));
                insCmd.Parameters.Add(new OracleParameter("gross", item.Gross));
                insCmd.Parameters.Add(new OracleParameter("segment", item.ElSegment));
                insCmd.Parameters.Add(new OracleParameter("procDate", item.ElProcessDate));
                insCmd.Parameters.Add(new OracleParameter("fromDate", item.FromDate));
                insCmd.Parameters.Add(new OracleParameter("lastDate", item.LastCountingDate));
                insCmd.Parameters.Add(new OracleParameter("totDays", item.TotalPresentDays));
                insCmd.Parameters.Add(new OracleParameter("totEl", item.TotalElDays));
                insCmd.Parameters.Add(new OracleParameter("elTaken", item.ElTaken));
                insCmd.Parameters.Add(new OracleParameter("elRate", item.ElRate));
                insCmd.Parameters.Add(new OracleParameter("netPay", item.NetPayable));
                insCmd.Parameters.Add(new OracleParameter("remarks", item.Remarks ?? (object)DBNull.Value));
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

    public async Task DeleteEarnLeavesAsync(DateTime processDate, int? empId = null, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        var sql = "DELETE FROM EARN_LEAVE_PROCESS WHERE TRUNC(EL_PROCESS_DATE, 'MM') = TRUNC(:procDate, 'MM')";
        if (empId.HasValue && empId.Value > 0) sql += " AND EMP_ID = :empId";

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("procDate", processDate));
        if (empId.HasValue && empId.Value > 0) cmd.Parameters.Add(new OracleParameter("empId", empId.Value));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ==========================================
    // 6. ALLOWANCE PROCESS (SALARY_SETUP)
    // ==========================================
    public async Task<List<AllowanceItem>> GetAllowancesAsync(AdvantageFilter filter, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var sql = """
            SELECT E.EMP_ID, E.EMP_CODE, NVL(E.EMP_NAME, '') AS EMP_NAME,
                   NVL(D.DESIGNATION_NAME, '') AS DESIGNATION_NAME,
                   NVL(DEP.DEPARTMENT_NAME, '') AS DEPARTMENT_NAME,
                   NVL(E.GROSS, 0) AS GROSS, NVL(E.BASIC, 0) AS BASIC,
                   NVL(S.BANK_AMOUNT, 0) AS BANK_AMOUNT,
                   NVL(S.CASH_AMOUNT, 0) AS CASH_AMOUNT,
                   NVL(S.ALLOWANCE_AMOUNT, 0) AS ALLOWANCE_AMOUNT,
                   NVL(S.TAX_AMOUNT, 0) AS TAX_AMOUNT,
                   S.ALLOW_DATE, NVL(S.STATUS, 'Y') AS STATUS
            FROM EMP_OFFICIAL E
            LEFT JOIN SALARY_SETUP S ON E.EMP_ID = S.EMP_ID
            LEFT JOIN DESIGNATION D ON E.DESIGNATION_ID = D.DESIGNATION_ID
            LEFT JOIN DEPARTMENT DEP ON E.DEPARTMENT_ID = DEP.DEPARTMENT_ID
            WHERE E.EMP_STATUS IN ('Active', 'Maternity')
            """;

        if (filter.DepartmentId.HasValue && filter.DepartmentId.Value > 0) sql += " AND E.DEPARTMENT_ID = :deptId";
        if (filter.SectionId.HasValue && filter.SectionId.Value > 0) sql += " AND E.SECTION_ID = :secId";
        if (!string.IsNullOrWhiteSpace(filter.EmpCode)) sql += " AND E.EMP_CODE = :empCode";

        sql += " ORDER BY DEP.DEPARTMENT_NAME, TO_NUMBER(REGEXP_SUBSTR(E.EMP_CODE, '^[0-9]+'))";

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        if (filter.DepartmentId.HasValue && filter.DepartmentId.Value > 0) cmd.Parameters.Add(new OracleParameter("deptId", filter.DepartmentId.Value));
        if (filter.SectionId.HasValue && filter.SectionId.Value > 0) cmd.Parameters.Add(new OracleParameter("secId", filter.SectionId.Value));
        if (!string.IsNullOrWhiteSpace(filter.EmpCode)) cmd.Parameters.Add(new OracleParameter("empCode", filter.EmpCode.Trim()));

        var list = new List<AllowanceItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new AllowanceItem
            {
                EmpId = Convert.ToInt32(reader["EMP_ID"]),
                EmpCode = Convert.ToString(reader["EMP_CODE"]) ?? "",
                EmpName = Convert.ToString(reader["EMP_NAME"]) ?? "",
                DesignationName = Convert.ToString(reader["DESIGNATION_NAME"]) ?? "",
                DepartmentName = Convert.ToString(reader["DEPARTMENT_NAME"]) ?? "",
                Gross = Convert.ToDecimal(reader["GROSS"]),
                Basic = Convert.ToDecimal(reader["BASIC"]),
                BankAmount = Convert.ToDecimal(reader["BANK_AMOUNT"]),
                CashAmount = Convert.ToDecimal(reader["CASH_AMOUNT"]),
                AllowanceAmount = Convert.ToDecimal(reader["ALLOWANCE_AMOUNT"]),
                TaxAmount = Convert.ToDecimal(reader["TAX_AMOUNT"]),
                AllowDate = reader["ALLOW_DATE"] == DBNull.Value ? null : Convert.ToDateTime(reader["ALLOW_DATE"]),
                Status = Convert.ToString(reader["STATUS"]) ?? "Y"
            });
        }
        return list;
    }

    public async Task SaveAllowancesAsync(List<AllowanceItem> items, CancellationToken ct = default)
    {
        if (items.Count == 0) return;
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var trans = conn.BeginTransaction();

        try
        {
            const string delSql = "DELETE FROM SALARY_SETUP WHERE EMP_ID = :empId";
            await using var delCmd = new OracleCommand(delSql, conn) { Transaction = trans, BindByName = true };

            const string insSql = """
                INSERT INTO SALARY_SETUP (
                    EMP_ID, BANK_AMOUNT, CASH_AMOUNT, ALLOWANCE_AMOUNT,
                    TAX_AMOUNT, ALLOW_DATE, STATUS, FLAG
                ) VALUES (
                    :empId, :bank, :cash, :allow,
                    :tax, :allowDate, 'Y', 'Y'
                )
                """;
            await using var insCmd = new OracleCommand(insSql, conn) { Transaction = trans, BindByName = true };

            foreach (var item in items.Where(i => i.IsSelected))
            {
                delCmd.Parameters.Clear();
                delCmd.Parameters.Add(new OracleParameter("empId", item.EmpId));
                await delCmd.ExecuteNonQueryAsync(ct);

                insCmd.Parameters.Clear();
                insCmd.Parameters.Add(new OracleParameter("empId", item.EmpId));
                insCmd.Parameters.Add(new OracleParameter("bank", item.BankAmount));
                insCmd.Parameters.Add(new OracleParameter("cash", item.CashAmount));
                insCmd.Parameters.Add(new OracleParameter("allow", item.AllowanceAmount));
                insCmd.Parameters.Add(new OracleParameter("tax", item.TaxAmount));
                insCmd.Parameters.Add(new OracleParameter("allowDate", item.AllowDate ?? DateTime.Today));
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

    // ==========================================
    // 7. INSURANCE
    // ==========================================
    public async Task<List<InsuranceItem>> GetInsuranceMembersAsync(AdvantageFilter filter, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var sql = """
            SELECT E.EMP_ID, E.EMP_CODE, NVL(E.EMP_NAME, '') AS EMP_NAME,
                   NVL(D.DESIGNATION_NAME, '') AS DESIGNATION_NAME,
                   NVL(DEP.DEPARTMENT_NAME, '') AS DEPARTMENT_NAME,
                   NVL(S.SECTION_NAME, '') AS SECTION_NAME,
                   E.DATE_OF_JOINING, E.DATE_OF_BIRTH,
                   NVL(E.GROSS, 0) AS GROSS,
                   NVL(E.INSURANCE_HOLDER, 'N') AS INSURANCE_HOLDER,
                   E.NOMINEE_NAME, E.NOMINEE_RELATION,
                   NVL(E.EMP_STATUS, 'Active') AS EMP_STATUS
            FROM EMP_OFFICIAL E
            LEFT JOIN DESIGNATION D ON E.DESIGNATION_ID = D.DESIGNATION_ID
            LEFT JOIN DEPARTMENT DEP ON E.DEPARTMENT_ID = DEP.DEPARTMENT_ID
            LEFT JOIN SECTION S ON E.SECTION_ID = S.SECTION_ID
            WHERE 1 = 1
            """;

        if (filter.UnitId.HasValue && filter.UnitId.Value > 0) sql += " AND E.UNIT_ID = :unitId";
        if (filter.DepartmentId.HasValue && filter.DepartmentId.Value > 0) sql += " AND E.DEPARTMENT_ID = :deptId";
        if (!string.IsNullOrWhiteSpace(filter.EmpCode)) sql += " AND E.EMP_CODE = :empCode";
        if (!string.IsNullOrWhiteSpace(filter.TitleOrType) && filter.TitleOrType == "ACTIVE_ONLY") sql += " AND E.INSURANCE_HOLDER = 'Y'";

        sql += " ORDER BY DEP.DEPARTMENT_NAME, S.SECTION_NAME, TO_NUMBER(REGEXP_SUBSTR(E.EMP_CODE, '^[0-9]+'))";

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        if (filter.UnitId.HasValue && filter.UnitId.Value > 0) cmd.Parameters.Add(new OracleParameter("unitId", filter.UnitId.Value));
        if (filter.DepartmentId.HasValue && filter.DepartmentId.Value > 0) cmd.Parameters.Add(new OracleParameter("deptId", filter.DepartmentId.Value));
        if (!string.IsNullOrWhiteSpace(filter.EmpCode)) cmd.Parameters.Add(new OracleParameter("empCode", filter.EmpCode.Trim()));

        var list = new List<InsuranceItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            DateTime? dob = reader["DATE_OF_BIRTH"] == DBNull.Value ? null : Convert.ToDateTime(reader["DATE_OF_BIRTH"]);
            var age = 0;
            if (dob.HasValue)
            {
                age = DateTime.Today.Year - dob.Value.Year;
                if (DateTime.Today < dob.Value.AddYears(age)) age--;
            }

            list.Add(new InsuranceItem
            {
                EmpId = Convert.ToInt32(reader["EMP_ID"]),
                EmpCode = Convert.ToString(reader["EMP_CODE"]) ?? "",
                EmpName = Convert.ToString(reader["EMP_NAME"]) ?? "",
                DesignationName = Convert.ToString(reader["DESIGNATION_NAME"]) ?? "",
                DepartmentName = Convert.ToString(reader["DEPARTMENT_NAME"]) ?? "",
                SectionName = Convert.ToString(reader["SECTION_NAME"]) ?? "",
                DateOfJoining = reader["DATE_OF_JOINING"] == DBNull.Value ? null : Convert.ToDateTime(reader["DATE_OF_JOINING"]),
                DateOfBirth = dob,
                Age = age,
                Gross = Convert.ToDecimal(reader["GROSS"]),
                InsuranceHolder = Convert.ToString(reader["INSURANCE_HOLDER"]) ?? "N",
                NomineeName = Convert.ToString(reader["NOMINEE_NAME"]),
                NomineeRelation = Convert.ToString(reader["NOMINEE_RELATION"]),
                EmpStatus = Convert.ToString(reader["EMP_STATUS"])
            });
        }
        return list;
    }

    public async Task UpdateInsuranceStatusAsync(int empId, string insuranceHolder, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        const string sql = "UPDATE EMP_OFFICIAL SET INSURANCE_HOLDER = :holder WHERE EMP_ID = :empId";
        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("holder", insuranceHolder));
        cmd.Parameters.Add(new OracleParameter("empId", empId));
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
