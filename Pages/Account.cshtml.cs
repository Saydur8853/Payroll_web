using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TG.Payroll.Web.Models;
using TG.Payroll.Web.Services;

namespace TG.Payroll.Web.Pages;

public class AccountModel(
    PayrollRepository payrollRepository,
    ILogger<AccountModel> logger) : PageModel
{
    public string CurrentUserName => HttpContext.Session.GetString("UserName") ?? "User";
    public string CompanyName => HttpContext.Session.GetString("CompanyName") ?? "Payroll";
    public bool CurrentUserIsAdmin => HttpContext.Session.GetString("IsAdmin") == "1";
    public List<NavigationItem> Menu { get; private set; } = [];
    public bool CredentialsUpdated { get; private set; }
    public string? CredentialError { get; private set; }

    [BindProperty] public string AccountUsername { get; set; } = string.Empty;
    [BindProperty] public string CurrentPassword { get; set; } = string.Empty;
    [BindProperty] public string NewAccountPassword { get; set; } = string.Empty;
    [BindProperty] public string ConfirmAccountPassword { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(bool credentialsUpdated = false, CancellationToken cancellationToken = default)
    {
        var userIdStr = HttpContext.Session.GetString("UserId");
        if (!int.TryParse(userIdStr, out var userId)) return RedirectToPage("/Index");

        CredentialsUpdated = credentialsUpdated;
        AccountUsername = CurrentUserName;
        Menu = await payrollRepository.GetMenuAsync(userId, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateCredentialsAsync(CancellationToken cancellationToken)
    {
        var userIdStr = HttpContext.Session.GetString("UserId");
        if (!int.TryParse(userIdStr, out var userId)) return RedirectToPage("/Index");

        if (string.IsNullOrWhiteSpace(AccountUsername) || string.IsNullOrWhiteSpace(CurrentPassword))
        {
            CredentialError = "Current password is required to save account changes.";
            Menu = await payrollRepository.GetMenuAsync(userId, cancellationToken);
            return Page();
        }

        if (!string.IsNullOrWhiteSpace(NewAccountPassword) && NewAccountPassword != ConfirmAccountPassword)
        {
            CredentialError = "New passwords do not match.";
            Menu = await payrollRepository.GetMenuAsync(userId, cancellationToken);
            return Page();
        }

        try
        {
            var updatedUsername = await payrollRepository.UpdateOwnCredentialsAsync(
                userId,
                CurrentPassword,
                AccountUsername.Trim(),
                NewAccountPassword,
                cancellationToken);

            HttpContext.Session.SetString("UserName", updatedUsername);
            return RedirectToPage(new { credentialsUpdated = true });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Credential update failed");
            CredentialError = "Failed to update profile: " + exception.Message;
            Menu = await payrollRepository.GetMenuAsync(userId, cancellationToken);
            return Page();
        }
    }
}
