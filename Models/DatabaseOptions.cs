namespace TG.Payroll.Web.Models;

using Oracle.ManagedDataAccess.Client;

public sealed class DatabaseOptions
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 1521;
    public string ServiceName { get; init; } = "orcl";
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;

    public static string GetConnectionString(IConfiguration configuration)
    {
        var options = configuration.GetSection("Database").Get<DatabaseOptions>() ?? new DatabaseOptions();
        return new OracleConnectionStringBuilder
        {
            DataSource = $"{options.Host}:{options.Port}/{options.ServiceName}",
            UserID = options.Username,
            Password = options.Password
        }.ConnectionString;
    }
}
