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
app.MapRazorPages();
app.MapRazorComponents<TG.Payroll.Web.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
