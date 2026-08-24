using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using TG.Payroll.Web.Data;
using TG.Payroll.Web.Models;

namespace TG.Payroll.Web.Services;

public sealed class PayrollRepository
{
    private readonly string _connectionString;
    private readonly PayrollDbContext _db;

    public PayrollRepository(IConfiguration configuration, PayrollDbContext db)
    {
        _connectionString = DatabaseOptions.GetConnectionString(configuration);
        _db = db;
    }

    public async Task CheckConnectionAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
    }

    public async Task<CurrentUser?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var normalizedUsername = username.Trim().ToUpperInvariant();
        var matchingUsers = await _db.Users.AsNoTracking()
            .Where(candidate => candidate.UserName != null && candidate.UserName.ToUpper() == normalizedUsername && candidate.Password == password)
            .ToListAsync(cancellationToken);
        var user = matchingUsers.FirstOrDefault();
        if (user is null) return null;

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new OracleCommand("SELECT COMPANY_NAME FROM COMPANY WHERE COMPANY_ID = :companyId", connection);
        command.BindByName = true;
        command.Parameters.Add(new OracleParameter("companyId", user.CompanyId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var companyName = await reader.ReadAsync(cancellationToken)
            ? Convert.ToString(reader.GetValue(0)) ?? string.Empty
            : string.Empty;

        return new CurrentUser(
            Convert.ToInt32(user.UserId),
            user.UserName ?? string.Empty,
            Convert.ToInt32(user.CompanyId),
            companyName,
            user.Admin == 1);
    }

    public async Task<DashboardData> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var lastYearMonthStart = monthStart.AddYears(-1);
        var lastYearMonthEnd = monthEnd.AddYears(-1);
        const string sql = """
            SELECT
                (SELECT COUNT(*) FROM EMP_OFFICIAL) TOTAL_EMPLOYEES,
                (SELECT COUNT(*) FROM EMP_OFFICIAL WHERE EMP_STATUS = 'Active') ACTIVE_EMPLOYEES,
                (SELECT COUNT(*) FROM EMP_OFFICIAL WHERE EMP_STATUS = 'Inactive') INACTIVE_EMPLOYEES,
                (SELECT COUNT(*) FROM EMP_OFFICIAL WHERE DATE_OF_JOINING >= :monthStart) NEW_JOINERS,
                (SELECT COUNT(*) FROM EMP_OFFICIAL WHERE EMP_STATUS = 'Close') CLOSED_EMPLOYEES,
                (SELECT COUNT(*) FROM EMP_OFFICIAL WHERE EMP_STATUS = 'Close' AND CLOSE_DATE >= :monthStart AND NVL(RESIGN_GIVEN, 'N') = 'Y') RELEASES,
                (SELECT COUNT(*) FROM EMP_OFFICIAL E JOIN EMP_CATEGORY C ON E.EMP_CATEGORY_ID = C.EMP_CATEGORY_ID WHERE E.EMP_STATUS = 'Active' AND UPPER(C.EMP_CATEGORY_NAME) = 'WORKER') WORKERS,
                (SELECT COUNT(*) FROM EMP_OFFICIAL E JOIN EMP_CATEGORY C ON E.EMP_CATEGORY_ID = C.EMP_CATEGORY_ID WHERE E.EMP_STATUS = 'Active' AND UPPER(C.EMP_CATEGORY_NAME) NOT IN ('WORKER', 'OFFICER')) STAFF,
                (SELECT COUNT(*) FROM EMP_OFFICIAL E JOIN EMP_CATEGORY C ON E.EMP_CATEGORY_ID = C.EMP_CATEGORY_ID WHERE E.EMP_STATUS = 'Active' AND UPPER(C.EMP_CATEGORY_NAME) = 'OFFICER') OFFICERS,
                (SELECT COUNT(*) FROM EMP_OFFICIAL E JOIN EMP_PERSONAL P ON E.EMP_ID = P.EMP_ID WHERE E.EMP_STATUS = 'Active' AND UPPER(P.SEX) = 'MALE') MALE,
                (SELECT COUNT(*) FROM EMP_OFFICIAL E JOIN EMP_PERSONAL P ON E.EMP_ID = P.EMP_ID WHERE E.EMP_STATUS = 'Active' AND UPPER(P.SEX) = 'FEMALE') FEMALE,
                (SELECT COUNT(*) FROM EMP_OFFICIAL WHERE EMP_STATUS = 'Active' AND BANK_ACCOUNT_HOLDER = 'N') CASH_PAY,
                (SELECT COUNT(*) FROM EMP_OFFICIAL WHERE EMP_STATUS = 'Active' AND BANK_ACCOUNT_HOLDER = 'Y' AND TAX_HOLDER = 'N') BANK_PAY,
                (SELECT COUNT(*) FROM EMP_OFFICIAL WHERE EMP_STATUS = 'Active' AND BANK_ACCOUNT_HOLDER = 'M' AND TAX_HOLDER = 'N') MOBILE_PAY,
                (SELECT COUNT(*) FROM EMP_OFFICIAL WHERE EMP_STATUS = 'Active' AND TAX_HOLDER = 'Y') TAX_HOLDERS,
                (SELECT COUNT(*) FROM EMP_OFFICIAL WHERE EMP_STATUS = 'Active' AND LUNCH = 'Y') QUARTER_HOLDERS,
                (SELECT COUNT(*) FROM EMP_OFFICIAL WHERE EMP_STATUS = 'Active' AND DATE_OF_JOINING BETWEEN :lastYearMonthStart AND :lastYearMonthEnd) INCREMENTS,
                (SELECT COUNT(DISTINCT EMP_ID) FROM LEAVE WHERE FROM_DATE BETWEEN :monthStart AND :monthEnd) ON_LEAVE,
                (SELECT COUNT(*) FROM EMP_OFFICIAL WHERE EMP_STATUS = 'Maternity') MATERNITY
            FROM DUAL
            """;

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new OracleCommand(sql, connection);
        command.BindByName = true;
        command.Parameters.Add(new OracleParameter("monthStart", monthStart));
        command.Parameters.Add(new OracleParameter("monthEnd", monthEnd));
        command.Parameters.Add(new OracleParameter("lastYearMonthStart", lastYearMonthStart));
        command.Parameters.Add(new OracleParameter("lastYearMonthEnd", lastYearMonthEnd));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        return new DashboardData
        {
            TotalEmployees = reader.GetInt32(0), ActiveEmployees = reader.GetInt32(1), InactiveEmployees = reader.GetInt32(2),
            NewJoiners = reader.GetInt32(3), ClosedEmployees = reader.GetInt32(4), Releases = reader.GetInt32(5),
            Workers = reader.GetInt32(6), Staff = reader.GetInt32(7), Officers = reader.GetInt32(8), Male = reader.GetInt32(9),
            Female = reader.GetInt32(10), CashPay = reader.GetInt32(11), BankPay = reader.GetInt32(12), MobilePay = reader.GetInt32(13),
            TaxHolders = reader.GetInt32(14), QuarterHolders = reader.GetInt32(15), Increments = reader.GetInt32(16),
            OnLeave = reader.GetInt32(17), Maternity = reader.GetInt32(18)
        };
    }

    public async Task<List<NavigationItem>> GetMenuAsync(int userId, CancellationToken cancellationToken = default)
    {
        const string privilegesSql = "SELECT PRIVILEGE_ARRAY, NVL(ADMIN, 0) FROM USERS WHERE USER_ID = :userId";
        const string controlsSql = """
            SELECT CONTROL_ID, CONTROL_NAME, CALLING_ID
            FROM CONTROLS
            WHERE CONTROL_TYPE = 'MENU'
            ORDER BY PRIORITY, CONTROL_ID
            """;

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        object? privilegesValue;
        bool isAdmin;
        await using (var privilegesCommand = new OracleCommand(privilegesSql, connection) { BindByName = true })
        {
            privilegesCommand.Parameters.Add(new OracleParameter("userId", userId));
            await using var privilegesReader = await privilegesCommand.ExecuteReaderAsync(cancellationToken);
            if (!await privilegesReader.ReadAsync(cancellationToken)) return [];
            privilegesValue = privilegesReader.IsDBNull(0) ? null : privilegesReader.GetValue(0);
            isAdmin = Convert.ToDecimal(privilegesReader.GetValue(1)) == 1;
        }
        var permittedIds = (Convert.ToString(privilegesValue) ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var items = new List<NavigationItem>();
        await using var controlsCommand = new OracleCommand(controlsSql, connection);
        await using var reader = await controlsCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = Convert.ToInt32(reader.GetValue(0));
            if (!isAdmin && !permittedIds.Contains(id.ToString())) continue;

            items.Add(new NavigationItem
            {
                Id = id,
                Name = Convert.ToString(reader.GetValue(1)) ?? string.Empty,
                ParentId = reader.IsDBNull(2) ? null : Convert.ToInt32(reader.GetValue(2))
            });
        }

        var lookup = items.ToDictionary(item => item.Id);
        var roots = new List<NavigationItem>();
        foreach (var item in items)
        {
            if (item.ParentId is > 0 && lookup.TryGetValue(item.ParentId.Value, out var parent)) parent.Children.Add(item);
            else roots.Add(item);
        }
        return roots;
    }

    public async Task<List<PayrollUser>> GetUsersAsync(CancellationToken cancellationToken = default)
        => await _db.Users.AsNoTracking().OrderBy(user => user.UserName).ToListAsync(cancellationToken);

    public async Task<PayrollUser?> GetUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var users = await _db.Users.AsNoTracking()
            .Where(user => user.UserId == userId)
            .ToListAsync(cancellationToken);
        return users.FirstOrDefault();
    }

    public async Task<List<NavigationItem>> GetAllMenuControlsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CONTROL_ID, CONTROL_NAME, CALLING_ID
            FROM CONTROLS
            WHERE CONTROL_TYPE = 'MENU'
            ORDER BY PRIORITY, CONTROL_ID
            """;
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new OracleCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var modules = new List<NavigationItem>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var parentIdVal = reader.IsDBNull(2) ? null : (int?)Convert.ToInt32(reader.GetValue(2));
            if (parentIdVal == 0) parentIdVal = null;
            modules.Add(new NavigationItem
            {
                Id = Convert.ToInt32(reader.GetValue(0)),
                Name = Convert.ToString(reader.GetValue(1)) ?? string.Empty,
                ParentId = parentIdVal
            });
        }
        return modules;
    }

    public async Task<List<NavigationItem>> GetAllMenuTreeAsync(CancellationToken cancellationToken = default)
    {
        var flat = await GetAllMenuControlsAsync(cancellationToken);
        var lookup = flat.ToDictionary(item => item.Id, item => new NavigationItem
        {
            Id = item.Id,
            Name = item.Name,
            ParentId = item.ParentId
        });
        var roots = new List<NavigationItem>();
        foreach (var item in flat)
        {
            var node = lookup[item.Id];
            if (item.ParentId is > 0 && lookup.TryGetValue(item.ParentId.Value, out var parent))
            {
                parent.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }
        return roots;
    }

    public async Task<List<ControlItem>> GetControlsDetailedAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT c.CONTROL_ID, c.CONTROL_NAME, c.CALLING_ID, NVL(p.CONTROL_NAME, 'Root / Top Level') AS PARENT_NAME, NVL(c.CONTROL_TYPE, 'MENU') AS CONTROL_TYPE, NVL(c.PRIORITY, 0) AS PRIORITY
            FROM CONTROLS c
            LEFT JOIN CONTROLS p ON c.CALLING_ID = p.CONTROL_ID
            ORDER BY NVL(c.PRIORITY, 0), c.CONTROL_ID
            """;
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new OracleCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var list = new List<ControlItem>();
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new ControlItem
            {
                ControlId = Convert.ToInt32(reader.GetValue(0)),
                ControlName = Convert.ToString(reader.GetValue(1)) ?? string.Empty,
                CallingId = reader.IsDBNull(2) || Convert.ToInt32(reader.GetValue(2)) == 0 ? null : Convert.ToInt32(reader.GetValue(2)),
                ParentName = Convert.ToString(reader.GetValue(3)) ?? "Root / Top Level",
                ControlType = Convert.ToString(reader.GetValue(4)) ?? "MENU",
                Priority = Convert.ToInt32(reader.GetValue(5))
            });
        }
        return list;
    }

    public async Task<List<string>> GetDistinctControlTypesAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT DISTINCT NVL(TRIM(CONTROL_TYPE), 'MENU') FROM CONTROLS WHERE CONTROL_TYPE IS NOT NULL ORDER BY 1";
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new OracleCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var list = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var val = Convert.ToString(reader.GetValue(0));
            if (!string.IsNullOrWhiteSpace(val) && !list.Contains(val))
            {
                list.Add(val);
            }
        }
        if (list.Count == 0) list.AddRange(["MENU", "FORM", "REPORT", "BUTTON"]);
        return list;
    }

    public async Task<int> CreateControlAsync(string controlName, int? callingId, string controlType, int priority, CancellationToken cancellationToken = default)
    {
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string nextIdSql = "SELECT NVL(MAX(CONTROL_ID), 0) + 1 FROM CONTROLS";
        int newId;
        await using (var idCommand = new OracleCommand(nextIdSql, connection))
        {
            var scalar = await idCommand.ExecuteScalarAsync(cancellationToken);
            newId = Convert.ToInt32(scalar);
        }

        const string insertSql = """
            INSERT INTO CONTROLS (CONTROL_ID, CONTROL_NAME, CALLING_ID, CONTROL_TYPE, PRIORITY)
            VALUES (:controlId, :controlName, :callingId, :controlType, :priority)
            """;
        await using var insertCommand = new OracleCommand(insertSql, connection) { BindByName = true };
        insertCommand.Parameters.Add(new OracleParameter("controlId", newId));
        insertCommand.Parameters.Add(new OracleParameter("controlName", controlName.Trim()));
        insertCommand.Parameters.Add(new OracleParameter("callingId", callingId.HasValue && callingId.Value > 0 ? callingId.Value : 0));
        insertCommand.Parameters.Add(new OracleParameter("controlType", string.IsNullOrWhiteSpace(controlType) ? "MENU" : controlType.Trim().ToUpperInvariant()));
        insertCommand.Parameters.Add(new OracleParameter("priority", priority));
        await insertCommand.ExecuteNonQueryAsync(cancellationToken);

        return newId;
    }

    public async Task UpdateControlAsync(int controlId, string controlName, int? callingId, string controlType, int priority, CancellationToken cancellationToken = default)
    {
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string updateSql = """
            UPDATE CONTROLS
            SET CONTROL_NAME = :controlName,
                CALLING_ID = :callingId,
                CONTROL_TYPE = :controlType,
                PRIORITY = :priority
            WHERE CONTROL_ID = :controlId
            """;
        await using var updateCommand = new OracleCommand(updateSql, connection) { BindByName = true };
        updateCommand.Parameters.Add(new OracleParameter("controlName", controlName.Trim()));
        updateCommand.Parameters.Add(new OracleParameter("callingId", callingId.HasValue && callingId.Value > 0 ? callingId.Value : 0));
        updateCommand.Parameters.Add(new OracleParameter("controlType", string.IsNullOrWhiteSpace(controlType) ? "MENU" : controlType.Trim().ToUpperInvariant()));
        updateCommand.Parameters.Add(new OracleParameter("priority", priority));
        updateCommand.Parameters.Add(new OracleParameter("controlId", controlId));
        await updateCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteControlAsync(int controlId, CancellationToken cancellationToken = default)
    {
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string deleteSql = "DELETE FROM CONTROLS WHERE CONTROL_ID = :controlId";
        await using var deleteCommand = new OracleCommand(deleteSql, connection) { BindByName = true };
        deleteCommand.Parameters.Add(new OracleParameter("controlId", controlId));
        await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<List<CompanyItem>> GetCompaniesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Discover columns in COMPANY table first safely from user schema
        var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? blobColName = null;
        try
        {
            await using var colCmd = new OracleCommand("SELECT COLUMN_NAME, DATA_TYPE FROM USER_TAB_COLUMNS WHERE TABLE_NAME = 'COMPANY'", connection);
            await using var colReader = await colCmd.ExecuteReaderAsync(cancellationToken);
            while (await colReader.ReadAsync(cancellationToken))
            {
                var colName = colReader.GetString(0);
                var dataType = colReader.GetString(1);
                cols.Add(colName);
                if (dataType.Contains("BLOB", StringComparison.OrdinalIgnoreCase) || dataType.Contains("RAW", StringComparison.OrdinalIgnoreCase))
                {
                    blobColName = colName;
                }
            }
        }
        catch { }

        // Select only text/numeric columns to prevent Oracle TTC Exception on inline BLOB fetch
        var selectParts = new List<string> { "COMPANY_ID", "NVL(COMPANY_NAME, '') AS COMPANY_NAME" };
        if (cols.Contains("SHORT_NAME")) selectParts.Add("NVL(SHORT_NAME, '') AS SHORT_NAME");
        if (cols.Contains("ADDRESS")) selectParts.Add("NVL(ADDRESS, '') AS ADDRESS");
        if (cols.Contains("REMARKS")) selectParts.Add("NVL(REMARKS, '') AS REMARKS");
        if (cols.Contains("COMPANY_NAME_BANG")) selectParts.Add("NVL(COMPANY_NAME_BANG, '') AS COMPANY_NAME_BANG");
        else if (cols.Contains("BANG_COMPANY_NAME")) selectParts.Add("NVL(BANG_COMPANY_NAME, '') AS COMPANY_NAME_BANG");
        if (cols.Contains("ADDRESS_BANG")) selectParts.Add("NVL(ADDRESS_BANG, '') AS ADDRESS_BANG");
        else if (cols.Contains("BANG_COMPANY_ADDRESS")) selectParts.Add("NVL(BANG_COMPANY_ADDRESS, '') AS ADDRESS_BANG");
        if (cols.Contains("COMPANY_LOGO_PATH") && !string.Equals(blobColName, "COMPANY_LOGO_PATH", StringComparison.OrdinalIgnoreCase))
        {
            selectParts.Add("NVL(COMPANY_LOGO_PATH, '') AS COMPANY_LOGO_PATH");
        }

        var sql = $"SELECT {string.Join(", ", selectParts)} FROM COMPANY ORDER BY COMPANY_ID";
        await using var command = new OracleCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        
        var list = new List<CompanyItem>();
        while (await reader.ReadAsync(cancellationToken))
        {
            string GetVal(string col)
            {
                try
                {
                    var ord = reader.GetOrdinal(col);
                    return reader.IsDBNull(ord) ? string.Empty : Convert.ToString(reader[ord]) ?? string.Empty;
                }
                catch { return string.Empty; }
            }

            var item = new CompanyItem
            {
                CompanyId = Convert.ToInt32(reader["COMPANY_ID"]),
                CompanyName = GetVal("COMPANY_NAME"),
                CompanyNameBang = EmployeeCsvHelper.EnsureUnicode(GetVal("COMPANY_NAME_BANG")),
                ShortName = GetVal("SHORT_NAME"),
                Address = GetVal("ADDRESS"),
                AddressBang = EmployeeCsvHelper.EnsureUnicode(GetVal("ADDRESS_BANG")),
                Remarks = GetVal("REMARKS"),
                CompanyLogoPath = GetVal("COMPANY_LOGO_PATH")
            };
            list.Add(item);
        }
        await reader.DisposeAsync();

        // Read BLOB logo data safely using chunked DBMS_LOB reader
        var targetBlobCol = blobColName ?? (cols.Contains("COMPANY_LOGO") ? "COMPANY_LOGO" : (cols.Contains("LOGO") ? "LOGO" : null));
        if (!string.IsNullOrEmpty(targetBlobCol))
        {
            foreach (var item in list)
            {
                item.CompanyLogo = ReadCompanyBlob(connection, "COMPANY", targetBlobCol, item.CompanyId);
            }
        }

        return list;
    }

    private static byte[]? ReadCompanyBlob(OracleConnection connection, string table, string column, int companyId)
    {
        try
        {
            using var lengthCommand = new OracleCommand($"SELECT NVL(DBMS_LOB.GETLENGTH({column}), 0) FROM {table} WHERE COMPANY_ID = :companyId", connection) { BindByName = true };
            lengthCommand.Parameters.Add(new OracleParameter("companyId", OracleDbType.Decimal) { Value = companyId });
            var totalLength = Convert.ToInt32(lengthCommand.ExecuteScalar() ?? 0);
            if (totalLength <= 0) return null;

            const int chunkSize = 2000;
            using var output = new MemoryStream(totalLength);
            for (var offset = 1; offset <= totalLength; offset += chunkSize)
            {
                using var chunkCommand = new OracleCommand($"SELECT DBMS_LOB.SUBSTR({column}, :amount, :offset) FROM {table} WHERE COMPANY_ID = :companyId", connection) { BindByName = true };
                chunkCommand.Parameters.Add(new OracleParameter("amount", OracleDbType.Int32) { Value = chunkSize });
                chunkCommand.Parameters.Add(new OracleParameter("offset", OracleDbType.Int32) { Value = offset });
                chunkCommand.Parameters.Add(new OracleParameter("companyId", OracleDbType.Decimal) { Value = companyId });
                var value = chunkCommand.ExecuteScalar();
                if (value is byte[] bytes) output.Write(bytes, 0, bytes.Length);
                else if (value is OracleBinary binary && !binary.IsNull) output.Write(binary.Value, 0, binary.Value.Length);
                else break;
            }
            return output.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private static async Task EnsureCompanySchemaAsync(OracleConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            using var cmd = new OracleCommand("ALTER TABLE COMPANY MODIFY COMPANY_NAME VARCHAR2(150)", connection);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch { }

        try
        {
            using var cmd = new OracleCommand("ALTER TABLE COMPANY MODIFY SHORT_NAME VARCHAR2(50)", connection);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch { }

        try
        {
            using var cmd = new OracleCommand("ALTER TABLE COMPANY MODIFY ADDRESS VARCHAR2(250)", connection);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch { }

        try
        {
            using var cmd = new OracleCommand("ALTER TABLE COMPANY MODIFY REMARKS VARCHAR2(250)", connection);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch { }

        try
        {
            using var cmd = new OracleCommand("ALTER TABLE COMPANY MODIFY COMPANY_NAME_BANG NVARCHAR2(200)", connection);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch { }

        try
        {
            using var cmd = new OracleCommand("ALTER TABLE COMPANY MODIFY ADDRESS_BANG NVARCHAR2(250)", connection);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch { }
    }

    public async Task<int> CreateCompanyAsync(string companyName, string companyNameBang, string shortName, string address, string addressBang, string remarks, string logoPath, byte[]? logoBytes = null, CancellationToken cancellationToken = default)
    {
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureCompanySchemaAsync(connection, cancellationToken);

        const string nextIdSql = "SELECT NVL(MAX(COMPANY_ID), 0) + 1 FROM COMPANY";
        int newId;
        await using (var idCommand = new OracleCommand(nextIdSql, connection))
        {
            var scalar = await idCommand.ExecuteScalarAsync(cancellationToken);
            newId = Convert.ToInt32(scalar);
        }

        bool hasShortNameCol = false;
        string? bangNameCol = null;
        string? bangAddrCol = null;
        string? blobColName = null;
        try
        {
            await using var chkCmd = new OracleCommand("SELECT COLUMN_NAME, DATA_TYPE FROM USER_TAB_COLUMNS WHERE TABLE_NAME = 'COMPANY'", connection);
            await using var rdr = await chkCmd.ExecuteReaderAsync(cancellationToken);
            while (await rdr.ReadAsync(cancellationToken))
            {
                var c = rdr.GetString(0);
                var dt = rdr.GetString(1);
                if (c.Equals("SHORT_NAME", StringComparison.OrdinalIgnoreCase)) hasShortNameCol = true;
                if (c.Equals("COMPANY_NAME_BANG", StringComparison.OrdinalIgnoreCase)) bangNameCol = "COMPANY_NAME_BANG";
                else if (c.Equals("BANG_COMPANY_NAME", StringComparison.OrdinalIgnoreCase) && bangNameCol == null) bangNameCol = "BANG_COMPANY_NAME";
                if (c.Equals("ADDRESS_BANG", StringComparison.OrdinalIgnoreCase)) bangAddrCol = "ADDRESS_BANG";
                else if (c.Equals("BANG_COMPANY_ADDRESS", StringComparison.OrdinalIgnoreCase) && bangAddrCol == null) bangAddrCol = "BANG_COMPANY_ADDRESS";
                if (dt.Contains("BLOB", StringComparison.OrdinalIgnoreCase) || dt.Contains("RAW", StringComparison.OrdinalIgnoreCase)) blobColName = c;
            }
        }
        catch { }

        var insertCols = new List<string> { "COMPANY_ID", "COMPANY_NAME", "ADDRESS", "REMARKS" };
        var insertVals = new List<string> { ":companyId", ":companyName", ":address", ":remarks" };

        if (hasShortNameCol)
        {
            insertCols.Add("SHORT_NAME");
            insertVals.Add(":shortName");
        }
        if (!string.IsNullOrEmpty(bangNameCol))
        {
            insertCols.Add(bangNameCol);
            insertVals.Add(":companyNameBang");
        }
        if (!string.IsNullOrEmpty(bangAddrCol))
        {
            insertCols.Add(bangAddrCol);
            insertVals.Add(":addressBang");
        }
        if (!string.IsNullOrEmpty(blobColName))
        {
            insertCols.Add(blobColName);
            insertVals.Add(":logoBlob");
        }
        else
        {
            insertCols.Add("COMPANY_LOGO_PATH");
            insertVals.Add(":logoPath");
        }

        var insertSql = $"INSERT INTO COMPANY ({string.Join(", ", insertCols)}) VALUES ({string.Join(", ", insertVals)})";

        await using var insertCommand = new OracleCommand(insertSql, connection) { BindByName = true };
        insertCommand.Parameters.Add(new OracleParameter("companyId", newId));
        insertCommand.Parameters.Add(new OracleParameter("companyName", companyName.Trim()));
        if (hasShortNameCol)
        {
            insertCommand.Parameters.Add(new OracleParameter("shortName", string.IsNullOrWhiteSpace(shortName) ? DBNull.Value : shortName.Trim()));
        }
        insertCommand.Parameters.Add(new OracleParameter("address", string.IsNullOrWhiteSpace(address) ? DBNull.Value : address.Trim()));
        insertCommand.Parameters.Add(new OracleParameter("remarks", string.IsNullOrWhiteSpace(remarks) ? DBNull.Value : remarks.Trim()));
        if (!string.IsNullOrEmpty(bangNameCol))
        {
            insertCommand.Parameters.Add(new OracleParameter("companyNameBang", OracleDbType.NVarchar2) { Value = string.IsNullOrWhiteSpace(companyNameBang) ? (object)DBNull.Value : companyNameBang.Trim() });
        }
        if (!string.IsNullOrEmpty(bangAddrCol))
        {
            insertCommand.Parameters.Add(new OracleParameter("addressBang", OracleDbType.NVarchar2) { Value = string.IsNullOrWhiteSpace(addressBang) ? (object)DBNull.Value : addressBang.Trim() });
        }
        if (!string.IsNullOrEmpty(blobColName))
        {
            insertCommand.Parameters.Add(new OracleParameter("logoBlob", OracleDbType.Blob) { Value = logoBytes ?? (object)DBNull.Value });
        }
        else
        {
            insertCommand.Parameters.Add(new OracleParameter("logoPath", string.IsNullOrWhiteSpace(logoPath) ? DBNull.Value : logoPath.Trim()));
        }

        await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        return newId;
    }

    public async Task UpdateCompanyAsync(int companyId, string companyName, string companyNameBang, string shortName, string address, string addressBang, string remarks, string logoPath, byte[]? logoBytes = null, CancellationToken cancellationToken = default)
    {
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureCompanySchemaAsync(connection, cancellationToken);

        bool hasShortNameCol = false;
        string? bangNameCol = null;
        string? bangAddrCol = null;
        string? blobColName = null;
        try
        {
            await using var chkCmd = new OracleCommand("SELECT COLUMN_NAME, DATA_TYPE FROM USER_TAB_COLUMNS WHERE TABLE_NAME = 'COMPANY'", connection);
            await using var rdr = await chkCmd.ExecuteReaderAsync(cancellationToken);
            while (await rdr.ReadAsync(cancellationToken))
            {
                var c = rdr.GetString(0);
                var dt = rdr.GetString(1);
                if (c.Equals("SHORT_NAME", StringComparison.OrdinalIgnoreCase)) hasShortNameCol = true;
                if (c.Equals("COMPANY_NAME_BANG", StringComparison.OrdinalIgnoreCase)) bangNameCol = "COMPANY_NAME_BANG";
                else if (c.Equals("BANG_COMPANY_NAME", StringComparison.OrdinalIgnoreCase) && bangNameCol == null) bangNameCol = "BANG_COMPANY_NAME";
                if (c.Equals("ADDRESS_BANG", StringComparison.OrdinalIgnoreCase)) bangAddrCol = "ADDRESS_BANG";
                else if (c.Equals("BANG_COMPANY_ADDRESS", StringComparison.OrdinalIgnoreCase) && bangAddrCol == null) bangAddrCol = "BANG_COMPANY_ADDRESS";
                if (dt.Contains("BLOB", StringComparison.OrdinalIgnoreCase) || dt.Contains("RAW", StringComparison.OrdinalIgnoreCase)) blobColName = c;
            }
        }
        catch { }

        var setParts = new List<string> { "COMPANY_NAME = :companyName", "ADDRESS = :address", "REMARKS = :remarks" };
        if (hasShortNameCol) setParts.Add("SHORT_NAME = :shortName");
        if (!string.IsNullOrEmpty(bangNameCol)) setParts.Add($"{bangNameCol} = :companyNameBang");
        if (!string.IsNullOrEmpty(bangAddrCol)) setParts.Add($"{bangAddrCol} = :addressBang");

        if (!string.IsNullOrEmpty(blobColName))
        {
            setParts.Add($"{blobColName} = :logoBlob");
        }
        else
        {
            setParts.Add("COMPANY_LOGO_PATH = :logoPath");
        }

        var updateSql = $"UPDATE COMPANY SET {string.Join(", ", setParts)} WHERE COMPANY_ID = :companyId";

        await using var updateCommand = new OracleCommand(updateSql, connection) { BindByName = true };
        updateCommand.Parameters.Add(new OracleParameter("companyName", companyName.Trim()));
        if (hasShortNameCol)
        {
            updateCommand.Parameters.Add(new OracleParameter("shortName", string.IsNullOrWhiteSpace(shortName) ? DBNull.Value : shortName.Trim()));
        }
        updateCommand.Parameters.Add(new OracleParameter("address", string.IsNullOrWhiteSpace(address) ? DBNull.Value : address.Trim()));
        updateCommand.Parameters.Add(new OracleParameter("remarks", string.IsNullOrWhiteSpace(remarks) ? DBNull.Value : remarks.Trim()));
        if (!string.IsNullOrEmpty(bangNameCol))
        {
            updateCommand.Parameters.Add(new OracleParameter("companyNameBang", OracleDbType.NVarchar2) { Value = string.IsNullOrWhiteSpace(companyNameBang) ? (object)DBNull.Value : companyNameBang.Trim() });
        }
        if (!string.IsNullOrEmpty(bangAddrCol))
        {
            updateCommand.Parameters.Add(new OracleParameter("addressBang", OracleDbType.NVarchar2) { Value = string.IsNullOrWhiteSpace(addressBang) ? (object)DBNull.Value : addressBang.Trim() });
        }
        if (!string.IsNullOrEmpty(blobColName))
        {
            updateCommand.Parameters.Add(new OracleParameter("logoBlob", OracleDbType.Blob) { Value = (logoBytes is not null && logoBytes.Length > 0) ? logoBytes : (object)DBNull.Value });
        }
        else
        {
            updateCommand.Parameters.Add(new OracleParameter("logoPath", string.IsNullOrWhiteSpace(logoPath) ? DBNull.Value : logoPath.Trim()));
        }
        updateCommand.Parameters.Add(new OracleParameter("companyId", companyId));
        await updateCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteCompanyAsync(int companyId, CancellationToken cancellationToken = default)
    {
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string deleteSql = "DELETE FROM COMPANY WHERE COMPANY_ID = :companyId";
        await using var deleteCommand = new OracleCommand(deleteSql, connection) { BindByName = true };
        deleteCommand.Parameters.Add(new OracleParameter("companyId", companyId));
        await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateUserPrivilegesAsync(int userId, IEnumerable<int> selectedModuleIds, CancellationToken cancellationToken = default)
    {
        var users = await _db.Users.AsNoTracking().Where(user => user.UserId == userId).ToListAsync(cancellationToken);
        var user = users.FirstOrDefault() ?? throw new InvalidOperationException("User not found.");
        var modules = await GetAllMenuControlsAsync(cancellationToken);
        var validIds = modules.Select(module => module.Id).ToHashSet();
        var selectedIds = user.Admin == 1
            ? validIds
            : IncludeParentModules(selectedModuleIds.Where(validIds.Contains), modules);
        var privilegeArray = string.Join(',', selectedIds.OrderBy(id => id));

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new OracleCommand(
            "UPDATE USERS SET PRIVILEGE_ARRAY = :privilegeArray WHERE USER_ID = :userId",
            connection) { BindByName = true };
        command.Parameters.Add(new OracleParameter("privilegeArray", privilegeArray));
        command.Parameters.Add(new OracleParameter("userId", userId));
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("User privileges could not be updated.");
    }

    public async Task<int> CreateUserAsync(
        string username,
        string password,
        bool isAdmin,
        int callingUserId,
        string companyId,
        IEnumerable<int> selectedModuleIds,
        CancellationToken cancellationToken = default)
    {
        var normalizedUsername = username.Trim().ToLowerInvariant();
        var duplicateCount = await _db.Users.CountAsync(
            user => user.UserName != null && user.UserName.ToLower() == normalizedUsername,
            cancellationToken);
        if (duplicateCount > 0) throw new InvalidOperationException("This username already exists.");

        var modules = await GetAllMenuControlsAsync(cancellationToken);
        var validIds = modules.Select(module => module.Id).ToHashSet();
        var privilegeIds = isAdmin
            ? validIds
            : IncludeParentModules(selectedModuleIds.Where(validIds.Contains), modules);

        var existingIds = await _db.Users.AsNoTracking().Select(user => user.UserId).ToListAsync(cancellationToken);
        var newUserId = existingIds.Count == 0 ? 1 : existingIds.Max() + 1;
        var privilegeArray = string.Join(',', privilegeIds.OrderBy(id => id));
        const string insertSql = """
            INSERT INTO USERS
                (USER_ID, USER_NAME, PASSWORD, ADMIN, CALLING_ID, COMPANY_ID, PRIVILEGE_ARRAY)
            VALUES
                (:userId, :username, :password, :admin, :callingId, :companyId, :privilegeArray)
            """;

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new OracleCommand(insertSql, connection) { BindByName = true, Transaction = (OracleTransaction)transaction };
        command.Parameters.Add(new OracleParameter("userId", newUserId));
        command.Parameters.Add(new OracleParameter("username", normalizedUsername));
        command.Parameters.Add(new OracleParameter("password", password));
        command.Parameters.Add(new OracleParameter("admin", isAdmin ? 1 : 0));
        command.Parameters.Add(new OracleParameter("callingId", callingUserId));
        command.Parameters.Add(new OracleParameter("companyId", companyId));
        command.Parameters.Add(new OracleParameter("privilegeArray", privilegeArray));
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("User could not be created.");
        await transaction.CommitAsync(cancellationToken);
        return newUserId;
    }

    public async Task<string> UpdateOwnCredentialsAsync(
        int userId,
        string currentPassword,
        string username,
        string? newPassword,
        CancellationToken cancellationToken = default)
    {
        var normalizedUsername = username.Trim().ToLowerInvariant();
        var users = await _db.Users.AsNoTracking()
            .Where(user => user.UserId == userId)
            .ToListAsync(cancellationToken);
        var user = users.FirstOrDefault() ?? throw new InvalidOperationException("User not found.");
        if (!string.Equals(user.Password, currentPassword, StringComparison.Ordinal))
            throw new InvalidOperationException("Current password is incorrect.");

        var duplicateCount = await _db.Users.CountAsync(
            candidate => candidate.UserId != userId && candidate.UserName != null && candidate.UserName.ToLower() == normalizedUsername,
            cancellationToken);
        if (duplicateCount > 0) throw new InvalidOperationException("This username already exists.");

        var password = string.IsNullOrEmpty(newPassword) ? currentPassword : newPassword;
        const string sql = """
            UPDATE USERS
            SET USER_NAME = :username, PASSWORD = :password
            WHERE USER_ID = :userId AND PASSWORD = :currentPassword
            """;
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new OracleCommand(sql, connection) { BindByName = true };
        command.Parameters.Add(new OracleParameter("username", normalizedUsername));
        command.Parameters.Add(new OracleParameter("password", password));
        command.Parameters.Add(new OracleParameter("userId", userId));
        command.Parameters.Add(new OracleParameter("currentPassword", currentPassword));
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("Credentials could not be updated.");
        return normalizedUsername;
    }

    public async Task<bool> VerifyCurrentUserPasswordAsync(int userId, string password, CancellationToken cancellationToken = default)
    {
        // Oracle does not support EF Core's SQL boolean projection for AnyAsync.
        var users = await _db.Users.AsNoTracking()
            .Where(user => user.UserId == userId)
            .ToListAsync(cancellationToken);
        return string.Equals(users.FirstOrDefault()?.Password, password, StringComparison.Ordinal);
    }

    private static HashSet<int> IncludeParentModules(IEnumerable<int> selectedIds, IReadOnlyCollection<NavigationItem> modules)
    {
        var result = selectedIds.ToHashSet();
        var lookup = modules.ToDictionary(module => module.Id);
        foreach (var selectedId in result.ToArray())
        {
            var currentId = selectedId;
            var visited = new HashSet<int>();
            while (lookup.TryGetValue(currentId, out var module) && module.ParentId is > 0)
            {
                if (!visited.Add(currentId)) break;
                result.Add(module.ParentId.Value);
                currentId = module.ParentId.Value;
            }
        }
        return result;
    }

}
