using System.Data;
using Oracle.ManagedDataAccess.Client;
using TG.Payroll.Web.Data;
using TG.Payroll.Web.Models;

namespace TG.Payroll.Web.Services;

public class MasterRepository
{
    private readonly string _connectionString;
    private readonly ILogger<MasterRepository> _logger;

    public MasterRepository(IConfiguration configuration, ILogger<MasterRepository> logger)
    {
        _connectionString = DatabaseOptions.GetConnectionString(configuration);
        _logger = logger;
    }

    private OracleConnection GetConnection() => new(_connectionString);

    // ==================== 1. DESIGNATION ====================
    public async Task<List<DesignationItem>> GetDesignationsAsync(CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        const string sql = """
            SELECT DESIGNATION_ID, NVL(DESIGNATION_NAME, '') AS DESIGNATION_NAME, 
                   NVL(BANG_DESIGNATION_NAME, '') AS BANG_DESIGNATION_NAME,
                   NVL(GRADE, '') AS GRADE, NVL(BANG_GRADE, '') AS BANG_GRADE, 
                   NVL(POSITION, '') AS POSITION, NVL(POSITION_PRIORITY, 1) AS POSITION_PRIORITY,
                   NVL(APPR_ATTD_BONUS, 0) AS APPR_ATTD_BONUS, NVL(REMARKS, '') AS REMARKS
            FROM DESIGNATION 
            ORDER BY DESIGNATION_NAME
            """;
        await using var cmd = new OracleCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<DesignationItem>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new DesignationItem
            {
                DesignationId = Convert.ToInt32(reader["DESIGNATION_ID"]),
                DesignationName = Convert.ToString(reader["DESIGNATION_NAME"]) ?? "",
                DesignationNameBang = EmployeeCsvHelper.EnsureUnicode(Convert.ToString(reader["BANG_DESIGNATION_NAME"])),
                Grade = Convert.ToString(reader["GRADE"]),
                GradeBang = EmployeeCsvHelper.EnsureUnicode(Convert.ToString(reader["BANG_GRADE"])),
                Position = Convert.ToString(reader["POSITION"]),
                PositionPriority = Convert.ToInt32(reader["POSITION_PRIORITY"]),
                AttendanceBonus = Convert.ToDecimal(reader["APPR_ATTD_BONUS"]),
                Remarks = Convert.ToString(reader["REMARKS"])
            });
        }
        return list;
    }

    public async Task SaveDesignationAsync(DesignationItem item, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        if (item.DesignationId > 0)
        {
            const string sql = """
                UPDATE DESIGNATION
                SET DESIGNATION_NAME = :name, BANG_DESIGNATION_NAME = :nameBang,
                    GRADE = :grade, BANG_GRADE = :gradeBang, POSITION = :pos,
                    POSITION_PRIORITY = :priority, APPR_ATTD_BONUS = :bonus, REMARKS = :remarks
                WHERE DESIGNATION_ID = :id
                """;
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("name", item.DesignationName.Trim()));
            cmd.Parameters.Add(new OracleParameter("nameBang", OracleDbType.NVarchar2) { Value = item.DesignationNameBang?.Trim() ?? "" });
            cmd.Parameters.Add(new OracleParameter("grade", item.Grade?.Trim() ?? ""));
            cmd.Parameters.Add(new OracleParameter("gradeBang", OracleDbType.NVarchar2) { Value = item.GradeBang?.Trim() ?? "" });
            cmd.Parameters.Add(new OracleParameter("pos", item.Position?.Trim() ?? ""));
            cmd.Parameters.Add(new OracleParameter("priority", item.PositionPriority));
            cmd.Parameters.Add(new OracleParameter("bonus", item.AttendanceBonus));
            cmd.Parameters.Add(new OracleParameter("remarks", item.Remarks?.Trim() ?? ""));
            cmd.Parameters.Add(new OracleParameter("id", item.DesignationId));
            await cmd.ExecuteNonQueryAsync(ct);
        }
        else
        {
            const string nextIdSql = "SELECT NVL(MAX(DESIGNATION_ID), 0) + 1 FROM DESIGNATION";
            await using var nextIdCmd = new OracleCommand(nextIdSql, conn);
            var nextId = Convert.ToInt32(await nextIdCmd.ExecuteScalarAsync(ct));

            const string sql = """
                INSERT INTO DESIGNATION (DESIGNATION_ID, DESIGNATION_NAME, BANG_DESIGNATION_NAME, GRADE, BANG_GRADE, POSITION, POSITION_PRIORITY, APPR_ATTD_BONUS, REMARKS)
                VALUES (:id, :name, :nameBang, :grade, :gradeBang, :pos, :priority, :bonus, :remarks)
                """;
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("id", nextId));
            cmd.Parameters.Add(new OracleParameter("name", item.DesignationName.Trim()));
            cmd.Parameters.Add(new OracleParameter("nameBang", OracleDbType.NVarchar2) { Value = item.DesignationNameBang?.Trim() ?? "" });
            cmd.Parameters.Add(new OracleParameter("grade", item.Grade?.Trim() ?? ""));
            cmd.Parameters.Add(new OracleParameter("gradeBang", OracleDbType.NVarchar2) { Value = item.GradeBang?.Trim() ?? "" });
            cmd.Parameters.Add(new OracleParameter("pos", item.Position?.Trim() ?? ""));
            cmd.Parameters.Add(new OracleParameter("priority", item.PositionPriority));
            cmd.Parameters.Add(new OracleParameter("bonus", item.AttendanceBonus));
            cmd.Parameters.Add(new OracleParameter("remarks", item.Remarks?.Trim() ?? ""));
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task DeleteDesignationAsync(int id, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new OracleCommand("DELETE FROM DESIGNATION WHERE DESIGNATION_ID = :id", conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("id", id));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ==================== 2. SHIFT INFO ====================
    public async Task<List<ShiftInfoItem>> GetShiftsAsync(CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        const string sql = """
            SELECT SHIFT_ID, NVL(SHIFT_NAME, '') AS SHIFT_NAME, NVL(IN_TIME, '') AS IN_TIME,
                   NVL(OUT_TIME, '') AS OUT_TIME, NVL(IN_TIME_FROM, '') AS IN_TIME_FROM,
                   NVL(OUT_TIME_FROM, '') AS OUT_TIME_FROM, NVL(GRACE, '') AS GRACE,
                   NVL(LUNCE_START, '') AS LUNCE_START, NVL(LUNCE_END, '') AS LUNCE_END,
                   NVL(DEFAULT_STATUS, '') AS DEFAULT_STATUS, NVL(REMARKS, '') AS REMARKS
            FROM SHIFT_INFO
            ORDER BY SHIFT_NAME
            """;
        await using var cmd = new OracleCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<ShiftInfoItem>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new ShiftInfoItem
            {
                ShiftId = Convert.ToInt32(reader["SHIFT_ID"]),
                ShiftName = Convert.ToString(reader["SHIFT_NAME"]) ?? "",
                InTime = Convert.ToString(reader["IN_TIME"]) ?? "",
                OutTime = Convert.ToString(reader["OUT_TIME"]) ?? "",
                InTimeFrom = Convert.ToString(reader["IN_TIME_FROM"]) ?? "",
                OutTimeFrom = Convert.ToString(reader["OUT_TIME_FROM"]) ?? "",
                Grace = Convert.ToString(reader["GRACE"]) ?? "15",
                LunchStart = Convert.ToString(reader["LUNCE_START"]) ?? "",
                LunchEnd = Convert.ToString(reader["LUNCE_END"]) ?? "",
                DefaultStatus = Convert.ToString(reader["DEFAULT_STATUS"]) ?? "General",
                Remarks = Convert.ToString(reader["REMARKS"])
            });
        }
        return list;
    }

    public async Task SaveShiftAsync(ShiftInfoItem item, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        if (item.ShiftId > 0)
        {
            const string sql = """
                UPDATE SHIFT_INFO
                SET SHIFT_NAME = :name, IN_TIME = :inTime, OUT_TIME = :outTime,
                    IN_TIME_FROM = :inFrom, OUT_TIME_FROM = :outFrom, GRACE = :grace,
                    LUNCE_START = :lStart, LUNCE_END = :lEnd, DEFAULT_STATUS = :status, REMARKS = :remarks
                WHERE SHIFT_ID = :id
                """;
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("name", item.ShiftName.Trim()));
            cmd.Parameters.Add(new OracleParameter("inTime", item.InTime.Trim()));
            cmd.Parameters.Add(new OracleParameter("outTime", item.OutTime.Trim()));
            cmd.Parameters.Add(new OracleParameter("inFrom", item.InTimeFrom.Trim()));
            cmd.Parameters.Add(new OracleParameter("outFrom", item.OutTimeFrom.Trim()));
            cmd.Parameters.Add(new OracleParameter("grace", item.Grace.Trim()));
            cmd.Parameters.Add(new OracleParameter("lStart", item.LunchStart.Trim()));
            cmd.Parameters.Add(new OracleParameter("lEnd", item.LunchEnd.Trim()));
            cmd.Parameters.Add(new OracleParameter("status", item.DefaultStatus.Trim()));
            cmd.Parameters.Add(new OracleParameter("remarks", item.Remarks?.Trim() ?? ""));
            cmd.Parameters.Add(new OracleParameter("id", item.ShiftId));
            await cmd.ExecuteNonQueryAsync(ct);
        }
        else
        {
            const string nextIdSql = "SELECT NVL(MAX(SHIFT_ID), 0) + 1 FROM SHIFT_INFO";
            await using var nextIdCmd = new OracleCommand(nextIdSql, conn);
            var nextId = Convert.ToInt32(await nextIdCmd.ExecuteScalarAsync(ct));

            const string sql = """
                INSERT INTO SHIFT_INFO (SHIFT_ID, SHIFT_NAME, IN_TIME, OUT_TIME, IN_TIME_FROM, OUT_TIME_FROM, GRACE, LUNCE_START, LUNCE_END, DEFAULT_STATUS, REMARKS)
                VALUES (:id, :name, :inTime, :outTime, :inFrom, :outFrom, :grace, :lStart, :lEnd, :status, :remarks)
                """;
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("id", nextId));
            cmd.Parameters.Add(new OracleParameter("name", item.ShiftName.Trim()));
            cmd.Parameters.Add(new OracleParameter("inTime", item.InTime.Trim()));
            cmd.Parameters.Add(new OracleParameter("outTime", item.OutTime.Trim()));
            cmd.Parameters.Add(new OracleParameter("inFrom", item.InTimeFrom.Trim()));
            cmd.Parameters.Add(new OracleParameter("outFrom", item.OutTimeFrom.Trim()));
            cmd.Parameters.Add(new OracleParameter("grace", item.Grace.Trim()));
            cmd.Parameters.Add(new OracleParameter("lStart", item.LunchStart.Trim()));
            cmd.Parameters.Add(new OracleParameter("lEnd", item.LunchEnd.Trim()));
            cmd.Parameters.Add(new OracleParameter("status", item.DefaultStatus.Trim()));
            cmd.Parameters.Add(new OracleParameter("remarks", item.Remarks?.Trim() ?? ""));
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task DeleteShiftAsync(int id, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new OracleCommand("DELETE FROM SHIFT_INFO WHERE SHIFT_ID = :id", conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("id", id));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ==================== 3. HOLIDAY ====================
    public async Task<List<HolidayItem>> GetHolidaysAsync(int? year = null, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        var targetYear = year ?? DateTime.Today.Year;
        var sql = $"SELECT HOLIDAY_ID, DT, NVL(TYPE, 'Holiday') AS TYPE, NVL(CATEGORY, 'All') AS CATEGORY, NVL(REMARKS, '') AS REMARKS FROM HOLIDAY WHERE DT >= TO_DATE('01-Jan-{targetYear}', 'dd-Mon-yyyy') AND DT <= TO_DATE('31-Dec-{targetYear}', 'dd-Mon-yyyy') ORDER BY DT DESC";
        await using var cmd = new OracleCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<HolidayItem>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new HolidayItem
            {
                HolidayId = Convert.ToInt32(reader["HOLIDAY_ID"]),
                Date = Convert.ToDateTime(reader["DT"]),
                Type = Convert.ToString(reader["TYPE"]) ?? "Holiday",
                Category = Convert.ToString(reader["CATEGORY"]) ?? "All",
                Remarks = Convert.ToString(reader["REMARKS"])
            });
        }
        return list;
    }

    public async Task SaveHolidayAsync(HolidayItem item, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        if (item.HolidayId > 0)
        {
            const string sql = "UPDATE HOLIDAY SET DT = :dt, TYPE = :type, CATEGORY = :cat, REMARKS = :remarks WHERE HOLIDAY_ID = :id";
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("dt", OracleDbType.Date) { Value = item.Date.Date });
            cmd.Parameters.Add(new OracleParameter("type", item.Type.Trim()));
            cmd.Parameters.Add(new OracleParameter("cat", item.Category.Trim()));
            cmd.Parameters.Add(new OracleParameter("remarks", item.Remarks?.Trim() ?? ""));
            cmd.Parameters.Add(new OracleParameter("id", item.HolidayId));
            await cmd.ExecuteNonQueryAsync(ct);
        }
        else
        {
            const string nextIdSql = "SELECT NVL(MAX(HOLIDAY_ID), 0) + 1 FROM HOLIDAY";
            await using var nextIdCmd = new OracleCommand(nextIdSql, conn);
            var nextId = Convert.ToInt32(await nextIdCmd.ExecuteScalarAsync(ct));

            const string sql = "INSERT INTO HOLIDAY (HOLIDAY_ID, DT, TYPE, CATEGORY, REMARKS) VALUES (:id, :dt, :type, :cat, :remarks)";
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("id", nextId));
            cmd.Parameters.Add(new OracleParameter("dt", OracleDbType.Date) { Value = item.Date.Date });
            cmd.Parameters.Add(new OracleParameter("type", item.Type.Trim()));
            cmd.Parameters.Add(new OracleParameter("cat", item.Category.Trim()));
            cmd.Parameters.Add(new OracleParameter("remarks", item.Remarks?.Trim() ?? ""));
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task DeleteHolidayAsync(int id, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new OracleCommand("DELETE FROM HOLIDAY WHERE HOLIDAY_ID = :id", conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("id", id));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ==================== 4. UNIT ====================
    public async Task<List<UnitItem>> GetUnitsAsync(CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        const string sql = """
            SELECT U.UNIT_ID, NVL(U.UNIT_NAME, '') AS UNIT_NAME, NVL(U.BANG_UNIT_NAME, '') AS BANG_UNIT_NAME,
                   U.COMPANY_ID, NVL(C.COMPANY_NAME, '') AS COMPANY_NAME,
                   NVL(U.SHORT_NAME, '') AS SHORT_NAME, NVL(U.ADDRESS, '') AS ADDRESS,
                   NVL(U.BANG_ADDRESS, '') AS BANG_ADDRESS, NVL(U.REMARKS, '') AS REMARKS
            FROM UNIT U
            LEFT JOIN COMPANY C ON U.COMPANY_ID = C.COMPANY_ID
            ORDER BY U.UNIT_ID
            """;
        await using var cmd = new OracleCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<UnitItem>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new UnitItem
            {
                UnitId = Convert.ToInt32(reader["UNIT_ID"]),
                UnitName = Convert.ToString(reader["UNIT_NAME"]) ?? "",
                UnitNameBang = EmployeeCsvHelper.EnsureUnicode(Convert.ToString(reader["BANG_UNIT_NAME"])),
                CompanyId = reader["COMPANY_ID"] != DBNull.Value ? Convert.ToInt32(reader["COMPANY_ID"]) : null,
                CompanyName = Convert.ToString(reader["COMPANY_NAME"]),
                ShortName = Convert.ToString(reader["SHORT_NAME"]),
                Address = Convert.ToString(reader["ADDRESS"]),
                AddressBang = EmployeeCsvHelper.EnsureUnicode(Convert.ToString(reader["BANG_ADDRESS"])),
                Remarks = Convert.ToString(reader["REMARKS"])
            });
        }
        return list;
    }

    public async Task SaveUnitAsync(UnitItem item, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        if (item.UnitId > 0)
        {
            const string sql = """
                UPDATE UNIT
                SET UNIT_NAME = :name, BANG_UNIT_NAME = :nameBang, COMPANY_ID = :compId,
                    SHORT_NAME = :shortName, ADDRESS = :address, BANG_ADDRESS = :addressBang, REMARKS = :remarks
                WHERE UNIT_ID = :id
                """;
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("name", item.UnitName.Trim()));
            cmd.Parameters.Add(new OracleParameter("nameBang", OracleDbType.NVarchar2) { Value = item.UnitNameBang?.Trim() ?? "" });
            cmd.Parameters.Add(new OracleParameter("compId", item.CompanyId ?? 1));
            cmd.Parameters.Add(new OracleParameter("shortName", item.ShortName?.Trim() ?? ""));
            cmd.Parameters.Add(new OracleParameter("address", item.Address?.Trim() ?? ""));
            cmd.Parameters.Add(new OracleParameter("addressBang", OracleDbType.NVarchar2) { Value = item.AddressBang?.Trim() ?? "" });
            cmd.Parameters.Add(new OracleParameter("remarks", item.Remarks?.Trim() ?? ""));
            cmd.Parameters.Add(new OracleParameter("id", item.UnitId));
            await cmd.ExecuteNonQueryAsync(ct);
        }
        else
        {
            const string nextIdSql = "SELECT NVL(MAX(UNIT_ID), 0) + 1 FROM UNIT";
            await using var nextIdCmd = new OracleCommand(nextIdSql, conn);
            var nextId = Convert.ToInt32(await nextIdCmd.ExecuteScalarAsync(ct));

            const string sql = """
                INSERT INTO UNIT (UNIT_ID, UNIT_NAME, BANG_UNIT_NAME, COMPANY_ID, SHORT_NAME, ADDRESS, BANG_ADDRESS, REMARKS, CREATED_BY)
                VALUES (:id, :name, :nameBang, :compId, :shortName, :address, :addressBang, :remarks, 1)
                """;
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("id", nextId));
            cmd.Parameters.Add(new OracleParameter("name", item.UnitName.Trim()));
            cmd.Parameters.Add(new OracleParameter("nameBang", OracleDbType.NVarchar2) { Value = item.UnitNameBang?.Trim() ?? "" });
            cmd.Parameters.Add(new OracleParameter("compId", item.CompanyId ?? 1));
            cmd.Parameters.Add(new OracleParameter("shortName", item.ShortName?.Trim() ?? ""));
            cmd.Parameters.Add(new OracleParameter("address", item.Address?.Trim() ?? ""));
            cmd.Parameters.Add(new OracleParameter("addressBang", OracleDbType.NVarchar2) { Value = item.AddressBang?.Trim() ?? "" });
            cmd.Parameters.Add(new OracleParameter("remarks", item.Remarks?.Trim() ?? ""));
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task DeleteUnitAsync(int id, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new OracleCommand("DELETE FROM UNIT WHERE UNIT_ID = :id", conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("id", id));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ==================== 5. DEPARTMENT ====================
    public async Task<List<DepartmentItem>> GetDepartmentsAsync(CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        const string sql = """
            SELECT D.DEPARTMENT_ID, NVL(D.DEPARTMENT_NAME, '') AS DEPARTMENT_NAME,
                   NVL(D.BANG_DEPT_NAME, '') AS BANG_DEPT_NAME, D.UNIT_ID,
                   NVL(U.UNIT_NAME, '') AS UNIT_NAME, NVL(D.SHOW_PRIORITY, 1) AS SHOW_PRIORITY,
                   NVL(D.SHORT_NAME, '') AS SHORT_NAME, NVL(D.REMARKS, '') AS REMARKS
            FROM DEPARTMENT D
            LEFT JOIN UNIT U ON D.UNIT_ID = U.UNIT_ID
            ORDER BY D.DEPARTMENT_NAME
            """;
        await using var cmd = new OracleCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<DepartmentItem>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new DepartmentItem
            {
                DepartmentId = Convert.ToInt32(reader["DEPARTMENT_ID"]),
                DepartmentName = Convert.ToString(reader["DEPARTMENT_NAME"]) ?? "",
                DepartmentNameBang = EmployeeCsvHelper.EnsureUnicode(Convert.ToString(reader["BANG_DEPT_NAME"])),
                UnitId = reader["UNIT_ID"] != DBNull.Value ? Convert.ToInt32(reader["UNIT_ID"]) : null,
                UnitName = Convert.ToString(reader["UNIT_NAME"]),
                ShowPriority = Convert.ToInt32(reader["SHOW_PRIORITY"]),
                ShortName = Convert.ToString(reader["SHORT_NAME"]),
                Remarks = Convert.ToString(reader["REMARKS"])
            });
        }
        return list;
    }

    public async Task SaveDepartmentAsync(DepartmentItem item, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        if (item.DepartmentId > 0)
        {
            const string sql = """
                UPDATE DEPARTMENT
                SET DEPARTMENT_NAME = :name, BANG_DEPT_NAME = :nameBang, UNIT_ID = :unitId,
                    SHOW_PRIORITY = :priority, SHORT_NAME = :shortName, REMARKS = :remarks
                WHERE DEPARTMENT_ID = :id
                """;
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("name", item.DepartmentName.Trim()));
            cmd.Parameters.Add(new OracleParameter("nameBang", OracleDbType.NVarchar2) { Value = item.DepartmentNameBang?.Trim() ?? "" });
            cmd.Parameters.Add(new OracleParameter("unitId", item.UnitId.HasValue && item.UnitId > 0 ? item.UnitId.Value : DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("priority", item.ShowPriority));
            cmd.Parameters.Add(new OracleParameter("shortName", item.ShortName?.Trim() ?? ""));
            cmd.Parameters.Add(new OracleParameter("remarks", item.Remarks?.Trim() ?? ""));
            cmd.Parameters.Add(new OracleParameter("id", item.DepartmentId));
            await cmd.ExecuteNonQueryAsync(ct);
        }
        else
        {
            const string nextIdSql = "SELECT NVL(MAX(DEPARTMENT_ID), 0) + 1 FROM DEPARTMENT";
            await using var nextIdCmd = new OracleCommand(nextIdSql, conn);
            var nextId = Convert.ToInt32(await nextIdCmd.ExecuteScalarAsync(ct));

            const string sql = """
                INSERT INTO DEPARTMENT (DEPARTMENT_ID, DEPARTMENT_NAME, BANG_DEPT_NAME, UNIT_ID, SHOW_PRIORITY, SHORT_NAME, REMARKS)
                VALUES (:id, :name, :nameBang, :unitId, :priority, :shortName, :remarks)
                """;
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("id", nextId));
            cmd.Parameters.Add(new OracleParameter("name", item.DepartmentName.Trim()));
            cmd.Parameters.Add(new OracleParameter("nameBang", OracleDbType.NVarchar2) { Value = item.DepartmentNameBang?.Trim() ?? "" });
            cmd.Parameters.Add(new OracleParameter("unitId", item.UnitId.HasValue && item.UnitId > 0 ? item.UnitId.Value : DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("priority", item.ShowPriority));
            cmd.Parameters.Add(new OracleParameter("shortName", item.ShortName?.Trim() ?? ""));
            cmd.Parameters.Add(new OracleParameter("remarks", item.Remarks?.Trim() ?? ""));
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task DeleteDepartmentAsync(int id, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new OracleCommand("DELETE FROM DEPARTMENT WHERE DEPARTMENT_ID = :id", conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("id", id));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ==================== 6. CATEGORY ====================
    public async Task<List<CategoryItem>> GetCategoriesAsync(CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        const string sql = """
            SELECT EMP_CATEGORY_ID, NVL(EMP_CATEGORY_NAME, '') AS EMP_CATEGORY_NAME,
                   NVL(BANG_EMP_TYPE_NAME, '') AS BANG_EMP_TYPE_NAME,
                   NVL(TIFFIN_ALW, 0) AS TIFFIN_ALW, NVL(POSITION_LEVEL, 1) AS POSITION_LEVEL,
                   NVL(REMARKS, '') AS REMARKS
            FROM EMP_CATEGORY
            ORDER BY EMP_CATEGORY_NAME
            """;
        await using var cmd = new OracleCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<CategoryItem>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new CategoryItem
            {
                CategoryId = Convert.ToInt32(reader["EMP_CATEGORY_ID"]),
                CategoryName = Convert.ToString(reader["EMP_CATEGORY_NAME"]) ?? "",
                CategoryNameBang = EmployeeCsvHelper.EnsureUnicode(Convert.ToString(reader["BANG_EMP_TYPE_NAME"])),
                TiffinAllowance = Convert.ToDecimal(reader["TIFFIN_ALW"]),
                PositionLevel = Convert.ToInt32(reader["POSITION_LEVEL"]),
                Remarks = Convert.ToString(reader["REMARKS"])
            });
        }
        return list;
    }

    public async Task SaveCategoryAsync(CategoryItem item, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        if (item.CategoryId > 0)
        {
            const string sql = """
                UPDATE EMP_CATEGORY
                SET EMP_CATEGORY_NAME = :name, BANG_EMP_TYPE_NAME = :nameBang,
                    TIFFIN_ALW = :tiffin, POSITION_LEVEL = :pos, REMARKS = :remarks
                WHERE EMP_CATEGORY_ID = :id
                """;
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("name", item.CategoryName.Trim()));
            cmd.Parameters.Add(new OracleParameter("nameBang", OracleDbType.NVarchar2) { Value = item.CategoryNameBang?.Trim() ?? "" });
            cmd.Parameters.Add(new OracleParameter("tiffin", item.TiffinAllowance));
            cmd.Parameters.Add(new OracleParameter("pos", item.PositionLevel));
            cmd.Parameters.Add(new OracleParameter("remarks", item.Remarks?.Trim() ?? ""));
            cmd.Parameters.Add(new OracleParameter("id", item.CategoryId));
            await cmd.ExecuteNonQueryAsync(ct);
        }
        else
        {
            const string nextIdSql = "SELECT NVL(MAX(EMP_CATEGORY_ID), 0) + 1 FROM EMP_CATEGORY";
            await using var nextIdCmd = new OracleCommand(nextIdSql, conn);
            var nextId = Convert.ToInt32(await nextIdCmd.ExecuteScalarAsync(ct));

            const string sql = """
                INSERT INTO EMP_CATEGORY (EMP_CATEGORY_ID, EMP_CATEGORY_NAME, BANG_EMP_TYPE_NAME, TIFFIN_ALW, POSITION_LEVEL, REMARKS)
                VALUES (:id, :name, :nameBang, :tiffin, :pos, :remarks)
                """;
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("id", nextId));
            cmd.Parameters.Add(new OracleParameter("name", item.CategoryName.Trim()));
            cmd.Parameters.Add(new OracleParameter("nameBang", OracleDbType.NVarchar2) { Value = item.CategoryNameBang?.Trim() ?? "" });
            cmd.Parameters.Add(new OracleParameter("tiffin", item.TiffinAllowance));
            cmd.Parameters.Add(new OracleParameter("pos", item.PositionLevel));
            cmd.Parameters.Add(new OracleParameter("remarks", item.Remarks?.Trim() ?? ""));
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task DeleteCategoryAsync(int id, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new OracleCommand("DELETE FROM EMP_CATEGORY WHERE EMP_CATEGORY_ID = :id", conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("id", id));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ==================== 7. SECTION ====================
    public async Task<List<SectionItem>> GetSectionsAsync(CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        const string sql = """
            SELECT S.SECTION_ID, NVL(S.SECTION_NAME, '') AS SECTION_NAME,
                   NVL(S.BANG_SEC_NAME, '') AS BANG_SEC_NAME, S.UNIT_ID,
                   NVL(U.UNIT_NAME, '') AS UNIT_NAME, NVL(S.SHOW_TOGETHER, 1) AS SHOW_TOGETHER,
                   NVL(S.REMARKS, '') AS REMARKS
            FROM SECTION S
            LEFT JOIN UNIT U ON S.UNIT_ID = U.UNIT_ID
            ORDER BY S.SECTION_NAME
            """;
        await using var cmd = new OracleCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<SectionItem>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new SectionItem
            {
                SectionId = Convert.ToInt32(reader["SECTION_ID"]),
                SectionName = Convert.ToString(reader["SECTION_NAME"]) ?? "",
                SectionNameBang = EmployeeCsvHelper.EnsureUnicode(Convert.ToString(reader["BANG_SEC_NAME"])),
                UnitId = reader["UNIT_ID"] != DBNull.Value ? Convert.ToInt32(reader["UNIT_ID"]) : null,
                UnitName = Convert.ToString(reader["UNIT_NAME"]),
                ShowTogether = Convert.ToInt32(reader["SHOW_TOGETHER"]),
                Remarks = Convert.ToString(reader["REMARKS"])
            });
        }
        return list;
    }

    public async Task SaveSectionAsync(SectionItem item, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        if (item.SectionId > 0)
        {
            const string sql = """
                UPDATE SECTION
                SET SECTION_NAME = :name, BANG_SEC_NAME = :nameBang, UNIT_ID = :unitId,
                    SHOW_TOGETHER = :show, REMARKS = :remarks
                WHERE SECTION_ID = :id
                """;
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("name", item.SectionName.Trim()));
            cmd.Parameters.Add(new OracleParameter("nameBang", OracleDbType.NVarchar2) { Value = item.SectionNameBang?.Trim() ?? "" });
            cmd.Parameters.Add(new OracleParameter("unitId", item.UnitId.HasValue && item.UnitId > 0 ? item.UnitId.Value : DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("show", item.ShowTogether));
            cmd.Parameters.Add(new OracleParameter("remarks", item.Remarks?.Trim() ?? ""));
            cmd.Parameters.Add(new OracleParameter("id", item.SectionId));
            await cmd.ExecuteNonQueryAsync(ct);
        }
        else
        {
            const string nextIdSql = "SELECT NVL(MAX(SECTION_ID), 0) + 1 FROM SECTION";
            await using var nextIdCmd = new OracleCommand(nextIdSql, conn);
            var nextId = Convert.ToInt32(await nextIdCmd.ExecuteScalarAsync(ct));

            const string sql = """
                INSERT INTO SECTION (SECTION_ID, SECTION_NAME, BANG_SEC_NAME, UNIT_ID, SHOW_TOGETHER, REMARKS)
                VALUES (:id, :name, :nameBang, :unitId, :show, :remarks)
                """;
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("id", nextId));
            cmd.Parameters.Add(new OracleParameter("name", item.SectionName.Trim()));
            cmd.Parameters.Add(new OracleParameter("nameBang", OracleDbType.NVarchar2) { Value = item.SectionNameBang?.Trim() ?? "" });
            cmd.Parameters.Add(new OracleParameter("unitId", item.UnitId.HasValue && item.UnitId > 0 ? item.UnitId.Value : DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("show", item.ShowTogether));
            cmd.Parameters.Add(new OracleParameter("remarks", item.Remarks?.Trim() ?? ""));
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task DeleteSectionAsync(int id, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new OracleCommand("DELETE FROM SECTION WHERE SECTION_ID = :id", conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("id", id));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ==================== 8. LINE ====================
    public async Task<List<LineItem>> GetLinesAsync(CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        const string sql = """
            SELECT L.LINE_ID, NVL(L.LINE_NAME, '') AS LINE_NAME,
                   NVL(L.BANG_LINE_NAME, '') AS BANG_LINE_NAME, L.UNIT_ID,
                   NVL(U.UNIT_NAME, '') AS UNIT_NAME, NVL(L.FLOOR_NO, 1) AS FLOOR_NO,
                   NVL(L.OPERATOR, 0) AS OPERATOR, NVL(L.HELPER, 0) AS HELPER,
                   NVL(L.IRONMAN, 0) AS IRONMAN, NVL(L.OTHERS, 0) AS OTHERS,
                   NVL(L.POSITION, 1) AS POSITION, NVL(L.REMARKS, '') AS REMARKS
            FROM LINE L
            LEFT JOIN UNIT U ON L.UNIT_ID = U.UNIT_ID
            ORDER BY L.LINE_NAME
            """;
        await using var cmd = new OracleCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<LineItem>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new LineItem
            {
                LineId = Convert.ToInt32(reader["LINE_ID"]),
                LineName = Convert.ToString(reader["LINE_NAME"]) ?? "",
                LineNameBang = EmployeeCsvHelper.EnsureUnicode(Convert.ToString(reader["BANG_LINE_NAME"])),
                UnitId = reader["UNIT_ID"] != DBNull.Value ? Convert.ToInt32(reader["UNIT_ID"]) : null,
                UnitName = Convert.ToString(reader["UNIT_NAME"]),
                FloorNo = Convert.ToInt32(reader["FLOOR_NO"]),
                OperatorCount = Convert.ToInt32(reader["OPERATOR"]),
                HelperCount = Convert.ToInt32(reader["HELPER"]),
                IronmanCount = Convert.ToInt32(reader["IRONMAN"]),
                OthersCount = Convert.ToInt32(reader["OTHERS"]),
                Position = Convert.ToInt32(reader["POSITION"]),
                Remarks = Convert.ToString(reader["REMARKS"])
            });
        }
        return list;
    }

    public async Task SaveLineAsync(LineItem item, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        if (item.LineId > 0)
        {
            const string sql = """
                UPDATE LINE
                SET LINE_NAME = :name, BANG_LINE_NAME = :nameBang, UNIT_ID = :unitId,
                    FLOOR_NO = :floor, OPERATOR = :op, HELPER = :help, IRONMAN = :iron,
                    OTHERS = :oth, POSITION = :pos, REMARKS = :remarks
                WHERE LINE_ID = :id
                """;
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("name", item.LineName.Trim()));
            cmd.Parameters.Add(new OracleParameter("nameBang", OracleDbType.NVarchar2) { Value = item.LineNameBang?.Trim() ?? "" });
            cmd.Parameters.Add(new OracleParameter("unitId", item.UnitId.HasValue && item.UnitId > 0 ? item.UnitId.Value : DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("floor", item.FloorNo));
            cmd.Parameters.Add(new OracleParameter("op", item.OperatorCount));
            cmd.Parameters.Add(new OracleParameter("help", item.HelperCount));
            cmd.Parameters.Add(new OracleParameter("iron", item.IronmanCount));
            cmd.Parameters.Add(new OracleParameter("oth", item.OthersCount));
            cmd.Parameters.Add(new OracleParameter("pos", item.Position));
            cmd.Parameters.Add(new OracleParameter("remarks", item.Remarks?.Trim() ?? ""));
            cmd.Parameters.Add(new OracleParameter("id", item.LineId));
            await cmd.ExecuteNonQueryAsync(ct);
        }
        else
        {
            const string nextIdSql = "SELECT NVL(MAX(LINE_ID), 0) + 1 FROM LINE";
            await using var nextIdCmd = new OracleCommand(nextIdSql, conn);
            var nextId = Convert.ToInt32(await nextIdCmd.ExecuteScalarAsync(ct));

            const string sql = """
                INSERT INTO LINE (LINE_ID, LINE_NAME, BANG_LINE_NAME, UNIT_ID, FLOOR_NO, OPERATOR, HELPER, IRONMAN, OTHERS, POSITION, REMARKS)
                VALUES (:id, :name, :nameBang, :unitId, :floor, :op, :help, :iron, :oth, :pos, :remarks)
                """;
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("id", nextId));
            cmd.Parameters.Add(new OracleParameter("name", item.LineName.Trim()));
            cmd.Parameters.Add(new OracleParameter("nameBang", OracleDbType.NVarchar2) { Value = item.LineNameBang?.Trim() ?? "" });
            cmd.Parameters.Add(new OracleParameter("unitId", item.UnitId.HasValue && item.UnitId > 0 ? item.UnitId.Value : DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("floor", item.FloorNo));
            cmd.Parameters.Add(new OracleParameter("op", item.OperatorCount));
            cmd.Parameters.Add(new OracleParameter("help", item.HelperCount));
            cmd.Parameters.Add(new OracleParameter("iron", item.IronmanCount));
            cmd.Parameters.Add(new OracleParameter("oth", item.OthersCount));
            cmd.Parameters.Add(new OracleParameter("pos", item.Position));
            cmd.Parameters.Add(new OracleParameter("remarks", item.Remarks?.Trim() ?? ""));
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task DeleteLineAsync(int id, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new OracleCommand("DELETE FROM LINE WHERE LINE_ID = :id", conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("id", id));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ==================== 9. TRANSPORT STAND ====================
    public async Task<List<TransportStandItem>> GetTransportStandsAsync(CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        const string sql = "SELECT STAND_ID, NVL(STAND_NAME, '') AS STAND_NAME, NVL(STAND_FORM, '') AS STAND_FORM FROM TRANSPORT_STAND ORDER BY STAND_NAME";
        await using var cmd = new OracleCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<TransportStandItem>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new TransportStandItem
            {
                StandId = Convert.ToInt32(reader["STAND_ID"]),
                StandName = Convert.ToString(reader["STAND_NAME"]) ?? "",
                StandForm = Convert.ToString(reader["STAND_FORM"])
            });
        }
        return list;
    }

    public async Task SaveTransportStandAsync(TransportStandItem item, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        if (item.StandId > 0)
        {
            const string sql = "UPDATE TRANSPORT_STAND SET STAND_NAME = :name, STAND_FORM = :form WHERE STAND_ID = :id";
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("name", item.StandName.Trim()));
            cmd.Parameters.Add(new OracleParameter("form", item.StandForm?.Trim() ?? ""));
            cmd.Parameters.Add(new OracleParameter("id", item.StandId));
            await cmd.ExecuteNonQueryAsync(ct);
        }
        else
        {
            const string nextIdSql = "SELECT NVL(MAX(STAND_ID), 0) + 1 FROM TRANSPORT_STAND";
            await using var nextIdCmd = new OracleCommand(nextIdSql, conn);
            var nextId = Convert.ToInt32(await nextIdCmd.ExecuteScalarAsync(ct));

            const string sql = "INSERT INTO TRANSPORT_STAND (STAND_ID, STAND_NAME, STAND_FORM) VALUES (:id, :name, :form)";
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("id", nextId));
            cmd.Parameters.Add(new OracleParameter("name", item.StandName.Trim()));
            cmd.Parameters.Add(new OracleParameter("form", item.StandForm?.Trim() ?? ""));
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task DeleteTransportStandAsync(int id, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new OracleCommand("DELETE FROM TRANSPORT_STAND WHERE STAND_ID = :id", conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("id", id));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ==================== 10. SALARY RULE ====================
    public async Task<List<SalaryRuleItem>> GetSalaryRulesAsync(CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        const string sql = """
            SELECT RULE_ID, NVL(RULE_NAME, '') AS RULE_NAME,
                   NVL(RULE_BASIC, 0) AS RULE_BASIC, NVL(RULE_HOUSE_RENT, 0) AS RULE_HOUSE_RENT,
                   NVL(RULE_MEDICAL, 0) AS RULE_MEDICAL, NVL(RULE_TRANSPORT, 0) AS RULE_TRANSPORT,
                   NVL(RULE_FOOD, 0) AS RULE_FOOD, NVL(GET_ATTD_BONUS, 0) AS GET_ATTD_BONUS,
                   NVL(MIN_ATTD_BONUS, 0) AS MIN_ATTD_BONUS, NVL(RULE_DEAR_ALW, 0) AS RULE_DEAR_ALW,
                   NVL(RULE_STATUS, 'Active') AS RULE_STATUS, NVL(RULE_REMARKS, '') AS RULE_REMARKS
            FROM SALARY_RULE_INFO
            ORDER BY RULE_NAME
            """;
        await using var cmd = new OracleCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<SalaryRuleItem>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new SalaryRuleItem
            {
                RuleId = Convert.ToInt32(reader["RULE_ID"]),
                RuleName = Convert.ToString(reader["RULE_NAME"]) ?? "",
                BasicPercent = Convert.ToDecimal(reader["RULE_BASIC"]),
                HouseRentPercent = Convert.ToDecimal(reader["RULE_HOUSE_RENT"]),
                MedicalAllowance = Convert.ToDecimal(reader["RULE_MEDICAL"]),
                TransportAllowance = Convert.ToDecimal(reader["RULE_TRANSPORT"]),
                FoodAllowance = Convert.ToDecimal(reader["RULE_FOOD"]),
                AttendanceBonus = Convert.ToDecimal(reader["GET_ATTD_BONUS"]),
                MinAttendanceBonus = Convert.ToDecimal(reader["MIN_ATTD_BONUS"]),
                DearnessAllowance = Convert.ToDecimal(reader["RULE_DEAR_ALW"]),
                Status = Convert.ToString(reader["RULE_STATUS"]) ?? "Active",
                Remarks = Convert.ToString(reader["RULE_REMARKS"])
            });
        }
        return list;
    }

    public async Task SaveSalaryRuleAsync(SalaryRuleItem item, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        if (item.RuleId > 0)
        {
            const string sql = """
                UPDATE SALARY_RULE_INFO
                SET RULE_NAME = :name, RULE_BASIC = :basic, RULE_HOUSE_RENT = :hr,
                    RULE_MEDICAL = :med, RULE_TRANSPORT = :trans, RULE_FOOD = :food,
                    GET_ATTD_BONUS = :bonus, MIN_ATTD_BONUS = :minBonus, RULE_DEAR_ALW = :dear,
                    RULE_STATUS = :status, RULE_REMARKS = :remarks
                WHERE RULE_ID = :id
                """;
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("name", item.RuleName.Trim()));
            cmd.Parameters.Add(new OracleParameter("basic", item.BasicPercent));
            cmd.Parameters.Add(new OracleParameter("hr", item.HouseRentPercent));
            cmd.Parameters.Add(new OracleParameter("med", item.MedicalAllowance));
            cmd.Parameters.Add(new OracleParameter("trans", item.TransportAllowance));
            cmd.Parameters.Add(new OracleParameter("food", item.FoodAllowance));
            cmd.Parameters.Add(new OracleParameter("bonus", item.AttendanceBonus));
            cmd.Parameters.Add(new OracleParameter("minBonus", item.MinAttendanceBonus));
            cmd.Parameters.Add(new OracleParameter("dear", item.DearnessAllowance));
            cmd.Parameters.Add(new OracleParameter("status", item.Status.Trim()));
            cmd.Parameters.Add(new OracleParameter("remarks", item.Remarks?.Trim() ?? ""));
            cmd.Parameters.Add(new OracleParameter("id", item.RuleId));
            await cmd.ExecuteNonQueryAsync(ct);
        }
        else
        {
            const string nextIdSql = "SELECT NVL(MAX(RULE_ID), 0) + 1 FROM SALARY_RULE_INFO";
            await using var nextIdCmd = new OracleCommand(nextIdSql, conn);
            var nextId = Convert.ToInt32(await nextIdCmd.ExecuteScalarAsync(ct));

            const string sql = """
                INSERT INTO SALARY_RULE_INFO (RULE_ID, RULE_NAME, RULE_BASIC, RULE_HOUSE_RENT, RULE_MEDICAL, RULE_TRANSPORT, RULE_FOOD, GET_ATTD_BONUS, MIN_ATTD_BONUS, RULE_DEAR_ALW, ATTD_ALW, OT_ALW, NIGHT_BILL, WASHING_BILL, DRIVER_ALW, EXPORT_ALW, IS_DEDUCT, RULE_STATUS, RULE_REMARKS, CREATED_BY)
                VALUES (:id, :name, :basic, :hr, :med, :trans, :food, :bonus, :minBonus, :dear, 0, 0, 0, 0, 0, 0, 'N', :status, :remarks, 1)
                """;
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("id", nextId));
            cmd.Parameters.Add(new OracleParameter("name", item.RuleName.Trim()));
            cmd.Parameters.Add(new OracleParameter("basic", item.BasicPercent));
            cmd.Parameters.Add(new OracleParameter("hr", item.HouseRentPercent));
            cmd.Parameters.Add(new OracleParameter("med", item.MedicalAllowance));
            cmd.Parameters.Add(new OracleParameter("trans", item.TransportAllowance));
            cmd.Parameters.Add(new OracleParameter("food", item.FoodAllowance));
            cmd.Parameters.Add(new OracleParameter("bonus", item.AttendanceBonus));
            cmd.Parameters.Add(new OracleParameter("minBonus", item.MinAttendanceBonus));
            cmd.Parameters.Add(new OracleParameter("dear", item.DearnessAllowance));
            cmd.Parameters.Add(new OracleParameter("status", item.Status.Trim()));
            cmd.Parameters.Add(new OracleParameter("remarks", item.Remarks?.Trim() ?? ""));
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task DeleteSalaryRuleAsync(int id, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new OracleCommand("DELETE FROM SALARY_RULE_INFO WHERE RULE_ID = :id", conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("id", id));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ==================== 11. SIGNING OPTION ====================
    public async Task<List<SigningOptionItem>> GetSigningOptionsAsync(CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        const string sql = """
            SELECT NVL(SIGNING_ID, 0) AS SIGNING_ID, NVL(SIGNING_NAME, '') AS SIGNING_NAME,
                   NVL(SIGNING_PRIORITY, 1) AS SIGNING_PRIORITY, NVL(SIGNING_FOR, '') AS SIGNING_FOR,
                   NVL(SIGNING_STATUS, 'Active') AS SIGNING_STATUS
            FROM SIGNING_OPTION
            ORDER BY SIGNING_PRIORITY
            """;
        await using var cmd = new OracleCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<SigningOptionItem>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new SigningOptionItem
            {
                SigningId = Convert.ToInt32(reader["SIGNING_ID"]),
                SigningName = Convert.ToString(reader["SIGNING_NAME"]) ?? "",
                SigningPriority = Convert.ToInt32(reader["SIGNING_PRIORITY"]),
                SigningFor = Convert.ToString(reader["SIGNING_FOR"]) ?? "PAYSLIP",
                SigningStatus = Convert.ToString(reader["SIGNING_STATUS"]) ?? "Active"
            });
        }
        return list;
    }

    public async Task SaveSigningOptionAsync(SigningOptionItem item, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        if (item.SigningId > 0)
        {
            const string sql = """
                UPDATE SIGNING_OPTION
                SET SIGNING_NAME = :name, SIGNING_PRIORITY = :priority,
                    SIGNING_FOR = :sFor, SIGNING_STATUS = :status
                WHERE SIGNING_ID = :id
                """;
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("name", item.SigningName.Trim()));
            cmd.Parameters.Add(new OracleParameter("priority", item.SigningPriority));
            cmd.Parameters.Add(new OracleParameter("sFor", item.SigningFor.Trim()));
            cmd.Parameters.Add(new OracleParameter("status", item.SigningStatus.Trim()));
            cmd.Parameters.Add(new OracleParameter("id", item.SigningId));
            await cmd.ExecuteNonQueryAsync(ct);
        }
        else
        {
            const string nextIdSql = "SELECT NVL(MAX(SIGNING_ID), 0) + 1 FROM SIGNING_OPTION";
            await using var nextIdCmd = new OracleCommand(nextIdSql, conn);
            var nextId = Convert.ToInt32(await nextIdCmd.ExecuteScalarAsync(ct));

            const string sql = """
                INSERT INTO SIGNING_OPTION (SIGNING_ID, SIGNING_NAME, SIGNING_PRIORITY, SIGNING_FOR, SIGNING_STATUS)
                VALUES (:id, :name, :priority, :sFor, :status)
                """;
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("id", nextId));
            cmd.Parameters.Add(new OracleParameter("name", item.SigningName.Trim()));
            cmd.Parameters.Add(new OracleParameter("priority", item.SigningPriority));
            cmd.Parameters.Add(new OracleParameter("sFor", item.SigningFor.Trim()));
            cmd.Parameters.Add(new OracleParameter("status", item.SigningStatus.Trim()));
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task DeleteSigningOptionAsync(int id, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new OracleCommand("DELETE FROM SIGNING_OPTION WHERE SIGNING_ID = :id", conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("id", id));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ==================== 12. DORMITORY / QUARTER SETUP ====================
    public async Task<List<DormitoryItem>> GetDormitoriesAsync(CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        const string sql = """
            SELECT F.FLOOR_ID, NVL(F.FLOOR_NAME, '') AS FLOOR_NAME, 
                   NVL(F.BANG_FLOOR_NAME, '') AS BANG_FLOOR_NAME,
                   F.UNIT_ID, NVL(U.UNIT_NAME, '') AS UNIT_NAME,
                   NVL(F.SHOW_TOGETHER, 1) AS SHOW_TOGETHER
            FROM FLOOR F
            LEFT JOIN UNIT U ON F.UNIT_ID = U.UNIT_ID
            ORDER BY F.FLOOR_ID
            """;
        await using var cmd = new OracleCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<DormitoryItem>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new DormitoryItem
            {
                DormitoryId = Convert.ToInt32(reader["FLOOR_ID"]),
                DormitoryName = Convert.ToString(reader["FLOOR_NAME"]) ?? "",
                DormitoryNameBang = EmployeeCsvHelper.EnsureUnicode(Convert.ToString(reader["BANG_FLOOR_NAME"])),
                UnitId = reader["UNIT_ID"] == DBNull.Value ? null : Convert.ToInt32(reader["UNIT_ID"]),
                UnitName = Convert.ToString(reader["UNIT_NAME"]),
                ShowTogether = reader["SHOW_TOGETHER"] == DBNull.Value ? 1 : Convert.ToInt32(reader["SHOW_TOGETHER"])
            });
        }
        return list;
    }

    public async Task SaveDormitoryAsync(DormitoryItem item, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        if (item.DormitoryId > 0)
        {
            const string sql = """
                UPDATE FLOOR
                SET FLOOR_NAME = :name, BANG_FLOOR_NAME = :nameBang,
                    UNIT_ID = :unitId, SHOW_TOGETHER = :showTogether
                WHERE FLOOR_ID = :id
                """;
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("name", item.DormitoryName.Trim()));
            cmd.Parameters.Add(new OracleParameter("nameBang", OracleDbType.NVarchar2) { Value = item.DormitoryNameBang?.Trim() ?? "" });
            cmd.Parameters.Add(new OracleParameter("unitId", item.UnitId ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("showTogether", item.ShowTogether ?? 1));
            cmd.Parameters.Add(new OracleParameter("id", item.DormitoryId));
            await cmd.ExecuteNonQueryAsync(ct);
        }
        else
        {
            const string nextIdSql = "SELECT NVL(MAX(FLOOR_ID), 0) + 1 FROM FLOOR";
            await using var nextIdCmd = new OracleCommand(nextIdSql, conn);
            var nextId = Convert.ToInt32(await nextIdCmd.ExecuteScalarAsync(ct));

            const string sql = """
                INSERT INTO FLOOR (FLOOR_ID, FLOOR_NAME, BANG_FLOOR_NAME, UNIT_ID, SHOW_TOGETHER)
                VALUES (:id, :name, :nameBang, :unitId, :showTogether)
                """;
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("id", nextId));
            cmd.Parameters.Add(new OracleParameter("name", item.DormitoryName.Trim()));
            cmd.Parameters.Add(new OracleParameter("nameBang", OracleDbType.NVarchar2) { Value = item.DormitoryNameBang?.Trim() ?? "" });
            cmd.Parameters.Add(new OracleParameter("unitId", item.UnitId ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("showTogether", item.ShowTogether ?? 1));
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task DeleteDormitoryAsync(int id, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new OracleCommand("DELETE FROM FLOOR WHERE FLOOR_ID = :id", conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("id", id));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public Task<List<BuildingFloorItem>> GetBuildingFloorsAsync(CancellationToken ct = default) =>
        GetDormitoriesAsync(ct).ContinueWith(t => t.Result.Select(d => new BuildingFloorItem { FloorId = d.DormitoryId, FloorName = d.DormitoryName, FloorNameBang = d.DormitoryNameBang, UnitId = d.UnitId, UnitName = d.UnitName, ShowTogether = d.ShowTogether }).ToList(), ct);

    public Task SaveBuildingFloorAsync(BuildingFloorItem item, CancellationToken ct = default) => SaveDormitoryAsync(item, ct);
    public Task DeleteBuildingFloorAsync(int id, CancellationToken ct = default) => DeleteDormitoryAsync(id, ct);
}

