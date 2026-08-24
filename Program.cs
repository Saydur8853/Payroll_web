using Microsoft.EntityFrameworkCore;
using TG.Payroll.Web.Data;
using TG.Payroll.Web.Models;
using TG.Payroll.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDbContext<PayrollDbContext>(options =>
    options.UseOracle(DatabaseOptions.GetConnectionString(builder.Configuration)));
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".TGPayroll.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromHours(8);
});
builder.Services.AddScoped<PayrollRepository>();
builder.Services.AddScoped<EmployeeRepository>();
builder.Services.AddScoped<MasterRepository>();
builder.Services.AddScoped<EmployeeSchemaMigration>();

var app = builder.Build();

if (args.Contains("--check-db", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var repository = scope.ServiceProvider.GetRequiredService<PayrollRepository>();
    await repository.CheckConnectionAsync();
    Console.WriteLine("Oracle database connection succeeded.");
    return;
}




if (args.Contains("--check-dashboard", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var repository = scope.ServiceProvider.GetRequiredService<PayrollRepository>();
    var dashboard = await repository.GetDashboardAsync();
    Console.WriteLine($"Dashboard query succeeded. Active employees: {dashboard.ActiveEmployees}.");
    return;
}

if (args.Contains("--check-employees", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var repository = scope.ServiceProvider.GetRequiredService<EmployeeRepository>();
    var payrollRepository = scope.ServiceProvider.GetRequiredService<PayrollRepository>();
    var employees = await repository.GetEmployeesAsync(null);
    var lookups = await repository.GetLookupsAsync();
    var employeeMenus = (await payrollRepository.GetAllMenuControlsAsync())
        .Where(item => item.Id == 12 || item.Name.Contains("Employee Information", StringComparison.OrdinalIgnoreCase))
        .Select(item => $"{item.Id}:{item.Name} (parent {item.ParentId?.ToString() ?? "none"})");
    var employeeAccess = (await payrollRepository.GetUsersAsync())
        .Select(user => $"{user.UserId}:{user.UserName}={(user.Admin == 1 || (user.PrivilegeArray ?? string.Empty).Split(',').Contains("125") ? "yes" : "no")}");
    if (employees.Count > 0) await repository.GetEmployeeAsync(employees[0].EmployeeId);
    Console.WriteLine($"Employee queries succeeded. Employees returned: {employees.Count}; departments: {lookups.Departments.Count}; menu: {string.Join(", ", employeeMenus)}; access: {string.Join(", ", employeeAccess)}.");
    return;
}



if (args.Contains("--check-schema", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<PayrollDbContext>();
    var conn = db.Database.GetDbConnection();
    await conn.OpenAsync();

    Console.WriteLine("=== Columns matching %PRIOR% ===");
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = "SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE FROM USER_TAB_COLUMNS WHERE COLUMN_NAME LIKE '%PRIOR%' ORDER BY TABLE_NAME";
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            Console.WriteLine($"{reader[0]}.{reader[1]} ({reader[2]})");
        }
    }

    Console.WriteLine("\n=== Columns matching %CONTACT% or %CELL% or %PHONE% ===");
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = "SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE FROM USER_TAB_COLUMNS WHERE COLUMN_NAME LIKE '%CONTACT%' OR COLUMN_NAME LIKE '%CELL%' OR COLUMN_NAME LIKE '%PHONE%' ORDER BY TABLE_NAME";
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            Console.WriteLine($"{reader[0]}.{reader[1]} ({reader[2]})");
        }
    }
    return;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAntiforgery();
app.MapPost("/logout", (HttpContext context) =>
{
    context.Session.Clear();
    return Results.Redirect("/");
});
app.MapGet("/api/company/logo/{id:int}", async (int id, PayrollRepository repo, CancellationToken ct) =>
{
    try
    {
        var companies = await repo.GetCompaniesAsync(ct);
        var comp = companies.FirstOrDefault(c => c.CompanyId == id);
        if (comp is null) return Results.NotFound();

        if (comp.CompanyLogo is not null && comp.CompanyLogo.Length > 0)
        {
            var mime = comp.CompanyLogo.Length > 3 && comp.CompanyLogo[0] == 0x89 && comp.CompanyLogo[1] == 0x50 && comp.CompanyLogo[2] == 0x4E && comp.CompanyLogo[3] == 0x47
                ? "image/png"
                : (comp.CompanyLogo.Length > 2 && comp.CompanyLogo[0] == 0xFF && comp.CompanyLogo[1] == 0xD8 && comp.CompanyLogo[2] == 0xFF ? "image/jpeg" : "image/png");
            return Results.File(comp.CompanyLogo, mime);
        }

        if (!string.IsNullOrWhiteSpace(comp.CompanyLogoPath) && File.Exists(comp.CompanyLogoPath))
        {
            var bytes = await File.ReadAllBytesAsync(comp.CompanyLogoPath, ct);
            var ext = Path.GetExtension(comp.CompanyLogoPath).ToLowerInvariant();
            var mime = ext switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".svg" => "image/svg+xml", ".gif" => "image/gif", _ => "image/png" };
            return Results.File(bytes, mime);
        }
    }
    catch { }

    return Results.NotFound();
});

app.MapRazorPages();
app.MapRazorComponents<TG.Payroll.Web.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
