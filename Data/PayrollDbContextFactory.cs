using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TG.Payroll.Web.Models;

namespace TG.Payroll.Web.Data;

/// <summary>Allows the EF command-line tools to create the context from appsettings.json.</summary>
public sealed class PayrollDbContextFactory : IDesignTimeDbContextFactory<PayrollDbContext>
{
    public PayrollDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        var options = new DbContextOptionsBuilder<PayrollDbContext>()
            .UseOracle(DatabaseOptions.GetConnectionString(configuration))
            .Options;
        return new PayrollDbContext(options);
    }
}
