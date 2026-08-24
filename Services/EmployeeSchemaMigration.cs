using Oracle.ManagedDataAccess.Client;
using TG.Payroll.Web.Models;

namespace TG.Payroll.Web.Services;

/// <summary>One-time database setup for safe concurrent employee creation.</summary>
public sealed class EmployeeSchemaMigration(IConfiguration configuration)
{
    private readonly string _connectionString = DatabaseOptions.GetConnectionString(configuration);

    public async Task ApplyEmployeeConcurrencyAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string duplicateSql = """
            SELECT COUNT(*) FROM (
                SELECT UPPER(TRIM(EMP_CODE))
                FROM EMP_OFFICIAL
                GROUP BY UPPER(TRIM(EMP_CODE))
                HAVING COUNT(*) > 1
            )
            """;
        if (await ScalarIntAsync(connection, duplicateSql, cancellationToken) > 0)
            throw new InvalidOperationException("Cannot enforce unique employee codes because duplicate EMP_CODE values already exist.");

        await CreateSequenceIfMissingAsync(connection, "EMP_OFFICIAL_ID_SEQ",
            "SELECT NVL(MAX(EMP_ID), 0) + 1 FROM EMP_OFFICIAL", cancellationToken);
        await CreateSequenceIfMissingAsync(connection, "EMP_CODE_SEQ",
            "SELECT NVL(MAX(TO_NUMBER(TRIM(EMP_CODE))), 0) + 1 FROM EMP_OFFICIAL WHERE REGEXP_LIKE(TRIM(EMP_CODE), '^[0-9]+$')", cancellationToken);

