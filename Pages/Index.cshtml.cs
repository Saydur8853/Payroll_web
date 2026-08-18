using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TG.Payroll.Web.Services;

namespace TG.Payroll.Web.Pages;

public class IndexModel(PayrollRepository payrollRepository, ILogger<IndexModel> logger) : PageModel
{
    [BindProperty] public string Username { get; set; } = string.Empty;
    [BindProperty] public string Password { get; set; } = string.Empty;
    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet()
        => HttpContext.Session.GetString("UserId") is null ? Page() : Redirect("/Dashboard");

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Enter your username and password.";
            return Page();
        }

        try
        {
            var user = await payrollRepository.AuthenticateAsync(Username, Password, cancellationToken);
            if (user is null)
            {
                ErrorMessage = "Invalid username or password.";
                return Page();
            }

            HttpContext.Session.SetString("UserId", user.Id.ToString());
            HttpContext.Session.SetString("UserName", user.Name);
            HttpContext.Session.SetString("CompanyName", user.CompanyName);
            HttpContext.Session.SetString("CompanyId", user.CompanyId.ToString());
            HttpContext.Session.SetString("IsAdmin", user.IsAdmin ? "1" : "0");

            if (!user.IsAdmin)
            {
                var menu = await payrollRepository.GetMenuAsync(user.Id, cancellationToken);
                var hasDashboard = HasModule(menu, item => item.Id == 184 || item.Name.Equals("Dashboard", StringComparison.OrdinalIgnoreCase));
                if (!hasDashboard)
                {
                    if (HasModule(menu, item => item.Name.Contains("Employee", StringComparison.OrdinalIgnoreCase)))
                        return Redirect("/EmployeeInformation");
                    if (HasModule(menu, item => item.Name.Contains("User Management", StringComparison.OrdinalIgnoreCase)))
                        return Redirect("/UserManagement");
                }
            }

            return Redirect("/Dashboard");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Payroll login could not reach the database.");
            ErrorMessage = "Unable to connect to the payroll database. Check appsettings.json.";
            return Page();
        }
    }

    private static bool HasModule(IEnumerable<TG.Payroll.Web.Models.NavigationItem> items, Func<TG.Payroll.Web.Models.NavigationItem, bool> predicate)
    {
        foreach (var item in items)
        {
            if (predicate(item)) return true;
            if (item.Children.Count > 0 && HasModule(item.Children, predicate)) return true;
        }
        return false;
    }
}
