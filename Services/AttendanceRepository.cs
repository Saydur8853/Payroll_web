using System.Data;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using TG.Payroll.Web.Models;

namespace TG.Payroll.Web.Services;

public class AttendanceRepository
{
    private readonly string _connectionString;

    public AttendanceRepository(IConfiguration configuration)
    {
        _connectionString = DatabaseOptions.GetConnectionString(configuration);
    }

    private OracleConnection GetConnection() => new(_connectionString);

    public async Task<List<ShiftOptionItem>> GetShiftsAsync(CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        const string sql = "SELECT SHIFT_ID, SHIFT_NAME FROM SHIFT_INFO ORDER BY SHIFT_NAME";
        await using var cmd = new OracleCommand(sql, conn);
        var list = new List<ShiftOptionItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new ShiftOptionItem
            {
                ShiftId = Convert.ToInt32(reader["SHIFT_ID"]),
                ShiftName = Convert.ToString(reader["SHIFT_NAME"]) ?? ""
            });
        }
        return list;
    }

    // =========================================================================
    // 1. ATTENDANCE DETAILS (STANDARD REGISTER)
    // =========================================================================
    public async Task<List<AttendanceRecordItem>> GetAttendanceRecordsAsync(AttendanceFilter filter, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var sql = """
            SELECT C.EMP_ID, NVL(A.EMP_CODE, '') AS EMP_CODE, NVL(A.EMP_NAME, '') AS EMP_NAME,
                   NVL(D.DESIGNATION_NAME, '') AS DESIGNATION_NAME,
                   NVL(DEP.DEPARTMENT_NAME, '') AS DEPARTMENT_NAME,
                   NVL(SEC.SECTION_NAME, '') AS SECTION_NAME,
                   NVL(L.LINE_NAME, '') AS LINE_NAME,
                   NVL(C.SHIFT_ID, NVL(A.SHIFT_ID, 0)) AS SHIFT_ID,
                   NVL(SF.SHIFT_NAME, '') AS SHIFT_NAME,
                   C.ATTD_DATE,
                   C.IN_TIME, C.OUT_TIME,
                   NVL(C.LATE, 0) AS LATE,
                   NVL(ROUND((C.OUT_TIME - C.IN_TIME) * 24, 1), 0) AS WORKING_HOUR,
                   NVL(C.OVER_TIME, 0) AS OVER_TIME,
                   NVL(C.STATUS, 'P') AS STATUS,
                   NVL(C.STATUS2, 'P') AS STATUS2,
                   NVL(C.NIGHT_STATUS, 'N') AS NIGHT_STATUS,
                   NVL(C.ATTD_LOCKED, 'N') AS ATTD_LOCKED,
                   C.ATTD_REMARKS,
                   NVL(C.IS_IN_TIME_MANUAL, 0) AS IS_IN_TIME_MANUAL,
                   NVL(C.IS_OUT_TIME_MANUAL, 0) AS IS_OUT_TIME_MANUAL
            FROM ATTENDANCE_DETAILS C
            INNER JOIN EMP_OFFICIAL A ON C.EMP_ID = A.EMP_ID
            LEFT JOIN DESIGNATION D ON A.DESIGNATION_ID = D.DESIGNATION_ID
            LEFT JOIN DEPARTMENT DEP ON A.DEPARTMENT_ID = DEP.DEPARTMENT_ID
            LEFT JOIN SECTION SEC ON A.SECTION_ID = SEC.SECTION_ID
            LEFT JOIN LINE L ON A.LINE_ID = L.LINE_ID
            LEFT JOIN SHIFT_INFO SF ON C.SHIFT_ID = SF.SHIFT_ID
            WHERE C.ATTD_DATE BETWEEN :fromDate AND :toDate
            """;

        if (filter.UnitId.HasValue && filter.UnitId > 0) sql += " AND A.UNIT_ID = :unitId";
        if (filter.DepartmentId.HasValue && filter.DepartmentId > 0) sql += " AND A.DEPARTMENT_ID = :deptId";
        if (filter.SectionId.HasValue && filter.SectionId > 0) sql += " AND A.SECTION_ID = :secId";
        if (filter.LineId.HasValue && filter.LineId > 0) sql += " AND A.LINE_ID = :lineId";
        if (filter.ShiftId.HasValue && filter.ShiftId > 0) sql += " AND C.SHIFT_ID = :shiftId";
        if (filter.CategoryId.HasValue && filter.CategoryId > 0) sql += " AND A.EMP_CATEGORY_ID = :catId";
        if (!string.IsNullOrWhiteSpace(filter.EmpCode)) sql += " AND A.EMP_CODE = :empCode";
        if (!string.IsNullOrWhiteSpace(filter.Status)) sql += " AND C.STATUS = :status";

        sql += " ORDER BY C.ATTD_DATE DESC, DEP.DEPARTMENT_NAME, TO_NUMBER(REGEXP_SUBSTR(A.EMP_CODE, '^[0-9]+'))";

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("fromDate", filter.FromDate));
        cmd.Parameters.Add(new OracleParameter("toDate", filter.ToDate));
        if (filter.UnitId.HasValue && filter.UnitId > 0) cmd.Parameters.Add(new OracleParameter("unitId", filter.UnitId.Value));
        if (filter.DepartmentId.HasValue && filter.DepartmentId > 0) cmd.Parameters.Add(new OracleParameter("deptId", filter.DepartmentId.Value));
        if (filter.SectionId.HasValue && filter.SectionId > 0) cmd.Parameters.Add(new OracleParameter("secId", filter.SectionId.Value));
        if (filter.LineId.HasValue && filter.LineId > 0) cmd.Parameters.Add(new OracleParameter("lineId", filter.LineId.Value));
        if (filter.ShiftId.HasValue && filter.ShiftId > 0) cmd.Parameters.Add(new OracleParameter("shiftId", filter.ShiftId.Value));
        if (filter.CategoryId.HasValue && filter.CategoryId > 0) cmd.Parameters.Add(new OracleParameter("catId", filter.CategoryId.Value));
        if (!string.IsNullOrWhiteSpace(filter.EmpCode)) cmd.Parameters.Add(new OracleParameter("empCode", filter.EmpCode.Trim()));
        if (!string.IsNullOrWhiteSpace(filter.Status)) cmd.Parameters.Add(new OracleParameter("status", filter.Status.Trim()));

        var list = new List<AttendanceRecordItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var inTimeVal = reader["IN_TIME"] == DBNull.Value ? null : Convert.ToDateTime(reader["IN_TIME"]).ToString("hh:mm tt");
            var outTimeVal = reader["OUT_TIME"] == DBNull.Value ? null : Convert.ToDateTime(reader["OUT_TIME"]).ToString("hh:mm tt");

            list.Add(new AttendanceRecordItem
            {
                EmpId = Convert.ToInt32(reader["EMP_ID"]),
                EmpCode = Convert.ToString(reader["EMP_CODE"]) ?? "",
                EmpName = Convert.ToString(reader["EMP_NAME"]) ?? "",
                DesignationName = Convert.ToString(reader["DESIGNATION_NAME"]) ?? "",
                DepartmentName = Convert.ToString(reader["DEPARTMENT_NAME"]) ?? "",
                SectionName = Convert.ToString(reader["SECTION_NAME"]) ?? "",
                LineName = Convert.ToString(reader["LINE_NAME"]) ?? "",
                ShiftId = Convert.ToInt32(reader["SHIFT_ID"]),
                ShiftName = Convert.ToString(reader["SHIFT_NAME"]) ?? "",
                AttdDate = Convert.ToDateTime(reader["ATTD_DATE"]),
                InTime = inTimeVal,
                OutTime = outTimeVal,
                LateMinutes = Convert.ToDecimal(reader["LATE"]),
                WorkingHours = Convert.ToDecimal(reader["WORKING_HOUR"]),
                OverTimeHours = Convert.ToDecimal(reader["OVER_TIME"]),
                Status = Convert.ToString(reader["STATUS"]) ?? "P",
                Status2 = Convert.ToString(reader["STATUS2"]) ?? "P",
                NightStatus = Convert.ToString(reader["NIGHT_STATUS"]) ?? "N",
                AttdLocked = Convert.ToString(reader["ATTD_LOCKED"]) ?? "N",
                AttdRemarks = Convert.ToString(reader["ATTD_REMARKS"]),
                IsInTimeManual = Convert.ToInt32(reader["IS_IN_TIME_MANUAL"]) == 1,
                IsOutTimeManual = Convert.ToInt32(reader["IS_OUT_TIME_MANUAL"]) == 1
            });
        }
        return list;
    }

    public async Task<AttendanceSummaryStats> GetAttendanceStatsAsync(AttendanceFilter filter, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var sql = """
            SELECT 
                COUNT(*) AS TOTAL_COUNT,
                NVL(SUM(CASE WHEN C.STATUS = 'P' THEN 1 ELSE 0 END), 0) AS PRESENT_COUNT,
                NVL(SUM(CASE WHEN C.STATUS = 'A' THEN 1 ELSE 0 END), 0) AS ABSENT_COUNT,
                NVL(SUM(CASE WHEN C.STATUS = 'L' THEN 1 ELSE 0 END), 0) AS LEAVE_COUNT,
                NVL(SUM(CASE WHEN C.STATUS = 'H' THEN 1 ELSE 0 END), 0) AS HOLIDAY_COUNT,
                NVL(SUM(CASE WHEN C.STATUS = 'W' THEN 1 ELSE 0 END), 0) AS WEEKEND_COUNT,
                NVL(SUM(NVL(C.OVER_TIME, 0)), 0) AS TOTAL_OT,
                NVL(SUM(CASE WHEN C.NIGHT_STATUS = 'Y' THEN 1 ELSE 0 END), 0) AS NIGHT_COUNT
            FROM ATTENDANCE_DETAILS C
            INNER JOIN EMP_OFFICIAL A ON C.EMP_ID = A.EMP_ID
            WHERE C.ATTD_DATE BETWEEN :fromDate AND :toDate
            """;

        if (filter.UnitId.HasValue && filter.UnitId > 0) sql += " AND A.UNIT_ID = :unitId";
        if (filter.DepartmentId.HasValue && filter.DepartmentId > 0) sql += " AND A.DEPARTMENT_ID = :deptId";
        if (!string.IsNullOrWhiteSpace(filter.EmpCode)) sql += " AND A.EMP_CODE = :empCode";

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("fromDate", filter.FromDate));
        cmd.Parameters.Add(new OracleParameter("toDate", filter.ToDate));
        if (filter.UnitId.HasValue && filter.UnitId > 0) cmd.Parameters.Add(new OracleParameter("unitId", filter.UnitId.Value));
        if (filter.DepartmentId.HasValue && filter.DepartmentId > 0) cmd.Parameters.Add(new OracleParameter("deptId", filter.DepartmentId.Value));
        if (!string.IsNullOrWhiteSpace(filter.EmpCode)) cmd.Parameters.Add(new OracleParameter("empCode", filter.EmpCode.Trim()));

        var stats = new AttendanceSummaryStats();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            stats.TotalCount = Convert.ToInt32(reader["TOTAL_COUNT"]);
            stats.PresentCount = Convert.ToInt32(reader["PRESENT_COUNT"]);
            stats.AbsentCount = Convert.ToInt32(reader["ABSENT_COUNT"]);
            stats.LeaveCount = Convert.ToInt32(reader["LEAVE_COUNT"]);
            stats.HolidayCount = Convert.ToInt32(reader["HOLIDAY_COUNT"]);
            stats.WeekendCount = Convert.ToInt32(reader["WEEKEND_COUNT"]);
            stats.TotalOtHours = Convert.ToDecimal(reader["TOTAL_OT"]);
            stats.NightCount = Convert.ToInt32(reader["NIGHT_COUNT"]);
        }
        return stats;
    }

    public async Task UpdateAttendanceRecordAsync(AttendanceRecordItem item, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        DateTime? inTimeDt = null;
        if (!string.IsNullOrWhiteSpace(item.InTime) && DateTime.TryParse(item.AttdDate.ToString("yyyy-MM-dd") + " " + item.InTime, out var inParsed))
        {
            inTimeDt = inParsed;
        }

        DateTime? outTimeDt = null;
        if (!string.IsNullOrWhiteSpace(item.OutTime) && DateTime.TryParse(item.AttdDate.ToString("yyyy-MM-dd") + " " + item.OutTime, out var outParsed))
        {
            outTimeDt = outParsed;
        }

        const string sql = """
            UPDATE ATTENDANCE_DETAILS
            SET IN_TIME = :inTime,
                OUT_TIME = :outTime,
                STATUS = :status,
                STATUS2 = :status2,
                OVER_TIME = :ot,
                LATE = :late,
                NIGHT_STATUS = :night,
                ATTD_REMARKS = :remarks,
                ATTD_LOCKED = 'Y',
                IS_IN_TIME_MANUAL = 1,
                IS_OUT_TIME_MANUAL = 1
            WHERE EMP_ID = :empId AND ATTD_DATE = :attdDate
            """;

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("inTime", inTimeDt.HasValue ? (object)inTimeDt.Value : DBNull.Value));
        cmd.Parameters.Add(new OracleParameter("outTime", outTimeDt.HasValue ? (object)outTimeDt.Value : DBNull.Value));
        cmd.Parameters.Add(new OracleParameter("status", item.Status));
        cmd.Parameters.Add(new OracleParameter("status2", item.Status2));
        cmd.Parameters.Add(new OracleParameter("ot", item.OverTimeHours));
        cmd.Parameters.Add(new OracleParameter("late", item.LateMinutes));
        cmd.Parameters.Add(new OracleParameter("night", item.NightStatus));
        cmd.Parameters.Add(new OracleParameter("remarks", item.AttdRemarks ?? (object)DBNull.Value));
        cmd.Parameters.Add(new OracleParameter("empId", item.EmpId));
        cmd.Parameters.Add(new OracleParameter("attdDate", item.AttdDate));

        await cmd.ExecuteNonQueryAsync(ct);
    }

    // =========================================================================
    // 2. RAW ATTENDANCE DATA (DEVICE PUNCH AUDIT)
    // =========================================================================
    public async Task<List<RawAttendanceItem>> GetRawAttendanceDataAsync(AttendanceFilter filter, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var sql = """
            SELECT C.EMP_ID, NVL(A.EMP_CODE, '') AS EMP_CODE, NVL(A.EMP_NAME, '') AS EMP_NAME,
                   NVL(DEP.DEPARTMENT_NAME, '') AS DEPARTMENT_NAME,
                   NVL(SF.SHIFT_NAME, '') AS SHIFT_NAME,
                   C.ATTD_DATE,
                   C.IN_TIME, C.OUT_TIME,
                   NVL(C.STATUS, 'P') AS STATUS,
                   NVL(C.OVER_TIME, 0) AS OVER_TIME,
                   C.MACHINE_NO,
                   NVL(C.ATTD_LOCKED, 'N') AS ATTD_LOCKED,
                   NVL(C.IS_IN_TIME_MANUAL, 0) AS IS_MANUAL,
                   C.ATTD_REMARKS
            FROM ATTENDANCE_DETAILS C
            INNER JOIN EMP_OFFICIAL A ON C.EMP_ID = A.EMP_ID
            LEFT JOIN DEPARTMENT DEP ON A.DEPARTMENT_ID = DEP.DEPARTMENT_ID
            LEFT JOIN SHIFT_INFO SF ON C.SHIFT_ID = SF.SHIFT_ID
            WHERE C.ATTD_DATE BETWEEN :fromDate AND :toDate
            """;

        if (filter.DepartmentId.HasValue && filter.DepartmentId > 0) sql += " AND A.DEPARTMENT_ID = :deptId";
        if (!string.IsNullOrWhiteSpace(filter.EmpCode)) sql += " AND A.EMP_CODE = :empCode";
        sql += " ORDER BY C.ATTD_DATE DESC, TO_NUMBER(REGEXP_SUBSTR(A.EMP_CODE, '^[0-9]+'))";

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("fromDate", filter.FromDate));
        cmd.Parameters.Add(new OracleParameter("toDate", filter.ToDate));
        if (filter.DepartmentId.HasValue && filter.DepartmentId > 0) cmd.Parameters.Add(new OracleParameter("deptId", filter.DepartmentId.Value));
        if (!string.IsNullOrWhiteSpace(filter.EmpCode)) cmd.Parameters.Add(new OracleParameter("empCode", filter.EmpCode.Trim()));

        var list = new List<RawAttendanceItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new RawAttendanceItem
            {
                EmpId = Convert.ToInt32(reader["EMP_ID"]),
                EmpCode = Convert.ToString(reader["EMP_CODE"]) ?? "",
                EmpName = Convert.ToString(reader["EMP_NAME"]) ?? "",
                DepartmentName = Convert.ToString(reader["DEPARTMENT_NAME"]) ?? "",
                ShiftName = Convert.ToString(reader["SHIFT_NAME"]) ?? "",
                AttdDate = Convert.ToDateTime(reader["ATTD_DATE"]),
                RawInTime = reader["IN_TIME"] == DBNull.Value ? null : Convert.ToDateTime(reader["IN_TIME"]).ToString("yyyy-MM-dd hh:mm:ss tt"),
                RawOutTime = reader["OUT_TIME"] == DBNull.Value ? null : Convert.ToDateTime(reader["OUT_TIME"]).ToString("yyyy-MM-dd hh:mm:ss tt"),
                Status = Convert.ToString(reader["STATUS"]) ?? "P",
                OverTime = Convert.ToDecimal(reader["OVER_TIME"]),
                MachineNo = Convert.ToString(reader["MACHINE_NO"]),
                AttdLocked = Convert.ToString(reader["ATTD_LOCKED"]) ?? "N",
                IsManual = Convert.ToInt32(reader["IS_MANUAL"]) == 1,
                Remarks = Convert.ToString(reader["ATTD_REMARKS"])
            });
        }
        return list;
    }

    // =========================================================================
    // 3. MICRO ATTENDANCE (SPECIAL ROSTER & SHIFT PROCESSOR)
    // =========================================================================
    public async Task<List<MicroAttendanceItem>> GetMicroAttendanceRecordsAsync(AttendanceFilter filter, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var sql = """
            SELECT C.EMP_ID, NVL(A.EMP_CODE, '') AS EMP_CODE, NVL(A.EMP_NAME, '') AS EMP_NAME,
                   NVL(DEP.DEPARTMENT_NAME, '') AS DEPARTMENT_NAME,
                   NVL(SF.SHIFT_NAME, '') AS SHIFT_NAME,
                   C.ATTD_DATE,
                   C.IN_TIME, C.OUT_TIME,
                   C.OUT_TIME2,
                   NVL(C.STATUS, 'P') AS STATUS,
                   NVL(C.OVER_TIME, 0) AS OVER_TIME,
                   NVL(C.OVER_TIME2, 0) AS OVER_TIME2,
                   C.MACHINE_NO,
                   C.ATTD_REMARKS
            FROM ATTENDANCE_MICRO C
            INNER JOIN EMP_OFFICIAL A ON C.EMP_ID = A.EMP_ID
            LEFT JOIN DEPARTMENT DEP ON A.DEPARTMENT_ID = DEP.DEPARTMENT_ID
            LEFT JOIN SHIFT_INFO SF ON C.SHIFT_ID = SF.SHIFT_ID
            WHERE C.ATTD_DATE BETWEEN :fromDate AND :toDate
            """;

        if (filter.DepartmentId.HasValue && filter.DepartmentId > 0) sql += " AND A.DEPARTMENT_ID = :deptId";
        if (!string.IsNullOrWhiteSpace(filter.EmpCode)) sql += " AND A.EMP_CODE = :empCode";
        sql += " ORDER BY C.ATTD_DATE DESC, TO_NUMBER(REGEXP_SUBSTR(A.EMP_CODE, '^[0-9]+'))";

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("fromDate", filter.FromDate));
        cmd.Parameters.Add(new OracleParameter("toDate", filter.ToDate));
        if (filter.DepartmentId.HasValue && filter.DepartmentId > 0) cmd.Parameters.Add(new OracleParameter("deptId", filter.DepartmentId.Value));
        if (!string.IsNullOrWhiteSpace(filter.EmpCode)) cmd.Parameters.Add(new OracleParameter("empCode", filter.EmpCode.Trim()));

        var list = new List<MicroAttendanceItem>();
        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(new MicroAttendanceItem
                {
                    EmpId = Convert.ToInt32(reader["EMP_ID"]),
                    EmpCode = Convert.ToString(reader["EMP_CODE"]) ?? "",
                    EmpName = Convert.ToString(reader["EMP_NAME"]) ?? "",
                    DepartmentName = Convert.ToString(reader["DEPARTMENT_NAME"]) ?? "",
                    ShiftName = Convert.ToString(reader["SHIFT_NAME"]) ?? "",
                    AttdDate = Convert.ToDateTime(reader["ATTD_DATE"]),
                    InTime = reader["IN_TIME"] == DBNull.Value ? null : Convert.ToDateTime(reader["IN_TIME"]).ToString("hh:mm tt"),
                    OutTime = reader["OUT_TIME"] == DBNull.Value ? null : Convert.ToDateTime(reader["OUT_TIME"]).ToString("hh:mm tt"),
                    OutTime2 = reader["OUT_TIME2"] == DBNull.Value ? null : Convert.ToDateTime(reader["OUT_TIME2"]).ToString("hh:mm tt"),
                    Status = Convert.ToString(reader["STATUS"]) ?? "P",
                    OverTime = Convert.ToDecimal(reader["OVER_TIME"]),
                    OverTime2 = Convert.ToDecimal(reader["OVER_TIME2"]),
                    MachineNo = Convert.ToString(reader["MACHINE_NO"]),
                    Remarks = Convert.ToString(reader["ATTD_REMARKS"])
                });
            }
        }
        catch (OracleException)
        {
            // Table ATTENDANCE_MICRO may be populated dynamically or fallback to ATTENDANCE_DETAILS
            return [];
        }

        return list;
    }

    public async Task SaveMicroAttendanceAsync(MicroAttendanceItem item, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        const string delSql = "DELETE FROM ATTENDANCE_MICRO WHERE EMP_ID = :empId AND ATTD_DATE = :attdDate";
        await using var delCmd = new OracleCommand(delSql, conn) { BindByName = true };
        delCmd.Parameters.Add(new OracleParameter("empId", item.EmpId));
        delCmd.Parameters.Add(new OracleParameter("attdDate", item.AttdDate));
        await delCmd.ExecuteNonQueryAsync(ct);

        const string insSql = """
            INSERT INTO ATTENDANCE_MICRO (
                EMP_ID, ATTD_DATE, IN_TIME, OUT_TIME, OUT_TIME2, STATUS, OVER_TIME, OVER_TIME2, ATTD_REMARKS
            ) VALUES (
                :empId, :attdDate, :inTime, :outTime, :outTime2, :status, :ot, :ot2, :remarks
            )
            """;
        await using var insCmd = new OracleCommand(insSql, conn) { BindByName = true };
        insCmd.Parameters.Add(new OracleParameter("empId", item.EmpId));
        insCmd.Parameters.Add(new OracleParameter("attdDate", item.AttdDate));
        insCmd.Parameters.Add(new OracleParameter("inTime", string.IsNullOrWhiteSpace(item.InTime) ? DBNull.Value : (object)DateTime.Parse(item.AttdDate.ToString("yyyy-MM-dd") + " " + item.InTime)));
        insCmd.Parameters.Add(new OracleParameter("outTime", string.IsNullOrWhiteSpace(item.OutTime) ? DBNull.Value : (object)DateTime.Parse(item.AttdDate.ToString("yyyy-MM-dd") + " " + item.OutTime)));
        insCmd.Parameters.Add(new OracleParameter("outTime2", string.IsNullOrWhiteSpace(item.OutTime2) ? DBNull.Value : (object)DateTime.Parse(item.AttdDate.ToString("yyyy-MM-dd") + " " + item.OutTime2)));
        insCmd.Parameters.Add(new OracleParameter("status", item.Status));
        insCmd.Parameters.Add(new OracleParameter("ot", item.OverTime));
        insCmd.Parameters.Add(new OracleParameter("ot2", item.OverTime2));
        insCmd.Parameters.Add(new OracleParameter("remarks", item.Remarks ?? (object)DBNull.Value));

        await insCmd.ExecuteNonQueryAsync(ct);
    }

    // =========================================================================
    // 4. UPLOAD ATTENDANCE FILE & AUTO-PROCESS ENGINE
    // =========================================================================
    public async Task<AttendanceProcessResult> ProcessAttendanceForDateRangeAsync(
        DateTime fromDate, DateTime toDate, int? unitId, int? deptId, int? shiftId, int currentUserId, CancellationToken ct = default)
    {
        var result = new AttendanceProcessResult();
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        // Fetch active employees
        var empSql = "SELECT EMP_ID, EMP_CODE, SHIFT_ID, NVL(WEEKEND, 'Friday') AS WEEKEND FROM EMP_OFFICIAL WHERE UPPER(EMP_STATUS) = 'ACTIVE'";
        if (unitId.HasValue && unitId > 0) empSql += " AND UNIT_ID = " + unitId.Value;
        if (deptId.HasValue && deptId > 0) empSql += " AND DEPARTMENT_ID = " + deptId.Value;
        if (shiftId.HasValue && shiftId > 0) empSql += " AND SHIFT_ID = " + shiftId.Value;

        var employees = new List<(int EmpId, string EmpCode, int ShiftId, string Weekend)>();
        await using (var empCmd = new OracleCommand(empSql, conn))
        {
            await using var reader = await empCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                employees.Add((
                    Convert.ToInt32(reader["EMP_ID"]),
                    Convert.ToString(reader["EMP_CODE"]) ?? "",
                    reader["SHIFT_ID"] == DBNull.Value ? 1 : Convert.ToInt32(reader["SHIFT_ID"]),
                    Convert.ToString(reader["WEEKEND"]) ?? "Friday"
                ));
            }
        }

        // Fetch Holidays in date range
        var holidayDates = new HashSet<DateTime>();
        const string holSql = "SELECT DT FROM HOLIDAY WHERE DT BETWEEN :fDate AND :tDate";
        await using (var holCmd = new OracleCommand(holSql, conn) { BindByName = true })
        {
            holCmd.Parameters.Add(new OracleParameter("fDate", fromDate));
            holCmd.Parameters.Add(new OracleParameter("tDate", toDate));
            await using var reader = await holCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                holidayDates.Add(Convert.ToDateTime(reader["DT"]).Date);
            }
        }

        // Fetch Approved Leaves in date range
        var leaveEntries = new Dictionary<(int EmpId, DateTime Date), string>();
        const string leaveSql = "SELECT EMP_ID, FROM_DATE, TO_DATE, NVL(TYPE, 'CL') AS TYPE FROM LEAVE WHERE TO_DATE >= :fDate AND FROM_DATE <= :tDate";
        await using (var leaveCmd = new OracleCommand(leaveSql, conn) { BindByName = true })
        {
            leaveCmd.Parameters.Add(new OracleParameter("fDate", fromDate));
            leaveCmd.Parameters.Add(new OracleParameter("tDate", toDate));
            await using var reader = await leaveCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var eId = Convert.ToInt32(reader["EMP_ID"]);
                var fD = Convert.ToDateTime(reader["FROM_DATE"]).Date;
                var tD = Convert.ToDateTime(reader["TO_DATE"]).Date;
                var lType = Convert.ToString(reader["TYPE"]) ?? "CL";
                for (var cur = fD; cur <= tD; cur = cur.AddDays(1))
                {
                    leaveEntries[(eId, cur)] = lType;
                }
            }
        }

        var totalDays = (toDate.Date - fromDate.Date).Days + 1;
        for (var day = 0; day < totalDays; day++)
        {
            var curDate = fromDate.Date.AddDays(day);
            var dayName = curDate.ToString("dddd");

            foreach (var emp in employees)
            {
                // Determine base status
                var status = "A"; // Absent by default
                var status2 = "A";
                var remarks = "";

                if (leaveEntries.TryGetValue((emp.EmpId, curDate), out var lType))
                {
                    status = "L";
                    status2 = "L";
                    remarks = lType;
                    result.UpdatedLeave++;
                }
                else if (holidayDates.Contains(curDate))
                {
                    status = "H";
                    status2 = "H";
                    remarks = "Holiday";
                    result.UpdatedWeekendHoliday++;
                }
                else if (emp.Weekend.Equals(dayName, StringComparison.OrdinalIgnoreCase) || emp.Weekend.Equals("Weekend", StringComparison.OrdinalIgnoreCase) && dayName == "Friday")
                {
                    status = "W";
                    status2 = "W";
                    remarks = "Weekend";
                    result.UpdatedWeekendHoliday++;
                }
                else
                {
                    result.UpdatedAbsent++;
                }

                // Check if existing record is locked
                const string checkSql = "SELECT ATTD_LOCKED, STATUS FROM ATTENDANCE_DETAILS WHERE EMP_ID = :empId AND ATTD_DATE = :attdDate";
                bool exists = false;
                bool isLocked = false;
                await using (var checkCmd = new OracleCommand(checkSql, conn) { BindByName = true })
                {
                    checkCmd.Parameters.Add(new OracleParameter("empId", emp.EmpId));
                    checkCmd.Parameters.Add(new OracleParameter("attdDate", curDate));
                    await using var rdr = await checkCmd.ExecuteReaderAsync(ct);
                    if (await rdr.ReadAsync(ct))
                    {
                        exists = true;
                        isLocked = Convert.ToString(rdr["ATTD_LOCKED"]) == "Y";
                    }
                }

                if (!exists)
                {
                    const string insSql = """
                        INSERT INTO ATTENDANCE_DETAILS (
                            EMP_ID, ATTD_DATE, STATUS, STATUS2, SHIFT_ID, USER_ID, ATTD_LOCKED, ATTD_REMARKS
                        ) VALUES (
                            :empId, :attdDate, :status, :status2, :shiftId, :userId, 'N', :remarks
                        )
                        """;
                    await using var insCmd = new OracleCommand(insSql, conn) { BindByName = true };
                    insCmd.Parameters.Add(new OracleParameter("empId", emp.EmpId));
                    insCmd.Parameters.Add(new OracleParameter("attdDate", curDate));
                    insCmd.Parameters.Add(new OracleParameter("status", status));
                    insCmd.Parameters.Add(new OracleParameter("status2", status2));
                    insCmd.Parameters.Add(new OracleParameter("shiftId", emp.ShiftId));
                    insCmd.Parameters.Add(new OracleParameter("userId", currentUserId));
                    insCmd.Parameters.Add(new OracleParameter("remarks", string.IsNullOrWhiteSpace(remarks) ? (object)DBNull.Value : remarks));
                    await insCmd.ExecuteNonQueryAsync(ct);
                }
                else if (!isLocked && (status == "L" || status == "H" || status == "W"))
                {
                    const string updSql = """
                        UPDATE ATTENDANCE_DETAILS
                        SET STATUS = :status, STATUS2 = :status2, ATTD_REMARKS = :remarks, USER_ID = :userId
                        WHERE EMP_ID = :empId AND ATTD_DATE = :attdDate AND NVL(ATTD_LOCKED, 'N') != 'Y'
                        """;
                    await using var updCmd = new OracleCommand(updSql, conn) { BindByName = true };
                    updCmd.Parameters.Add(new OracleParameter("status", status));
                    updCmd.Parameters.Add(new OracleParameter("status2", status2));
                    updCmd.Parameters.Add(new OracleParameter("remarks", string.IsNullOrWhiteSpace(remarks) ? (object)DBNull.Value : remarks));
                    updCmd.Parameters.Add(new OracleParameter("userId", currentUserId));
                    updCmd.Parameters.Add(new OracleParameter("empId", emp.EmpId));
                    updCmd.Parameters.Add(new OracleParameter("attdDate", curDate));
                    await updCmd.ExecuteNonQueryAsync(ct);
                }
                result.ProcessedEmployees++;
            }
        }

        result.SummaryMessage = $"Processed {employees.Count} employees for {totalDays} days. Absent: {result.UpdatedAbsent}, Weekend/Holiday: {result.UpdatedWeekendHoliday}, Leave: {result.UpdatedLeave}.";
        return result;
    }

    public async Task<AttendanceProcessResult> UploadAndProcessPunchLogsAsync(
        List<RawPunchLogItem> rawLogs, DateTime fromDate, DateTime toDate, int currentUserId, CancellationToken ct = default)
    {
        var result = new AttendanceProcessResult { TotalPunches = rawLogs.Count };
        if (rawLogs.Count == 0)
        {
            result.SummaryMessage = "No punch logs provided to process.";
            return result;
        }

        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        // Pre-initialize default attendance rows for date range
        await ProcessAttendanceForDateRangeAsync(fromDate, toDate, null, null, null, currentUserId, ct);

        // Fetch shift details
        var shiftMap = new Dictionary<int, (DateTime InTime, DateTime OutTime, int Grace)>();
        const string shiftSql = "SELECT SHIFT_ID, IN_TIME, OUT_TIME, NVL(GRACE, 0) AS GRACE FROM SHIFT_INFO";
        await using (var sCmd = new OracleCommand(shiftSql, conn))
        {
            await using var sRdr = await sCmd.ExecuteReaderAsync(ct);
            while (await sRdr.ReadAsync(ct))
            {
                var sId = Convert.ToInt32(sRdr["SHIFT_ID"]);
                var inT = sRdr["IN_TIME"] == DBNull.Value ? DateTime.Today.AddHours(8) : Convert.ToDateTime(sRdr["IN_TIME"]);
                var outT = sRdr["OUT_TIME"] == DBNull.Value ? DateTime.Today.AddHours(17) : Convert.ToDateTime(sRdr["OUT_TIME"]);
                var grc = Convert.ToInt32(sRdr["GRACE"]);
                shiftMap[sId] = (inT, outT, grc);
            }
        }

        // Fetch employee mapping
        var empMap = new Dictionary<string, (int EmpId, int ShiftId)>(StringComparer.OrdinalIgnoreCase);
        const string empSql = "SELECT EMP_ID, EMP_CODE, NVL(SHIFT_ID, 1) AS SHIFT_ID FROM EMP_OFFICIAL WHERE UPPER(EMP_STATUS) = 'ACTIVE'";
        await using (var eCmd = new OracleCommand(empSql, conn))
        {
            await using var eRdr = await eCmd.ExecuteReaderAsync(ct);
            while (await eRdr.ReadAsync(ct))
            {
                var code = Convert.ToString(eRdr["EMP_CODE"]) ?? "";
                var empId = Convert.ToInt32(eRdr["EMP_ID"]);
                var sId = Convert.ToInt32(eRdr["SHIFT_ID"]);
                empMap[code] = (empId, sId);
            }
        }

        // Group punches by EmpCode and Punch Date
        var groupedPunches = rawLogs
            .Where(p => !string.IsNullOrWhiteSpace(p.EmpCode))
            .GroupBy(p => (EmpCode: p.EmpCode.Trim(), Date: p.PunchDateTime.Date));

        foreach (var group in groupedPunches)
        {
            if (!empMap.TryGetValue(group.Key.EmpCode, out var empInfo))
            {
                result.SkippedLogs += group.Count();
                continue;
            }

            var punches = group.OrderBy(p => p.PunchDateTime).ToList();
            var firstPunch = punches.First().PunchDateTime;
            var lastPunch = punches.Count > 1 ? punches.Last().PunchDateTime : (DateTime?)null;
            var machineNo = punches.First().MachineNo ?? "1";

            var shiftId = empInfo.ShiftId;
            shiftMap.TryGetValue(shiftId, out var shiftTimes);

            // Compute late minutes
            decimal lateMinutes = 0;
            if (shiftTimes.InTime != default)
            {
                var shiftExpectedIn = group.Key.Date.Add(shiftTimes.InTime.TimeOfDay).AddMinutes(shiftTimes.Grace);
                if (firstPunch > shiftExpectedIn)
                {
                    lateMinutes = (decimal)Math.Round((firstPunch - shiftExpectedIn).TotalMinutes);
                }
            }

            // Compute OT hours if last punch exists
            decimal otHours = 0;
            if (lastPunch.HasValue && shiftTimes.OutTime != default)
            {
                var shiftExpectedOut = group.Key.Date.Add(shiftTimes.OutTime.TimeOfDay);
                if (lastPunch.Value > shiftExpectedOut)
                {
                    otHours = (decimal)Math.Round((lastPunch.Value - shiftExpectedOut).TotalHours, 1);
                }
            }

            // Update ATTENDANCE_DETAILS
            const string updAttdSql = """
                UPDATE ATTENDANCE_DETAILS
                SET IN_TIME = :inTime,
                    OUT_TIME = :outTime,
                    STATUS = 'P',
                    STATUS2 = 'P',
                    LATE = :late,
                    OVER_TIME = :ot,
                    MACHINE_NO = :machineNo,
                    IS_IN_TIME_MANUAL = 0,
                    IS_OUT_TIME_MANUAL = 0
                WHERE EMP_ID = :empId AND ATTD_DATE = :attdDate AND NVL(ATTD_LOCKED, 'N') != 'Y'
                """;

            await using var updCmd = new OracleCommand(updAttdSql, conn) { BindByName = true };
            updCmd.Parameters.Add(new OracleParameter("inTime", firstPunch));
            updCmd.Parameters.Add(new OracleParameter("outTime", lastPunch.HasValue ? (object)lastPunch.Value : DBNull.Value));
            updCmd.Parameters.Add(new OracleParameter("late", lateMinutes));
            updCmd.Parameters.Add(new OracleParameter("ot", otHours));
            updCmd.Parameters.Add(new OracleParameter("machineNo", machineNo));
            updCmd.Parameters.Add(new OracleParameter("empId", empInfo.EmpId));
            updCmd.Parameters.Add(new OracleParameter("attdDate", group.Key.Date));

            var rowsUpdated = await updCmd.ExecuteNonQueryAsync(ct);
            if (rowsUpdated > 0) result.UpdatedPresent++;
        }

        result.SummaryMessage = $"Successfully uploaded and processed {rawLogs.Count} punch logs across {groupedPunches.Count()} employee-dates. Present updated: {result.UpdatedPresent}, Skipped: {result.SkippedLogs}.";
        return result;
    }
}