        const string indexExistsSql = "SELECT COUNT(*) FROM USER_INDEXES WHERE INDEX_NAME = 'UX_EMP_OFFICIAL_CODE_NORM'";
        if (await ScalarIntAsync(connection, indexExistsSql, cancellationToken) == 0)
        {
            await using var command = new OracleCommand("CREATE UNIQUE INDEX UX_EMP_OFFICIAL_CODE_NORM ON EMP_OFFICIAL (UPPER(TRIM(EMP_CODE)))", connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task CreateSequenceIfMissingAsync(OracleConnection connection, string sequenceName, string startQuery, CancellationToken token)
    {
        await using var existsCommand = new OracleCommand("SELECT COUNT(*) FROM USER_SEQUENCES WHERE SEQUENCE_NAME = :sequenceName", connection) { BindByName = true };
        existsCommand.Parameters.Add(new OracleParameter("sequenceName", sequenceName));
        if (Convert.ToInt32(await existsCommand.ExecuteScalarAsync(token)) > 0) return;

        var startValue = await ScalarDecimalAsync(connection, startQuery, token);
        await using var createCommand = new OracleCommand($"CREATE SEQUENCE {sequenceName} START WITH {startValue:0} INCREMENT BY 1 NOCACHE", connection);
        await createCommand.ExecuteNonQueryAsync(token);
    }

    private static async Task<int> ScalarIntAsync(OracleConnection connection, string sql, CancellationToken token)
    {
        await using var command = new OracleCommand(sql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync(token));
    }

    private static async Task<decimal> ScalarDecimalAsync(OracleConnection connection, string sql, CancellationToken token)
    {
        await using var command = new OracleCommand(sql, connection);
        return Convert.ToDecimal(await command.ExecuteScalarAsync(token));
    }

    public async Task MigrateBanglaColumnsToUnicodeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // 1. Alter columns to NVARCHAR2
        var alterStatements = new[]
        {
            "ALTER TABLE DESIGNATION MODIFY (BANG_DESIGNATION_NAME NVARCHAR2(300), BANG_GRADE NVARCHAR2(100))",
            "ALTER TABLE UNIT MODIFY (BANG_UNIT_NAME NVARCHAR2(300), BANG_ADDRESS NVARCHAR2(500))",
            "ALTER TABLE DEPARTMENT MODIFY (BANG_DEPT_NAME NVARCHAR2(300))",
            "ALTER TABLE EMP_CATEGORY MODIFY (BANG_EMP_TYPE_NAME NVARCHAR2(300))",
            "ALTER TABLE SECTION MODIFY (BANG_SEC_NAME NVARCHAR2(300))",
            "ALTER TABLE LINE MODIFY (BANG_LINE_NAME NVARCHAR2(300))",
            "ALTER TABLE COMPANY MODIFY (COMPANY_NAME_BANG NVARCHAR2(300), ADDRESS_BANG NVARCHAR2(500))"
        };

        foreach (var sql in alterStatements)
        {
            try
            {
                await using var cmd = new OracleCommand(sql, connection);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // Column might already be NVARCHAR2 or table locked, log and continue
                Console.WriteLine($"Alter info ({sql}): {ex.Message}");
            }
        }

        // 2. Convert existing Bijoy data to native Unicode in DESIGNATION table
        try
        {
            await using var selectCmd = new OracleCommand(
                "SELECT DESIGNATION_ID, BANG_DESIGNATION_NAME, BANG_GRADE FROM DESIGNATION WHERE BANG_DESIGNATION_NAME IS NOT NULL OR BANG_GRADE IS NOT NULL", 
                connection);
            await using var reader = await selectCmd.ExecuteReaderAsync(cancellationToken);
            var updates = new List<(int Id, string BangName, string BangGrade)>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = Convert.ToInt32(reader["DESIGNATION_ID"]);
                var rawName = Convert.ToString(reader["BANG_DESIGNATION_NAME"]) ?? "";
                var rawGrade = Convert.ToString(reader["BANG_GRADE"]) ?? "";
                var uName = BanglaBijoyConverter.ConvertToUnicode(rawName);
                var uGrade = BanglaBijoyConverter.ConvertToUnicode(rawGrade);
                if (uName != rawName || uGrade != rawGrade)
                {
                    updates.Add((id, uName, uGrade));
                }
            }
            await reader.CloseAsync();

            foreach (var (id, uName, uGrade) in updates)
            {
                await using var updCmd = new OracleCommand(
                    "UPDATE DESIGNATION SET BANG_DESIGNATION_NAME = :bName, BANG_GRADE = :bGrade WHERE DESIGNATION_ID = :id", 
                    connection) { BindByName = true };
                updCmd.Parameters.Add(new OracleParameter("bName", OracleDbType.NVarchar2) { Value = uName });
                updCmd.Parameters.Add(new OracleParameter("bGrade", OracleDbType.NVarchar2) { Value = uGrade });
                updCmd.Parameters.Add(new OracleParameter("id", id));
                await updCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            Console.WriteLine($"Migrated {updates.Count} designation records to direct Unicode in Oracle DB.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error converting designation data: {ex.Message}");
        }

        // 3. Convert existing data in UNIT table
        try
        {
            await using var selectCmd = new OracleCommand(
                "SELECT UNIT_ID, BANG_UNIT_NAME, BANG_ADDRESS FROM UNIT WHERE BANG_UNIT_NAME IS NOT NULL OR BANG_ADDRESS IS NOT NULL", 
                connection);
            await using var reader = await selectCmd.ExecuteReaderAsync(cancellationToken);
            var updates = new List<(int Id, string Name, string Addr)>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = Convert.ToInt32(reader["UNIT_ID"]);
                var rawName = Convert.ToString(reader["BANG_UNIT_NAME"]) ?? "";
                var rawAddr = Convert.ToString(reader["BANG_ADDRESS"]) ?? "";
                var uName = BanglaBijoyConverter.ConvertToUnicode(rawName);
                var uAddr = BanglaBijoyConverter.ConvertToUnicode(rawAddr);
                if (uName != rawName || uAddr != rawAddr) updates.Add((id, uName, uAddr));
            }
            await reader.CloseAsync();

            foreach (var (id, uName, uAddr) in updates)
            {
                await using var updCmd = new OracleCommand(
                    "UPDATE UNIT SET BANG_UNIT_NAME = :bName, BANG_ADDRESS = :bAddr WHERE UNIT_ID = :id", 
                    connection) { BindByName = true };
                updCmd.Parameters.Add(new OracleParameter("bName", OracleDbType.NVarchar2) { Value = uName });
                updCmd.Parameters.Add(new OracleParameter("bAddr", OracleDbType.NVarchar2) { Value = uAddr });
                updCmd.Parameters.Add(new OracleParameter("id", id));
                await updCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            Console.WriteLine($"Migrated {updates.Count} unit records to direct Unicode.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error converting unit data: {ex.Message}");
        }

        // 4. Convert existing data in DEPARTMENT table
        try
        {
            await using var selectCmd = new OracleCommand(
                "SELECT DEPARTMENT_ID, BANG_DEPT_NAME FROM DEPARTMENT WHERE BANG_DEPT_NAME IS NOT NULL", 
                connection);
            await using var reader = await selectCmd.ExecuteReaderAsync(cancellationToken);
            var updates = new List<(int Id, string Name)>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = Convert.ToInt32(reader["DEPARTMENT_ID"]);
                var rawName = Convert.ToString(reader["BANG_DEPT_NAME"]) ?? "";
                var uName = BanglaBijoyConverter.ConvertToUnicode(rawName);
                if (uName != rawName) updates.Add((id, uName));
            }
            await reader.CloseAsync();

            foreach (var (id, uName) in updates)
            {
                await using var updCmd = new OracleCommand(
                    "UPDATE DEPARTMENT SET BANG_DEPT_NAME = :bName WHERE DEPARTMENT_ID = :id", 
                    connection) { BindByName = true };
                updCmd.Parameters.Add(new OracleParameter("bName", OracleDbType.NVarchar2) { Value = uName });
                updCmd.Parameters.Add(new OracleParameter("id", id));
                await updCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            Console.WriteLine($"Migrated {updates.Count} department records to direct Unicode.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error converting department data: {ex.Message}");
        }

        // 5. Convert existing data in SECTION table
        try
        {
            await using var selectCmd = new OracleCommand(
                "SELECT SECTION_ID, BANG_SEC_NAME FROM SECTION WHERE BANG_SEC_NAME IS NOT NULL", 
                connection);
            await using var reader = await selectCmd.ExecuteReaderAsync(cancellationToken);
            var updates = new List<(int Id, string Name)>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = Convert.ToInt32(reader["SECTION_ID"]);
                var rawName = Convert.ToString(reader["BANG_SEC_NAME"]) ?? "";
                var uName = BanglaBijoyConverter.ConvertToUnicode(rawName);
                if (uName != rawName) updates.Add((id, uName));
            }
            await reader.CloseAsync();

            foreach (var (id, uName) in updates)
            {
                await using var updCmd = new OracleCommand(
                    "UPDATE SECTION SET BANG_SEC_NAME = :bName WHERE SECTION_ID = :id", 
                    connection) { BindByName = true };
                updCmd.Parameters.Add(new OracleParameter("bName", OracleDbType.NVarchar2) { Value = uName });
                updCmd.Parameters.Add(new OracleParameter("id", id));
                await updCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            Console.WriteLine($"Migrated {updates.Count} section records to direct Unicode.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error converting section data: {ex.Message}");
        }
    }
}

