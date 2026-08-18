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
}
