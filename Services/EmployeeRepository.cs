using System.Text;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using TG.Payroll.Web.Models;

namespace TG.Payroll.Web.Services;

public sealed class EmployeeRepository
{
    private readonly string _connectionString;

    public EmployeeRepository(IConfiguration configuration)
    {
        _connectionString = DatabaseOptions.GetConnectionString(configuration);
    }

    public async Task<List<EmployeeListItem>> GetEmployeesAsync(string? search, CancellationToken cancellationToken = default)
    {
        var filter = string.IsNullOrWhiteSpace(search)
            ? string.Empty
            : " AND (UPPER(E.EMP_CODE) LIKE :search OR UPPER(E.EMP_NAME) LIKE :search)";
        var sql = $"""
            SELECT * FROM (
                SELECT E.EMP_ID, E.EMP_CODE, E.EMP_NAME,
                       NVL((SELECT D.DEPARTMENT_NAME FROM DEPARTMENT D WHERE D.DEPARTMENT_ID = E.DEPARTMENT_ID), '') DEPARTMENT_NAME,
                       NVL((SELECT D.DESIGNATION_NAME FROM DESIGNATION D WHERE D.DESIGNATION_ID = E.DESIGNATION_ID), '') DESIGNATION_NAME,
                       NVL(E.EMP_STATUS, '') EMP_STATUS, E.DATE_OF_JOINING
                FROM EMP_OFFICIAL E
                WHERE 1 = 1 {filter}
                ORDER BY E.EMP_CODE
            ) WHERE ROWNUM <= 100
            """;

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var command = new OracleCommand(sql, connection) { BindByName = true };
        if (!string.IsNullOrWhiteSpace(search))
            command.Parameters.Add(new OracleParameter("search", $"%{search.Trim().ToUpperInvariant()}%"));

        var employees = new List<EmployeeListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            employees.Add(new EmployeeListItem(
                ToInt(reader.GetValue(0)),
                ToText(reader.GetValue(1)),
                ToText(reader.GetValue(2)),
                ToText(reader.GetValue(3)),
                ToText(reader.GetValue(4)),
                ToText(reader.GetValue(5)),
                reader.IsDBNull(6) ? null : Convert.ToDateTime(reader.GetValue(6))));
        }
        return employees;
    }

    public async Task<EmployeeInformation?> GetEmployeeAsync(int employeeId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT O.EMP_ID, O.EMP_CODE, O.EMP_NAME, O.ERP_CODE, O.BANG_EMP_NAME,
                   O.UNIT_ID, O.EMP_CATEGORY_ID, O.DEPARTMENT_ID, O.SECTION_ID, O.LINE_ID,
                   O.DESIGNATION_ID, O.SHIFT_ID, O.RULE_ID, O.FLOOR_ID, O.DATE_OF_JOINING,
                   O.CLOSE_DATE, O.GROSS, O.EMP_STATUS, O.STS_REASONS, O.WEEKEND,
                   O.PROXIMITY_NO, O.LICENSE_NO, O.EMP_GRADE, O.BENEFICIARY_NAME,
                   O.BANG_BENEFICIARY_NAME, O.RELATION_WITH_BENEFICIARY, O.BANK_ACCOUNT_HOLDER,
                   DECODE(O.BANK_ACCOUNT_HOLDER, 'M', O.MOBILE_BANK_ACC_NO, O.ACCOUNT_NO) ACCOUNT_NO,
                   NVL(O.TRANSPORT,'N') TRANSPORT, NVL(O.OVER_TIME,'N') OVER_TIME,
                   NVL(O.LUNCH,'N') LUNCH, NVL(O.TAX_HOLDER,'N') TAX_HOLDER,
                   NVL(O.EL_HOLDER,'N') EL_HOLDER, NVL(O.EL_SEGMENT,'None') EL_SEGMENT,
                   P.FATHER_NAME, P.BANG_FATHER_NAME, P.MOTHER_NAME, P.BANG_MOTHER_NAME,
                   P.HUSBAND_NAME, P.BANG_HUSBAND_NAME, P.DATE_OF_BIRTH,
                   P.SEX, P.RELIGION, P.MARITAL_STATUS, P.BLOOD_GROUP, P.NATIONAL_ID,
                   P.CONTACT_NO, P.E_MAIL, P.EDUCATION, P.EMPLOYEMENT, P.REMARKS,
                   P.PRESENT_VILL, P.BANG_PRESENT_VILL, P.PRESENT_HOUSE, P.BANG_PRESENT_POST,
                   P.PRESENT_PS, P.BANG_PRESENT_PS, P.PRESENT_DIST, P.BANG_PRESENT_DIST,
                   P.PARMANENT_HOUSE, P.BANG_PERMANENT_VILL, P.PARMANENT_VILL, P.BANG_PERMANENT_POST,
                   P.PARMANENT_PS, P.BANG_PERMANENT_PS, P.PARMANENT_DIST, P.BANG_PERMANENT_DIST,
                   NVL(P.CONTRACTUAL,'N') CONTRACTUAL, P.NOMINEE_CELL_NO
            FROM EMP_OFFICIAL O, EMP_PERSONAL P
            WHERE O.EMP_ID = P.EMP_ID(+) AND O.EMP_ID = :employeeId
            """;

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new OracleCommand(sql, connection) { BindByName = true };
        command.Parameters.Add(new OracleParameter("employeeId", employeeId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        var employee = new EmployeeInformation
        {
            EmployeeId = Int(reader, "EMP_ID"),
            EmployeeCode = Text(reader, "EMP_CODE"),
            EmployeeName = Text(reader, "EMP_NAME"),
            ErpCode = Text(reader, "ERP_CODE"),
            BanglaEmployeeName = Text(reader, "BANG_EMP_NAME"),
            UnitId = Int(reader, "UNIT_ID"), CategoryId = Int(reader, "EMP_CATEGORY_ID"),
            DepartmentId = Int(reader, "DEPARTMENT_ID"), SectionId = Int(reader, "SECTION_ID"),
            LineId = Int(reader, "LINE_ID"), DesignationId = Int(reader, "DESIGNATION_ID"),
            ShiftId = Int(reader, "SHIFT_ID"), SalaryRuleId = Int(reader, "RULE_ID"), FloorId = Int(reader, "FLOOR_ID"),
            DateOfJoining = Date(reader, "DATE_OF_JOINING") ?? DateTime.Today,
            CloseDate = Date(reader, "CLOSE_DATE"), DateOfBirth = Date(reader, "DATE_OF_BIRTH"),
            Gross = Decimal(reader, "GROSS"), EmployeeStatus = Text(reader, "EMP_STATUS", "Active"),
            StatusReason = Text(reader, "STS_REASONS"), Weekend = Text(reader, "WEEKEND", "N/A"),
            ProximityNo = Text(reader, "PROXIMITY_NO"), LicenseNo = Text(reader, "LICENSE_NO"),
            EmployeeGrade = Text(reader, "EMP_GRADE"), BeneficiaryName = Text(reader, "BENEFICIARY_NAME"),
            BanglaBeneficiaryName = Text(reader, "BANG_BENEFICIARY_NAME"),
            RelationWithBeneficiary = Text(reader, "RELATION_WITH_BENEFICIARY"),
            BankAccountType = Text(reader, "BANK_ACCOUNT_HOLDER", "N"), AccountNo = Text(reader, "ACCOUNT_NO"),
            Transport = Yes(reader, "TRANSPORT"), OverTime = Yes(reader, "OVER_TIME"),
            QuarterHolder = Yes(reader, "LUNCH"), TaxHolder = Yes(reader, "TAX_HOLDER"),
            EarnLeaveHolder = Yes(reader, "EL_HOLDER"), EarnLeaveSegment = Text(reader, "EL_SEGMENT", "None"),
            FatherName = Text(reader, "FATHER_NAME"), BanglaFatherName = Text(reader, "BANG_FATHER_NAME"),
            MotherName = Text(reader, "MOTHER_NAME"), BanglaMotherName = Text(reader, "BANG_MOTHER_NAME"),
            SpouseName = Text(reader, "HUSBAND_NAME"), BanglaSpouseName = Text(reader, "BANG_HUSBAND_NAME"),
            Gender = Text(reader, "SEX", "MALE"), Religion = Text(reader, "RELIGION", "ISLAM"),
            MaritalStatus = Text(reader, "MARITAL_STATUS", "SINGLE"), BloodGroup = Text(reader, "BLOOD_GROUP"),
            NationalId = Text(reader, "NATIONAL_ID"), ContactNo = Text(reader, "CONTACT_NO"),
            Email = Text(reader, "E_MAIL"), Education = Text(reader, "EDUCATION"),
            EmploymentExperience = Text(reader, "EMPLOYEMENT"), Remarks = Text(reader, "REMARKS"),
            PresentVillage = Text(reader, "PRESENT_VILL"), BanglaPresentVillage = Text(reader, "BANG_PRESENT_VILL"),
            PresentPost = Text(reader, "PRESENT_HOUSE"), BanglaPresentPost = Text(reader, "BANG_PRESENT_POST"),
            PresentPoliceStation = Text(reader, "PRESENT_PS"), BanglaPresentPoliceStation = Text(reader, "BANG_PRESENT_PS"),
            PresentDistrict = Text(reader, "PRESENT_DIST"), BanglaPresentDistrict = Text(reader, "BANG_PRESENT_DIST"),
            PermanentVillage = Text(reader, "PARMANENT_HOUSE"), BanglaPermanentVillage = Text(reader, "BANG_PERMANENT_VILL"),
            PermanentPost = Text(reader, "PARMANENT_VILL"), BanglaPermanentPost = Text(reader, "BANG_PERMANENT_POST"),
            PermanentPoliceStation = Text(reader, "PARMANENT_PS"), BanglaPermanentPoliceStation = Text(reader, "BANG_PERMANENT_PS"),
            PermanentDistrict = Text(reader, "PARMANENT_DIST"), BanglaPermanentDistrict = Text(reader, "BANG_PERMANENT_DIST"),
            Contractual = Yes(reader, "CONTRACTUAL"), NomineeCellNo = Text(reader, "NOMINEE_CELL_NO")
        };
        await reader.DisposeAsync();
        employee.Photo = ReadBlob(connection, "EMP_PERSONAL", "EMP_PHOTO", employeeId);
        employee.Signature = ReadBlob(connection, "EMP_SIGNATURE", "SIGNATURE", employeeId);
        return employee;
    }

    public async Task<EmployeeLookups> GetLookupsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return new EmployeeLookups
        {
            Units = await ReadLookupAsync(connection, "SELECT UNIT_ID, UNIT_NAME FROM UNIT ORDER BY UNIT_NAME", cancellationToken),
            Categories = await ReadLookupAsync(connection, "SELECT EMP_CATEGORY_ID, EMP_CATEGORY_NAME FROM EMP_CATEGORY ORDER BY EMP_CATEGORY_NAME", cancellationToken),
            Departments = await ReadLookupAsync(connection, "SELECT DEPARTMENT_ID, DEPARTMENT_NAME FROM DEPARTMENT ORDER BY DEPARTMENT_NAME", cancellationToken),
            Sections = await ReadLookupAsync(connection, "SELECT SECTION_ID, SECTION_NAME FROM SECTION ORDER BY SECTION_NAME", cancellationToken),
            Lines = await ReadLookupAsync(connection, "SELECT LINE_ID, LINE_NAME FROM LINE ORDER BY LINE_NAME", cancellationToken),
            Designations = await ReadLookupAsync(connection, "SELECT DESIGNATION_ID, DESIGNATION_NAME FROM DESIGNATION ORDER BY DESIGNATION_NAME", cancellationToken),
            Shifts = await ReadLookupAsync(connection, "SELECT SHIFT_ID, SHIFT_NAME FROM SHIFT_INFO ORDER BY SHIFT_NAME", cancellationToken),
            SalaryRules = await ReadLookupAsync(connection, "SELECT RULE_ID, RULE_NAME FROM SALARY_RULE_INFO WHERE RULE_STATUS <> 'NONE' ORDER BY RULE_NAME", cancellationToken),
            Floors = await ReadLookupAsync(connection, "SELECT FLOOR_ID, FLOOR_NAME FROM FLOOR ORDER BY FLOOR_NAME", cancellationToken)
        };
    }

    public async Task<List<SalaryRuleDetail>> GetSalaryRuleDetailsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT RULE_ID, NVL(RULE_BASIC,0) RULE_BASIC, NVL(RULE_HOUSE_RENT,0) RULE_HOUSE_RENT, NVL(RULE_MEDICAL,0) RULE_MEDICAL, NVL(RULE_TRANSPORT,0) RULE_TRANSPORT, NVL(RULE_FOOD,0) RULE_FOOD FROM SALARY_RULE_INFO WHERE RULE_STATUS <> 'NONE'";
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new OracleCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var list = new List<SalaryRuleDetail>();
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new SalaryRuleDetail
            {
                RuleId = Convert.ToInt32(reader["RULE_ID"]),
                RuleBasic = Convert.ToDecimal(reader["RULE_BASIC"]),
                RuleHouseRent = Convert.ToDecimal(reader["RULE_HOUSE_RENT"]),
                RuleMedical = Convert.ToDecimal(reader["RULE_MEDICAL"]),
                RuleTransport = Convert.ToDecimal(reader["RULE_TRANSPORT"]),
                RuleFood = Convert.ToDecimal(reader["RULE_FOOD"])
            });
        }
        return list;
    }

    public async Task<EmployeeLeaveSummary> GetEmployeeLeaveSummaryAsync(int employeeId, string employeeCode, DateTime joinDate, DateTime? closeDate, CancellationToken cancellationToken = default)
    {
        var summary = new EmployeeLeaveSummary();
        if (employeeId <= 0) return summary;

        int cYear = DateTime.Now.Year;
        string closeDateStr = (closeDate ?? DateTime.Today).ToString("dd-MMM-yyyy");
        string sql = $"""
            SELECT 14 - NVL(GRANT_SL, 0) SL,
                   NVL(CASE WHEN DATE_OF_JOINING < TO_DATE('01-Jan-{cYear}','DD-Mon-YYYY') THEN 10
                            ELSE ROUND(10/365*(365 - ROUND(MONTHS_BETWEEN(DATE_OF_JOINING, TO_DATE('01-Jan-{cYear}','DD-Mon-YYYY'))/12*365 + 1))) END, 0) - NVL(GRANT_CL, 0) CL,
                   DECODE(E_O.EL_HOLDER, 'Y', ((CASE WHEN (TRUNC(MONTHS_BETWEEN(SYSDATE, DATE_OF_JOINING)/12)) < 1 THEN 0 ELSE CAST(NVL(PRESENT,0)/18 AS NUMBER(12,4)) END) - NVL(GRANT_EL,0)), 0) EL
            FROM EMP_OFFICIAL E_O,
                 (SELECT DISTINCT A.EMP_ID, COUNT(A.EMP_ID) PRESENT FROM ATTENDANCE_DETAILS A, EMP_OFFICIAL O,
                         (SELECT E.EMP_ID, MAX(E.LAST_COUNTING_DATE+1) LAST_DATE FROM EARN_LEAVE_PROCESS E
                          WHERE E.LAST_COUNTING_DATE < TO_DATE('01-Jan-{cYear}','DD-Mon-YYYY') GROUP BY E.EMP_ID) E
                  WHERE A.ATTD_DATE BETWEEN NVL(E.LAST_DATE, O.DATE_OF_JOINING) AND TO_DATE('{closeDateStr}','DD-Mon-YYYY')
                    AND O.EMP_ID = A.EMP_ID AND O.EMP_ID = E.EMP_ID(+) AND A.STATUS = 'P' GROUP BY A.EMP_ID) P,
                 (SELECT EMP_ID, SUM(NVL(CL,0)) GRANT_CL, SUM(NVL(SL,0)) GRANT_SL, SUM(NVL(EL,0)) GRANT_EL
                  FROM (SELECT EMP_ID, DECODE(TYPE,'CL',SUM(GRANT_DAYS)) CL, DECODE(UPPER(TYPE),'ML',SUM(GRANT_DAYS),'SL',SUM(GRANT_DAYS)) SL, 0 EL
                        FROM LEAVE WHERE FROM_DATE >= TO_DATE('01-Jan-{cYear}','DD-Mon-YYYY') GROUP BY EMP_ID, TYPE
                        UNION ALL
                        SELECT L.EMP_ID, 0 CL, 0 SL, SUM(L.GRANT_DAYS) EL
                        FROM LEAVE L, EMP_OFFICIAL E_O,
                             (SELECT E.EMP_ID, MAX(E.LAST_COUNTING_DATE+1) LAST_DATE FROM EARN_LEAVE_PROCESS E
                              WHERE E.LAST_COUNTING_DATE < TO_DATE('01-Jan-{cYear}','DD-Mon-YYYY') GROUP BY E.EMP_ID) ELP
                        WHERE E_O.EMP_ID = L.EMP_ID AND E_O.EMP_ID = ELP.EMP_ID(+) AND L.TYPE = 'EL'
                          AND L.FROM_DATE BETWEEN NVL(ELP.LAST_DATE, E_O.DATE_OF_JOINING) AND TO_DATE('{closeDateStr}','DD-Mon-YYYY') GROUP BY L.EMP_ID)
                  GROUP BY EMP_ID) LV
            WHERE E_O.EMP_ID = P.EMP_ID(+) AND E_O.EMP_ID = LV.EMP_ID(+) AND E_O.EMP_ID = :empId
            """;

        try
        {
            await using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new OracleCommand(sql, connection) { BindByName = true };
            command.Parameters.Add(new OracleParameter("empId", employeeId));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                summary.SickLeave = Convert.ToDecimal(reader["SL"] is DBNull ? 0 : reader["SL"]);
                summary.CasualLeave = Convert.ToDecimal(reader["CL"] is DBNull ? 0 : reader["CL"]);
                summary.CalculatedEl = Convert.ToDecimal(reader["EL"] is DBNull ? 0 : reader["EL"]);
                summary.HasData = true;
            }
            await reader.DisposeAsync();

            // Fetch Carry EL
            if (!string.IsNullOrWhiteSpace(employeeCode))
            {
                using var carryCmd = new OracleCommand("SELECT NVL(EL_CARRY,0) FROM EL_VAULT WHERE EMP_CODE = :empCode", connection) { BindByName = true };
                carryCmd.Parameters.Add(new OracleParameter("empCode", employeeCode.Trim()));
                var carryVal = await carryCmd.ExecuteScalarAsync(cancellationToken);
                summary.CarryEl = carryVal is null || carryVal is DBNull ? 0 : Convert.ToDecimal(carryVal);
            }

            // Fetch Observed EL
            string obsSql = $"""
                SELECT NVL(SUM(L.GRANT_DAYS),0) FROM LEAVE L, EMP_OFFICIAL E_O,
                       (SELECT E.EMP_ID, MAX(E.LAST_COUNTING_DATE+1) LAST_DATE FROM EARN_LEAVE_PROCESS E
                        WHERE E.LAST_COUNTING_DATE < TO_DATE('01-Jan-{cYear}','DD-Mon-YYYY') GROUP BY E.EMP_ID) ELP
                WHERE E_O.EMP_ID = L.EMP_ID AND E_O.EMP_ID = ELP.EMP_ID(+) AND L.TYPE = 'EL'
                  AND E_O.EMP_ID = :empId AND L.FROM_DATE BETWEEN NVL(ELP.LAST_DATE, E_O.DATE_OF_JOINING) AND TO_DATE('{closeDateStr}','DD-Mon-YYYY')
                """;
            using var obsCmd = new OracleCommand(obsSql, connection) { BindByName = true };
            obsCmd.Parameters.Add(new OracleParameter("empId", employeeId));
            var obsVal = await obsCmd.ExecuteScalarAsync(cancellationToken);
            summary.ObservedEl = obsVal is null || obsVal is DBNull ? 0 : Convert.ToDecimal(obsVal);
            summary.EarnLeave = summary.CalculatedEl - summary.ObservedEl + summary.CarryEl;
        }
        catch
        {
            // Leave calculation fallback gracefully if tables don't exist or have custom permissions
        }

        return summary;
    }

    private static bool _charCertSchemaEnsured;
    private static readonly SemaphoreSlim _schemaLock = new(1, 1);

    public async Task EnsureCharacterCertificateTableExistsAsync(CancellationToken cancellationToken = default)
    {
        if (_charCertSchemaEnsured) return;

        await _schemaLock.WaitAsync(cancellationToken);
        try
        {
            if (_charCertSchemaEnsured) return;

            await using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // 1. Check if table EMP_CHARACTER_CERTIFICATE exists
            const string checkTableSql = "SELECT COUNT(*) FROM USER_TABLES WHERE TABLE_NAME = 'EMP_CHARACTER_CERTIFICATE'";
            await using var checkTableCmd = new OracleCommand(checkTableSql, connection);
            var tableCount = Convert.ToInt32(await checkTableCmd.ExecuteScalarAsync(cancellationToken) ?? 0);

            if (tableCount == 0)
            {
                const string createTableSql = """
                    CREATE TABLE EMP_CHARACTER_CERTIFICATE
                    (
                        CERT_ID       NUMBER NOT NULL,
                        EMP_ID        NUMBER,
                        EMP_CODE      VARCHAR2(30) NOT NULL,
                        RECORD_DATE   DATE,
                        MONTH_YEAR    VARCHAR2(20) NOT NULL,
                        RATING        VARCHAR2(100),
                        REMARKS       VARCHAR2(500),
                        CREATED_DATE  DATE DEFAULT SYSDATE,
                        CONSTRAINT PK_EMP_CHAR_CERT PRIMARY KEY (CERT_ID)
                    )
                    """;

                await using var createCmd = new OracleCommand(createTableSql, connection);
                await createCmd.ExecuteNonQueryAsync(cancellationToken);

                try
                {
                    const string createIndexSql = "CREATE UNIQUE INDEX UI_EMP_CHAR_CERT ON EMP_CHARACTER_CERTIFICATE (EMP_CODE, MONTH_YEAR)";
                    await using var idxCmd = new OracleCommand(createIndexSql, connection);
                    await idxCmd.ExecuteNonQueryAsync(cancellationToken);
                }
                catch { }
            }

            // 2. Check if sequence SEQ_EMP_CHARACTER_CERT exists
            try
            {
                const string checkSeqSql = "SELECT COUNT(*) FROM USER_SEQUENCES WHERE SEQUENCE_NAME = 'SEQ_EMP_CHARACTER_CERT'";
                await using var checkSeqCmd = new OracleCommand(checkSeqSql, connection);
                var seqCount = Convert.ToInt32(await checkSeqCmd.ExecuteScalarAsync(cancellationToken) ?? 0);
                if (seqCount == 0)
                {
                    const string createSeqSql = "CREATE SEQUENCE SEQ_EMP_CHARACTER_CERT START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE";
                    await using var createSeqCmd = new OracleCommand(createSeqSql, connection);
                    await createSeqCmd.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            catch { }

            _charCertSchemaEnsured = true;
        }
        catch
        {
            // Fallback gracefully if database user lacks DDL privileges
        }
        finally
        {
            _schemaLock.Release();
        }
    }

    public static string FromUnistr(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] == '\\' && i + 4 < input.Length)
            {
                var hex = input.Substring(i + 1, 4);
                if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int code))
                {
                    sb.Append((char)code);
                    i += 4;
                    continue;
                }
            }
            sb.Append(input[i]);
        }
        return sb.ToString();
    }

    public async Task<List<CharacterCertificateItem>> GetCharacterCertificatesAsync(string employeeCode, DateTime joinDate, CancellationToken cancellationToken = default)
    {
        var result = new List<CharacterCertificateItem>();
        if (string.IsNullOrWhiteSpace(employeeCode)) return result;

        await EnsureCharacterCertificateTableExistsAsync(cancellationToken);

        var existingMap = new Dictionary<string, (string Rating, string Remarks)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            const string sql = "SELECT MONTH_YEAR, RATING, ASCIISTR(RATING) AS RATING_HEX, REMARKS, ASCIISTR(REMARKS) AS REMARKS_HEX FROM EMP_CHARACTER_CERTIFICATE WHERE UPPER(EMP_CODE) = :empCode";
            await using var command = new OracleCommand(sql, connection) { BindByName = true };
            command.Parameters.Add(new OracleParameter("empCode", employeeCode.Trim().ToUpperInvariant()));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var my = Text(reader, "MONTH_YEAR");
                var ratingRaw = Text(reader, "RATING");
                var ratingHex = Text(reader, "RATING_HEX");
                var rating = !string.IsNullOrWhiteSpace(ratingRaw) && !ratingRaw.Contains('?') ? ratingRaw : FromUnistr(ratingHex);

                var remarksRaw = Text(reader, "REMARKS");
                var remarksHex = Text(reader, "REMARKS_HEX");
                var remarks = !string.IsNullOrWhiteSpace(remarksRaw) && !remarksRaw.Contains('?') ? remarksRaw : FromUnistr(remarksHex);

                if (!string.IsNullOrEmpty(my)) existingMap[my] = (rating, remarks);
            }
        }
        catch
        {
            // Ignore if table query fails
        }

        var current = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var minDate = joinDate == DateTime.MinValue || joinDate > DateTime.Today ? DateTime.Today.AddYears(-1) : new DateTime(joinDate.Year, joinDate.Month, 1);

        while (current >= minDate)
        {
            var myKey = current.ToString("MMM-yyyy");
            var item = new CharacterCertificateItem
            {
                RecordDate = current,
                MonthYear = myKey
            };
            if (existingMap.TryGetValue(myKey, out var val))
            {
                item.Rating = val.Rating;
                item.Remarks = val.Remarks;
            }
            result.Add(item);
            current = current.AddMonths(-1);
        }

        return result;
    }

    public async Task<int> SaveCharacterCertificatesAsync(string employeeCode, int employeeId, List<CharacterCertificateItem> items, int userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(employeeCode) || items.Count == 0) return 0;

        await EnsureCharacterCertificateTableExistsAsync(cancellationToken);

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        int savedCount = 0;
        try
        {
            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.Rating) && string.IsNullOrWhiteSpace(item.Remarks))
                {
                    const string deleteSql = "DELETE FROM EMP_CHARACTER_CERTIFICATE WHERE UPPER(EMP_CODE) = :empCode AND MONTH_YEAR = :monthYear";
                    await using var delCommand = new OracleCommand(deleteSql, connection) { BindByName = true, Transaction = (OracleTransaction)transaction };
                    delCommand.Parameters.Add(new OracleParameter("empCode", employeeCode.Trim().ToUpperInvariant()));
                    delCommand.Parameters.Add(new OracleParameter("monthYear", item.MonthYear));
                    var deletedRows = await delCommand.ExecuteNonQueryAsync(cancellationToken);
                    if (deletedRows > 0) savedCount++;
                    continue;
                }

                const string mergeSql = """
                    MERGE INTO EMP_CHARACTER_CERTIFICATE T
                    USING (SELECT :empCode AS EMP_CODE, :monthYear AS MONTH_YEAR FROM DUAL) S
                    ON (UPPER(T.EMP_CODE) = UPPER(S.EMP_CODE) AND T.MONTH_YEAR = S.MONTH_YEAR)
                    WHEN MATCHED THEN
                        UPDATE SET T.RATING = :rating, T.REMARKS = :remarks, T.CREATED_DATE = SYSDATE
                    WHEN NOT MATCHED THEN
                        INSERT (CERT_ID, EMP_ID, EMP_CODE, RECORD_DATE, MONTH_YEAR, RATING, REMARKS, CREATED_DATE)
                        VALUES (NVL((SELECT MAX(CERT_ID) FROM EMP_CHARACTER_CERTIFICATE), 0) + 1, :empId, :empCode, :recordDate, :monthYear, :rating, :remarks, SYSDATE)
                    """;

                await using var command = new OracleCommand(mergeSql, connection) { BindByName = true, Transaction = (OracleTransaction)transaction };
                command.Parameters.Add(new OracleParameter("empCode", employeeCode.Trim().ToUpperInvariant()));
                command.Parameters.Add(new OracleParameter("monthYear", item.MonthYear));
                var cleanRating = item.Rating?.Trim() ?? string.Empty;
                string? normalizedRating = cleanRating;
                if (!string.IsNullOrWhiteSpace(cleanRating))
                {
                    if (cleanRating.Contains("অসন্তোষ") || cleanRating.Equals("Unsatisfactory", StringComparison.OrdinalIgnoreCase) || cleanRating.Equals("Poor", StringComparison.OrdinalIgnoreCase) || cleanRating.Equals("U", StringComparison.OrdinalIgnoreCase) || cleanRating == "3")
                        normalizedRating = "Unsatisfactory";
                    else if (cleanRating.Contains("মধ্যম") || cleanRating.Equals("Moderate", StringComparison.OrdinalIgnoreCase) || cleanRating.Equals("Medium", StringComparison.OrdinalIgnoreCase) || cleanRating.Equals("Average", StringComparison.OrdinalIgnoreCase) || cleanRating.Equals("M", StringComparison.OrdinalIgnoreCase) || cleanRating == "2")
                        normalizedRating = "Moderate";
                    else if (cleanRating.Contains("সন্তোষ") || cleanRating.Equals("Satisfactory", StringComparison.OrdinalIgnoreCase) || cleanRating.Equals("Good", StringComparison.OrdinalIgnoreCase) || cleanRating.Equals("S", StringComparison.OrdinalIgnoreCase) || cleanRating == "1" || cleanRating.StartsWith("?"))
                        normalizedRating = "Satisfactory";
                }

                command.Parameters.Add(new OracleParameter("rating", OracleDbType.Varchar2) { Value = string.IsNullOrWhiteSpace(normalizedRating) ? (object)DBNull.Value : normalizedRating });
                command.Parameters.Add(new OracleParameter("remarks", OracleDbType.NVarchar2) { Value = string.IsNullOrWhiteSpace(item.Remarks) ? (object)DBNull.Value : item.Remarks.Trim() });
                command.Parameters.Add(new OracleParameter("empId", employeeId));
                command.Parameters.Add(new OracleParameter("recordDate", OracleDbType.Date) { Value = item.RecordDate });

                await command.ExecuteNonQueryAsync(cancellationToken);
                savedCount++;
            }
            await transaction.CommitAsync(cancellationToken);
            return savedCount;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<EmployeeInformation?> GetEmployeeByCodeAsync(string employeeCode, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT EMP_ID FROM EMP_OFFICIAL WHERE UPPER(TRIM(EMP_CODE)) = :employeeCode";
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new OracleCommand(sql, connection) { BindByName = true };
        command.Parameters.Add(new OracleParameter("employeeCode", employeeCode.Trim().ToUpperInvariant()));
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null || result is DBNull ? null : await GetEmployeeAsync(Convert.ToInt32(result), cancellationToken);
    }

    public async Task<string> GetNextEmployeeCodeAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT NVL(MAX(TO_NUMBER(TRIM(EMP_CODE))), 0) + 1 FROM EMP_OFFICIAL WHERE REGEXP_LIKE(TRIM(EMP_CODE), '^[0-9]+$')";
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new OracleCommand(sql, connection);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToDecimal(result).ToString("0", System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<bool> EmployeeCodeExistsAsync(string employeeCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(employeeCode)) return false;

        const string sql = "SELECT COUNT(*) FROM EMP_OFFICIAL WHERE UPPER(TRIM(EMP_CODE)) = :employeeCode";
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new OracleCommand(sql, connection) { BindByName = true };
        command.Parameters.Add(new OracleParameter("employeeCode", employeeCode.Trim()));
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    public async Task<int> SaveEmployeeAsync(EmployeeInformation employee, int userId, bool assignNextEmployeeCode = false, CancellationToken cancellationToken = default)
    {
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var oracleTransaction = (OracleTransaction)transaction;

        try
        {
            if (employee.EmployeeId == 0)
            {
                employee.EmployeeId = await GetNextEmployeeIdAsync(connection, oracleTransaction, cancellationToken);
                if (assignNextEmployeeCode)
                {
                    await InsertEmployeeWithNextAvailableCodeAsync(connection, oracleTransaction, employee, userId, cancellationToken);
                }
                else
                {
                    await EnsureEmployeeCodeIsUniqueAsync(connection, oracleTransaction, employee.EmployeeCode, null, cancellationToken);
                    await InsertOfficialAsync(connection, oracleTransaction, employee, userId, cancellationToken);
                }
                await InsertPersonalAsync(connection, oracleTransaction, employee, cancellationToken);
                await LogActionAsync(connection, oracleTransaction, employee.EmployeeCode, $"INSERT: Name={employee.EmployeeName}, Status={employee.EmployeeStatus}, Gross={employee.Gross}", userId, cancellationToken);
            }
            else
            {
                await EnsureEmployeeCodeIsUniqueAsync(connection, oracleTransaction, employee.EmployeeCode, employee.EmployeeId, cancellationToken);
                await UpdateOfficialAsync(connection, oracleTransaction, employee, userId, cancellationToken);
                var updated = await UpdatePersonalAsync(connection, oracleTransaction, employee, cancellationToken);
                if (updated == 0) await InsertPersonalAsync(connection, oracleTransaction, employee, cancellationToken);
                await LogActionAsync(connection, oracleTransaction, employee.EmployeeCode, $"UPDATE: Name={employee.EmployeeName}, Status={employee.EmployeeStatus}, Gross={employee.Gross}", userId, cancellationToken);
            }

            if (!employee.EmployeeStatus.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                await UpdateCloseDateAsync(connection, oracleTransaction, employee.EmployeeId, employee.CloseDate ?? DateTime.Today, cancellationToken);
                if (employee.EmployeeStatus.Equals("Maternity", StringComparison.OrdinalIgnoreCase))
                {
                    await InsertMaternityLeaveAttendanceAsync(connection, oracleTransaction, employee.EmployeeId, employee.ShiftId, employee.CloseDate ?? DateTime.Today, userId, cancellationToken);
                }
            }

            if (employee.Signature is not null)
                await SaveSignatureAsync(connection, oracleTransaction, employee.EmployeeId, employee.Signature, userId, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return employee.EmployeeId;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task InsertOfficialAsync(OracleConnection connection, OracleTransaction transaction, EmployeeInformation e, int userId, CancellationToken token)
    {
        const string sql = """
            INSERT INTO EMP_OFFICIAL
            (EMP_ID, EMP_CODE, EMP_NAME, ERP_CODE, UNIT_ID, EMP_CATEGORY_ID, DEPARTMENT_ID, SECTION_ID, LINE_ID,
             DESIGNATION_ID, SHIFT_ID, DATE_OF_JOINING, PROXIMITY_NO, TRANSPORT, LICENSE_NO, EMP_STATUS, CLOSE_DATE,
             RESIGN_GIVEN, STS_REASONS, EMP_STATUS_CHANGE_DATE, GROSS, WEEKEND, OVER_TIME, LUNCH, USER_ID,
             ACCOUNT_NO, MOBILE_BANK_ACC_NO, BANK_ACCOUNT_HOLDER, TAX_HOLDER, RULE_ID, EMP_GRADE, BENEFICIARY_NAME,
             RELATION_WITH_BENEFICIARY, EL_HOLDER, EL_SEGMENT, STAND_ID, FLOOR_ID, BANG_EMP_NAME, BANG_BENEFICIARY_NAME)
            VALUES
            (:id, :code, :name, :erp, :unit, :category, :department, :section, :line, :designation, :shift,
             :joining, :proximity, :transport, :license, :status, :closeDate, 'N', :reason, SYSDATE, :gross,
             :weekend, :overtime, :lunch, :userId, :bankAccount, :mobileAccount, :bankType, :tax, :ruleId,
             :grade, :beneficiary, :relation, :elHolder, :elSegment, 0, :floorId, :banglaName, :banglaBeneficiary)
            """;
        await using var command = CreateOfficialCommand(sql, connection, transaction, e, userId);
        if (await command.ExecuteNonQueryAsync(token) != 1) throw new InvalidOperationException("Employee official information could not be saved.");
    }

    private static async Task UpdateOfficialAsync(OracleConnection connection, OracleTransaction transaction, EmployeeInformation e, int userId, CancellationToken token)
    {
        const string sql = """
            UPDATE EMP_OFFICIAL SET EMP_CODE=:code, EMP_NAME=:name, ERP_CODE=:erp, UNIT_ID=:unit,
            EMP_CATEGORY_ID=:category, DEPARTMENT_ID=:department, SECTION_ID=:section, LINE_ID=:line,
            DESIGNATION_ID=:designation, SHIFT_ID=:shift, DATE_OF_JOINING=:joining, PROXIMITY_NO=:proximity,
            TRANSPORT=:transport, LICENSE_NO=:license, EMP_STATUS=:status, CLOSE_DATE=:closeDate,
            RESIGN_GIVEN=:resignGiven, STS_REASONS=:reason, EMP_STATUS_CHANGE_DATE=SYSDATE, GROSS=:gross,
            WEEKEND=:weekend, OVER_TIME=:overtime, LUNCH=:lunch, USER_ID=:userId, ACCOUNT_NO=:bankAccount,
            MOBILE_BANK_ACC_NO=:mobileAccount, BANK_ACCOUNT_HOLDER=:bankType, TAX_HOLDER=:tax, RULE_ID=:ruleId,
            EMP_GRADE=:grade, BENEFICIARY_NAME=:beneficiary, RELATION_WITH_BENEFICIARY=:relation,
            EL_HOLDER=:elHolder, EL_SEGMENT=:elSegment, FLOOR_ID=:floorId, BANG_EMP_NAME=:banglaName,
            BANG_BENEFICIARY_NAME=:banglaBeneficiary
            WHERE EMP_ID=:id
            """;
        await using var command = CreateOfficialCommand(sql, connection, transaction, e, userId);
        Add(command, "resignGiven", e.EmployeeStatus.Equals("Active", StringComparison.OrdinalIgnoreCase) ? "N" : "Y");
        if (await command.ExecuteNonQueryAsync(token) != 1) throw new InvalidOperationException("Employee was not found or could not be updated.");
    }

    private static OracleCommand CreateOfficialCommand(string sql, OracleConnection connection, OracleTransaction transaction, EmployeeInformation e, int userId)
    {
        var command = new OracleCommand(sql, connection) { BindByName = true, Transaction = transaction };
        Add(command, "id", e.EmployeeId); Add(command, "code", e.EmployeeCode.Trim()); Add(command, "name", e.EmployeeName.Trim());
        Add(command, "erp", e.ErpCode.Trim()); Add(command, "unit", e.UnitId); Add(command, "category", e.CategoryId);
        Add(command, "department", e.DepartmentId); Add(command, "section", e.SectionId); Add(command, "line", e.LineId);
        Add(command, "designation", e.DesignationId); Add(command, "shift", e.ShiftId); AddDate(command, "joining", e.DateOfJoining);
        Add(command, "proximity", e.ProximityNo.Trim()); Add(command, "transport", Yn(e.Transport)); Add(command, "license", e.LicenseNo.Trim());
        Add(command, "status", e.EmployeeStatus); AddDate(command, "closeDate", e.EmployeeStatus.Equals("Active", StringComparison.OrdinalIgnoreCase) ? null : e.CloseDate);
        Add(command, "reason", e.StatusReason.Trim()); Add(command, "gross", e.Gross); Add(command, "weekend", e.Weekend);
        Add(command, "overtime", Yn(e.OverTime)); Add(command, "lunch", Yn(e.QuarterHolder)); Add(command, "userId", userId);
        Add(command, "bankAccount", e.BankAccountType == "M" ? string.Empty : e.AccountNo.Trim());
        Add(command, "mobileAccount", e.BankAccountType == "M" ? e.AccountNo.Trim() : string.Empty);
        Add(command, "bankType", e.BankAccountType); Add(command, "tax", Yn(e.TaxHolder)); Add(command, "ruleId", e.SalaryRuleId);
        Add(command, "grade", e.EmployeeGrade.Trim()); Add(command, "beneficiary", e.BeneficiaryName.Trim());
        Add(command, "banglaBeneficiary", e.BanglaBeneficiaryName.Trim());
        Add(command, "relation", e.RelationWithBeneficiary.Trim()); Add(command, "elHolder", Yn(e.EarnLeaveHolder));
        Add(command, "elSegment", e.EarnLeaveSegment); Add(command, "floorId", e.FloorId); Add(command, "banglaName", e.BanglaEmployeeName.Trim());
        return command;
    }

    private static async Task InsertPersonalAsync(OracleConnection connection, OracleTransaction transaction, EmployeeInformation e, CancellationToken token)
    {
        const string sql = """
            INSERT INTO EMP_PERSONAL
            (EMP_ID, FATHER_NAME, BANG_FATHER_NAME, MOTHER_NAME, BANG_MOTHER_NAME, HUSBAND_NAME, BANG_HUSBAND_NAME,
             DATE_OF_BIRTH, NOMINEE_CELL_NO, SEX, CONTRACTUAL, CONTACT_NO, MARITAL_STATUS, RELIGION, NATIONAL_ID,
             BLOOD_GROUP, E_MAIL, EMPLOYEMENT, REMARKS, PRESENT_HOUSE, BANG_PRESENT_POST, PRESENT_VILL, BANG_PRESENT_VILL,
             PRESENT_PS, BANG_PRESENT_PS, PRESENT_DIST, BANG_PRESENT_DIST, PARMANENT_HOUSE, BANG_PERMANENT_VILL,
             PARMANENT_VILL, BANG_PERMANENT_POST, PARMANENT_PS, BANG_PERMANENT_PS, PARMANENT_DIST, BANG_PERMANENT_DIST,
             EDUCATION, EMP_PHOTO)
            VALUES
            (:id, :father, :bangFather, :mother, :bangMother, :spouse, :bangSpouse,
             :birthDate, :nomineeCell, :gender, :contractual, :contact, :marital, :religion, :nationalId,
             :blood, :email, :experience, :remarks, :presentPost, :bangPresentPost, :presentVillage, :bangPresentVill,
             :presentPs, :bangPresentPs, :presentDistrict, :bangPresentDist, :permanentVillage, :bangPermanentVill,
             :permanentPost, :bangPermanentPost, :permanentPs, :bangPermanentPs, :permanentDistrict, :bangPermanentDist,
             :education, :photo)
            """;
        await using var command = CreatePersonalCommand(sql, connection, transaction, e);
        if (await command.ExecuteNonQueryAsync(token) != 1) throw new InvalidOperationException("Employee personal information could not be saved.");
    }

    private static async Task<int> UpdatePersonalAsync(OracleConnection connection, OracleTransaction transaction, EmployeeInformation e, CancellationToken token)
    {
        const string sql = """
            UPDATE EMP_PERSONAL SET FATHER_NAME=:father, BANG_FATHER_NAME=:bangFather, MOTHER_NAME=:mother,
            BANG_MOTHER_NAME=:bangMother, HUSBAND_NAME=:spouse, BANG_HUSBAND_NAME=:bangSpouse,
            DATE_OF_BIRTH=:birthDate, NOMINEE_CELL_NO=:nomineeCell, SEX=:gender, CONTRACTUAL=:contractual,
            CONTACT_NO=:contact, MARITAL_STATUS=:marital, RELIGION=:religion, NATIONAL_ID=:nationalId,
            BLOOD_GROUP=:blood, E_MAIL=:email, EMPLOYEMENT=:experience, REMARKS=:remarks,
            PRESENT_HOUSE=:presentPost, BANG_PRESENT_POST=:bangPresentPost, PRESENT_VILL=:presentVillage,
            BANG_PRESENT_VILL=:bangPresentVill, PRESENT_PS=:presentPs, BANG_PRESENT_PS=:bangPresentPs,
            PRESENT_DIST=:presentDistrict, BANG_PRESENT_DIST=:bangPresentDist, PARMANENT_HOUSE=:permanentVillage,
            BANG_PERMANENT_VILL=:bangPermanentVill, PARMANENT_VILL=:permanentPost, BANG_PERMANENT_POST=:bangPermanentPost,
            PARMANENT_PS=:permanentPs, BANG_PERMANENT_PS=:bangPermanentPs, PARMANENT_DIST=:permanentDistrict,
            BANG_PERMANENT_DIST=:bangPermanentDist, EDUCATION=:education,
            EMP_PHOTO=NVL(:photo, EMP_PHOTO)
            WHERE EMP_ID=:id
            """;
        await using var command = CreatePersonalCommand(sql, connection, transaction, e);
        return await command.ExecuteNonQueryAsync(token);
    }

    private static OracleCommand CreatePersonalCommand(string sql, OracleConnection connection, OracleTransaction transaction, EmployeeInformation e)
    {
        var command = new OracleCommand(sql, connection) { BindByName = true, Transaction = transaction };
        Add(command, "id", e.EmployeeId); Add(command, "father", e.FatherName.Trim()); Add(command, "bangFather", e.BanglaFatherName.Trim());
        Add(command, "mother", e.MotherName.Trim()); Add(command, "bangMother", e.BanglaMotherName.Trim());
        Add(command, "spouse", e.SpouseName.Trim()); Add(command, "bangSpouse", e.BanglaSpouseName.Trim());
        AddDate(command, "birthDate", e.DateOfBirth); Add(command, "nomineeCell", e.NomineeCellNo.Trim());
        Add(command, "gender", e.Gender); Add(command, "contractual", Yn(e.Contractual)); Add(command, "contact", e.ContactNo.Trim());
        Add(command, "marital", e.MaritalStatus); Add(command, "religion", e.Religion); Add(command, "nationalId", e.NationalId.Trim());
        Add(command, "blood", e.BloodGroup); Add(command, "email", e.Email.Trim()); Add(command, "experience", e.EmploymentExperience.Trim());
        Add(command, "remarks", e.Remarks.Trim()); Add(command, "presentPost", e.PresentPost.Trim());
        Add(command, "bangPresentPost", e.BanglaPresentPost.Trim());
        Add(command, "presentVillage", e.PresentVillage.Trim()); Add(command, "bangPresentVill", e.BanglaPresentVillage.Trim());
        Add(command, "presentPs", e.PresentPoliceStation.Trim()); Add(command, "bangPresentPs", e.BanglaPresentPoliceStation.Trim());
        Add(command, "presentDistrict", e.PresentDistrict.Trim()); Add(command, "bangPresentDist", e.BanglaPresentDistrict.Trim());
        Add(command, "permanentVillage", e.PermanentVillage.Trim()); Add(command, "bangPermanentVill", e.BanglaPermanentVillage.Trim());
        Add(command, "permanentPost", e.PermanentPost.Trim()); Add(command, "bangPermanentPost", e.BanglaPermanentPost.Trim());
        Add(command, "permanentPs", e.PermanentPoliceStation.Trim()); Add(command, "bangPermanentPs", e.BanglaPermanentPoliceStation.Trim());
        Add(command, "permanentDistrict", e.PermanentDistrict.Trim()); Add(command, "bangPermanentDist", e.BanglaPermanentDistrict.Trim());
        Add(command, "education", e.Education.Trim());
        AddBlob(command, "photo", e.Photo);
        return command;
    }

    private static async Task UpdateCloseDateAsync(OracleConnection connection, OracleTransaction transaction, int employeeId, DateTime closeDate, CancellationToken token)
    {
        try
        {
            const string sql = "UPDATE EMP_OFFICIAL SET LAST_CLOSE_DATE = :closeDate WHERE EMP_ID = :id";
            await using var command = new OracleCommand(sql, connection) { BindByName = true, Transaction = transaction };
            AddDate(command, "closeDate", closeDate);
            Add(command, "id", employeeId);
            await command.ExecuteNonQueryAsync(token);
        }
        catch { }
    }

    private static async Task InsertMaternityLeaveAttendanceAsync(OracleConnection connection, OracleTransaction transaction, int employeeId, int shiftId, DateTime startDate, int userId, CancellationToken token)
    {
        try
        {
            var endDate = startDate.AddDays(111).Date;
            const string delSql = "DELETE FROM ATTENDANCE_DETAILS WHERE EMP_ID = :empId AND ATTD_DATE BETWEEN :startDate AND :endDate";
            await using var delCmd = new OracleCommand(delSql, connection) { BindByName = true, Transaction = transaction };
            Add(delCmd, "empId", employeeId);
            AddDate(delCmd, "startDate", startDate);
            AddDate(delCmd, "endDate", endDate);
            await delCmd.ExecuteNonQueryAsync(token);

            for (int i = 0; i < 112; i++)
            {
                var attdDate = startDate.AddDays(i).Date;
                const string insSql = """
                    INSERT INTO ATTENDANCE_DETAILS
                    (EMP_ID, ATTD_DATE, ATTD_REMARKS, STATUS, STATUS2, OVER_TIME, NIGHT_STATUS, ATTD_LOCKED, SHIFT_ID, USER_ID)
                    VALUES (:empId, :attdDate, 'Maternity Leave', 'L', 'L', '0', 'N', 'Y', :shiftId, :userId)
                    """;
                await using var insCmd = new OracleCommand(insSql, connection) { BindByName = true, Transaction = transaction };
                Add(insCmd, "empId", employeeId);
                AddDate(insCmd, "attdDate", attdDate);
                Add(insCmd, "shiftId", shiftId);
                Add(insCmd, "userId", userId.ToString());
                await insCmd.ExecuteNonQueryAsync(token);
            }
        }
        catch { }
    }

    private static async Task LogActionAsync(OracleConnection connection, OracleTransaction transaction, string empCode, string description, int userId, CancellationToken token)
    {
        try
        {
            const string sql = "INSERT INTO LOG_TAB (USER_ID, WORKING_PLACE, DESCRIPTION, DT, EMP_CODE) VALUES (:userId, 'EMPLOYEE_ENTRY', :description, SYSDATE, :empCode)";
            await using var command = new OracleCommand(sql, connection) { BindByName = true, Transaction = transaction };
            Add(command, "userId", userId.ToString());
            Add(command, "description", description);
            Add(command, "empCode", empCode.Trim());
            await command.ExecuteNonQueryAsync(token);
        }
        catch { }
    }

    private static async Task SaveSignatureAsync(OracleConnection connection, OracleTransaction transaction, int employeeId, byte[] signature, int userId, CancellationToken token)
    {
        await using var update = new OracleCommand("UPDATE EMP_SIGNATURE SET SIGNATURE=:signature, UPDATE_BY=:userId, UPDATE_DATE=SYSDATE WHERE EMP_ID=:employeeId", connection) { BindByName = true, Transaction = transaction };
        AddBlob(update, "signature", signature); Add(update, "userId", userId.ToString()); Add(update, "employeeId", employeeId);
        if (await update.ExecuteNonQueryAsync(token) > 0) return;

        const string sql = "INSERT INTO EMP_SIGNATURE (SIGN_ID, EMP_ID, SIGNATURE, CREATE_BY, CREATE_DATE) VALUES ((SELECT NVL(MAX(SIGN_ID),0)+1 FROM EMP_SIGNATURE), :employeeId, :signature, :userId, SYSDATE)";
        await using var insert = new OracleCommand(sql, connection) { BindByName = true, Transaction = transaction };
        Add(insert, "employeeId", employeeId); AddBlob(insert, "signature", signature); Add(insert, "userId", userId.ToString());
        await insert.ExecuteNonQueryAsync(token);
    }

    private static async Task EnsureEmployeeCodeIsUniqueAsync(OracleConnection connection, OracleTransaction transaction, string code, int? employeeId, CancellationToken token)
    {
        var sql = "SELECT COUNT(*) FROM EMP_OFFICIAL WHERE UPPER(TRIM(EMP_CODE))=UPPER(TRIM(:code))" + (employeeId.HasValue ? " AND EMP_ID<>:id" : string.Empty);
        await using var command = new OracleCommand(sql, connection) { BindByName = true, Transaction = transaction };
        Add(command, "code", code.Trim());
        if (employeeId.HasValue) Add(command, "id", employeeId.Value);
        if (Convert.ToInt32(await command.ExecuteScalarAsync(token)) > 0)
            throw new InvalidOperationException($"Employee code {code.Trim()} already exists.");
    }

    private static async Task<int> GetNextEmployeeIdAsync(OracleConnection connection, OracleTransaction transaction, CancellationToken token)
    {
        await using var command = new OracleCommand("SELECT EMP_OFFICIAL_ID_SEQ.NEXTVAL FROM DUAL", connection) { Transaction = transaction };
        return Convert.ToInt32(await command.ExecuteScalarAsync(token));
    }

    private static async Task InsertEmployeeWithNextAvailableCodeAsync(OracleConnection connection, OracleTransaction transaction, EmployeeInformation employee, int userId, CancellationToken token)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            await using var codeCommand = new OracleCommand("SELECT EMP_CODE_SEQ.NEXTVAL FROM DUAL", connection) { Transaction = transaction };
            employee.EmployeeCode = Convert.ToDecimal(await codeCommand.ExecuteScalarAsync(token)).ToString("0", System.Globalization.CultureInfo.InvariantCulture);
            try
            {
                await InsertOfficialAsync(connection, transaction, employee, userId, token);
                return;
            }
            catch (OracleException ex) when (ex.Number == 1)
            {
                // A manually selected code used this sequence value first; safely try the next value.
            }
        }

        throw new InvalidOperationException("Unable to allocate a unique employee code. Please try saving again.");
    }

    private static async Task<List<LookupOption>> ReadLookupAsync(OracleConnection connection, string sql, CancellationToken token)
    {
        await using var command = new OracleCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(token);
        var values = new List<LookupOption>();
        while (await reader.ReadAsync(token)) values.Add(new LookupOption(ToInt(reader.GetValue(0)), ToText(reader.GetValue(1))));
        return values;
    }

    private static void Add(OracleCommand command, string name, object? value) => command.Parameters.Add(new OracleParameter(name, value ?? DBNull.Value));
    private static void AddBlob(OracleCommand command, string name, byte[]? value) => command.Parameters.Add(new OracleParameter(name, OracleDbType.Blob) { Value = value ?? (object)DBNull.Value });
    private static void AddDate(OracleCommand command, string name, DateTime? value) => command.Parameters.Add(new OracleParameter(name, OracleDbType.Date) { Value = value ?? (object)DBNull.Value });
    private static string Yn(bool value) => value ? "Y" : "N";
    private static int ToInt(object value) => value is DBNull ? 0 : Convert.ToInt32(value);
    private static string ToText(object value) => value is DBNull ? string.Empty : Convert.ToString(value) ?? string.Empty;
    private static int Int(OracleDataReader reader, string name)
    {
        try
        {
            var ord = reader.GetOrdinal(name);
            return reader.IsDBNull(ord) ? 0 : Convert.ToInt32(reader[ord]);
        }
        catch { return 0; }
    }

    private static decimal Decimal(OracleDataReader reader, string name)
    {
        try
        {
            var ord = reader.GetOrdinal(name);
            return reader.IsDBNull(ord) ? 0 : Convert.ToDecimal(reader[ord]);
        }
        catch { return 0; }
    }

    private static string Text(OracleDataReader reader, string name, string fallback = "")
    {
        try
        {
            var ord = reader.GetOrdinal(name);
            return reader.IsDBNull(ord) ? fallback : Convert.ToString(reader[ord]) ?? fallback;
        }
        catch { return fallback; }
    }

    private static DateTime? Date(OracleDataReader reader, string name)
    {
        try
        {
            var ord = reader.GetOrdinal(name);
            return reader.IsDBNull(ord) ? null : Convert.ToDateTime(reader[ord]);
        }
        catch { return null; }
    }

    private static bool Yes(OracleDataReader reader, string name) => Text(reader, name).Equals("Y", StringComparison.OrdinalIgnoreCase);

    private static byte[]? ReadBlob(OracleConnection connection, string table, string column, int employeeId)
    {
        try
        {
            using var lengthCommand = new OracleCommand($"SELECT NVL(DBMS_LOB.GETLENGTH({column}), 0) FROM {table} WHERE EMP_ID = :employeeId", connection) { BindByName = true };
            lengthCommand.Parameters.Add(new OracleParameter("employeeId", OracleDbType.Decimal) { Value = employeeId });
            var totalLength = Convert.ToInt32(lengthCommand.ExecuteScalar() ?? 0);
            if (totalLength <= 0) return null;

            const int chunkSize = 2000;
            using var output = new MemoryStream(totalLength);
            for (var offset = 1; offset <= totalLength; offset += chunkSize)
            {
                using var chunkCommand = new OracleCommand($"SELECT DBMS_LOB.SUBSTR({column}, :amount, :offset) FROM {table} WHERE EMP_ID = :employeeId", connection) { BindByName = true };
                chunkCommand.Parameters.Add(new OracleParameter("amount", OracleDbType.Int32) { Value = chunkSize });
                chunkCommand.Parameters.Add(new OracleParameter("offset", OracleDbType.Int32) { Value = offset });
                chunkCommand.Parameters.Add(new OracleParameter("employeeId", OracleDbType.Decimal) { Value = employeeId });
                var value = chunkCommand.ExecuteScalar();
                if (value is byte[] bytes) output.Write(bytes, 0, bytes.Length);
                else if (value is OracleBinary binary && !binary.IsNull) output.Write(binary.Value, 0, binary.Value.Length);
                else break;
            }
            return output.Length == 0 ? null : output.ToArray();
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<EmployeeStatusItem>> GetEmployeeStatusListAsync(EmployeeStatusFilter filter, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder(@"
            SELECT A.EMP_ID, A.EMP_CODE, A.EMP_NAME, 
                   NVL(C.DESIGNATION_NAME, '') DESIGNATION_NAME, 
                   NVL(G.DEPARTMENT_NAME, '') DEPARTMENT_NAME,
                   NVL(E.SECTION_NAME, '') SECTION_NAME,
                   NVL(F.LINE_NAME, '') LINE_NAME,
                   NVL(D.EMP_CATEGORY_NAME, '') EMP_CATEGORY_NAME,
                   A.DATE_OF_JOINING, 
                   NVL(A.GROSS, 0) GROSS, 
                   NVL(EXTRACT(YEAR FROM sysdate) - EXTRACT(YEAR FROM B.DATE_OF_BIRTH), 0) AGE, 
                   NVL(A.OVER_TIME, 'N') OVER_TIME, 
                   NVL(A.TRANSPORT, 'N') TRANSPORT, 
                   NVL(A.TRANSPORT_STAND, '') TRANSPORT_STAND
            FROM EMP_OFFICIAL A, EMP_PERSONAL B, DESIGNATION C, EMP_CATEGORY D, SECTION E, LINE F, DEPARTMENT G
            WHERE A.EMP_STATUS = 'Active' 
              AND A.EMP_ID = B.EMP_ID(+) 
              AND A.DESIGNATION_ID = C.DESIGNATION_ID(+)
              AND A.EMP_CATEGORY_ID = D.EMP_CATEGORY_ID(+) 
              AND A.SECTION_ID = E.SECTION_ID(+)
              AND A.DEPARTMENT_ID = G.DEPARTMENT_ID(+) 
              AND A.LINE_ID = F.LINE_ID(+)");

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var command = new OracleCommand { Connection = connection, BindByName = true };

        if (filter.UnitId.HasValue && filter.UnitId.Value > 0)
        {
            sb.Append(" AND A.UNIT_ID = :unitId");
            command.Parameters.Add(new OracleParameter("unitId", filter.UnitId.Value));
        }

        if (filter.CategoryId.HasValue && filter.CategoryId.Value > 0)
        {
            sb.Append(" AND A.EMP_CATEGORY_ID = :catId");
            command.Parameters.Add(new OracleParameter("catId", filter.CategoryId.Value));
        }

        if (filter.DepartmentId.HasValue && filter.DepartmentId.Value > 0)
        {
            sb.Append(" AND A.DEPARTMENT_ID = :deptId");
            command.Parameters.Add(new OracleParameter("deptId", filter.DepartmentId.Value));
        }

        if (filter.SectionId.HasValue && filter.SectionId.Value > 0)
        {
            sb.Append(" AND A.SECTION_ID = :secId");
            command.Parameters.Add(new OracleParameter("secId", filter.SectionId.Value));
        }

        if (filter.LineId.HasValue && filter.LineId.Value > 0)
        {
            sb.Append(" AND A.LINE_ID = :lineId");
            command.Parameters.Add(new OracleParameter("lineId", filter.LineId.Value));
        }

        if (filter.DesignationId.HasValue && filter.DesignationId.Value > 0)
        {
            sb.Append(" AND A.DESIGNATION_ID = :desigId");
            command.Parameters.Add(new OracleParameter("desigId", filter.DesignationId.Value));
        }

        if (filter.FromDate.HasValue)
        {
            sb.Append(" AND A.DATE_OF_JOINING >= :fromDate");
            command.Parameters.Add(new OracleParameter("fromDate", filter.FromDate.Value.Date));
        }

        if (filter.ToDate.HasValue)
        {
            sb.Append(" AND A.DATE_OF_JOINING <= :toDate");
            command.Parameters.Add(new OracleParameter("toDate", filter.ToDate.Value.Date.AddDays(1).AddSeconds(-1)));
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchQuery))
        {
            sb.Append(" AND (UPPER(A.EMP_CODE) LIKE :search OR UPPER(A.EMP_NAME) LIKE :search)");
            command.Parameters.Add(new OracleParameter("search", $"%{filter.SearchQuery.Trim().ToUpperInvariant()}%"));
        }

        sb.Append(" ORDER BY A.EMP_CODE");
        command.CommandText = sb.ToString();

        var list = new List<EmployeeStatusItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var gross = reader.IsDBNull(9) ? 0 : Convert.ToDecimal(reader.GetValue(9));
            var ot = Text(reader, "OVER_TIME");
            var tr = Text(reader, "TRANSPORT");
            var doj = Date(reader, "DATE_OF_JOINING");
            var stand = Text(reader, "TRANSPORT_STAND");

            list.Add(new EmployeeStatusItem
            {
                EmployeeId = Int(reader, "EMP_ID"),
                EmployeeCode = Text(reader, "EMP_CODE"),
                EmployeeName = Text(reader, "EMP_NAME"),
                DesignationName = Text(reader, "DESIGNATION_NAME"),
                DepartmentName = Text(reader, "DEPARTMENT_NAME"),
                SectionName = Text(reader, "SECTION_NAME"),
                LineName = Text(reader, "LINE_NAME"),
                CategoryName = Text(reader, "EMP_CATEGORY_NAME"),
                DateOfJoining = doj,
                Gross = gross,
                Age = reader.IsDBNull(10) ? 0 : Convert.ToInt32(reader.GetValue(10)),
                OverTime = string.IsNullOrEmpty(ot) ? "N" : ot,
                Transport = string.IsNullOrEmpty(tr) ? "N" : tr,
                TransportStand = stand,
                OriginalGross = gross,
                OriginalOverTime = string.IsNullOrEmpty(ot) ? "N" : ot,
                OriginalTransport = string.IsNullOrEmpty(tr) ? "N" : tr,
                OriginalDateOfJoining = doj,
                OriginalTransportStand = stand,
                IsEdited = false
            });
        }
        return list;
    }

    public async Task<int> BulkUpdateEmployeeStatusAsync(IEnumerable<EmployeeStatusItem> items, int userId, CancellationToken cancellationToken = default)
    {
        var editedItems = items.Where(i => i.IsEdited).ToList();
        if (editedItems.Count == 0) return 0;

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var oracleTransaction = (OracleTransaction)transaction;

        try
        {
            var updatedCount = 0;
            foreach (var item in editedItems)
            {
                const string sql = @"
                    UPDATE EMP_OFFICIAL 
                    SET GROSS = :gross, 
                        OVER_TIME = :overTime, 
                        TRANSPORT = :transport, 
                        DATE_OF_JOINING = :doj,
                        TRANSPORT_STAND = :stand,
                        USER_ID = :userId
                    WHERE EMP_CODE = :empCode";

                using var command = new OracleCommand(sql, connection) { Transaction = oracleTransaction, BindByName = true };
                command.Parameters.Add(new OracleParameter("gross", item.Gross));
                command.Parameters.Add(new OracleParameter("overTime", item.OverTime));
                command.Parameters.Add(new OracleParameter("transport", item.Transport));
                command.Parameters.Add(new OracleParameter("doj", item.DateOfJoining ?? DateTime.Today));
                command.Parameters.Add(new OracleParameter("stand", item.TransportStand ?? string.Empty));
                command.Parameters.Add(new OracleParameter("userId", userId));
                command.Parameters.Add(new OracleParameter("empCode", item.EmployeeCode.Trim()));

                updatedCount += await command.ExecuteNonQueryAsync(cancellationToken);
                await LogActionAsync(connection, oracleTransaction, item.EmployeeCode, $"BULK_STATUS_UPDATE: Gross={item.Gross}, OT={item.OverTime}, TR={item.Transport}, Stand={item.TransportStand}", userId, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return updatedCount;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
