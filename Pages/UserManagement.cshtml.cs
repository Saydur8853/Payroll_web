using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TG.Payroll.Web.Models;
using TG.Payroll.Web.Services;

namespace TG.Payroll.Web.Pages;

public class UserManagementModel(PayrollRepository payrollRepository, ILogger<UserManagementModel> logger) : PageModel
{
    private const int DashboardModuleId = 184;
    public List<PayrollUser> Users { get; private set; } = [];
    public List<NavigationItem> Modules { get; private set; } = [];
    public PayrollUser? SelectedUser { get; private set; }
    public bool SelectedUserIsAdmin => SelectedUser?.Admin == 1;
    public string CurrentUserName => HttpContext.Session.GetString("UserName") ?? "User";
    public string CompanyName => HttpContext.Session.GetString("CompanyName") ?? "Payroll";
    public bool CurrentUserIsAdmin => HttpContext.Session.GetString("IsAdmin") == "1";
    public bool Saved { get; private set; }
    public bool Created { get; private set; }
    public bool CredentialsUpdated { get; private set; }
    public string? CreateError { get; private set; }
    public string? CredentialError { get; private set; }

    [BindProperty] public int SelectedUserId { get; set; }
    [BindProperty] public List<int> SelectedModuleIds { get; set; } = [];
    [BindProperty] public string NewUsername { get; set; } = string.Empty;
    [BindProperty] public string NewPassword { get; set; } = string.Empty;
    [BindProperty] public string NewRole { get; set; } = "User";
    [BindProperty] public List<int> NewUserModuleIds { get; set; } = [];
    [BindProperty] public string AccountUsername { get; set; } = string.Empty;
    [BindProperty] public string CurrentPassword { get; set; } = string.Empty;
    [BindProperty] public string NewAccountPassword { get; set; } = string.Empty;
    [BindProperty] public string ConfirmAccountPassword { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(int? userId, bool saved = false, bool created = false, bool credentialsUpdated = false, CancellationToken cancellationToken = default)
    {
        var accessResult = CheckAccess();
        if (accessResult is not null) return accessResult;

        Saved = saved;
        Created = created;
        CredentialsUpdated = credentialsUpdated;
        NewUserModuleIds = [DashboardModuleId];
        await LoadAsync(userId, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSavePrivilegesAsync(CancellationToken cancellationToken)
    {
        var accessResult = CheckAccess();
        if (accessResult is not null) return accessResult;
        if (!CurrentUserIsAdmin) return Forbid();

        await payrollRepository.UpdateUserPrivilegesAsync(SelectedUserId, SelectedModuleIds, cancellationToken);
        return RedirectToPage(new { userId = SelectedUserId, saved = true });
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        var accessResult = CheckAccess();
        if (accessResult is not null) return accessResult;
        if (!CurrentUserIsAdmin) return Forbid();

        if (string.IsNullOrWhiteSpace(NewUsername) || string.IsNullOrWhiteSpace(NewPassword))
        {
            CreateError = "Username and password are required.";
            await LoadAsync(null, cancellationToken);
            return Page();
        }
        if (NewUsername.Trim().Length > 80 || NewPassword.Length > 50)
        {
            CreateError = "Username or password exceeds the database field length.";
            await LoadAsync(null, cancellationToken);
            return Page();
        }

        try
        {
            var callingUserId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
            var companyId = HttpContext.Session.GetString("CompanyId") ?? "1";
            var newUserId = await payrollRepository.CreateUserAsync(
                NewUsername,
                NewPassword,
                string.Equals(NewRole, "Admin", StringComparison.OrdinalIgnoreCase),
                callingUserId,
                companyId,
                NewUserModuleIds,
                cancellationToken);
            return RedirectToPage(new { userId = newUserId, created = true });
        }
        catch (InvalidOperationException exception)
        {
            CreateError = exception.Message;
            await LoadAsync(null, cancellationToken);
            return Page();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "User creation failed.");
            CreateError = "User could not be created. Check the database log for details.";
            await LoadAsync(null, cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostUpdateCredentialsAsync(CancellationToken cancellationToken)
    {
        var accessResult = CheckAccess();
        if (accessResult is not null) return accessResult;

        var currentUserId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
        if (string.IsNullOrWhiteSpace(AccountUsername) || string.IsNullOrWhiteSpace(CurrentPassword))
        {
            CredentialError = "Username and current password are required.";
            await LoadAsync(currentUserId, cancellationToken);
            return Page();
        }
        if (AccountUsername.Trim().Length > 80 || NewAccountPassword.Length > 50)
        {
            CredentialError = "Username or password exceeds the database field length.";
            await LoadAsync(currentUserId, cancellationToken);
            return Page();
        }
        if (!string.Equals(NewAccountPassword, ConfirmAccountPassword, StringComparison.Ordinal))
        {
            CredentialError = "New password and confirmation do not match.";
            await LoadAsync(currentUserId, cancellationToken);
            return Page();
        }

        try
        {
            var updatedUsername = await payrollRepository.UpdateOwnCredentialsAsync(
                currentUserId, CurrentPassword, AccountUsername, NewAccountPassword, cancellationToken);
            HttpContext.Session.SetString("UserName", updatedUsername);
            return RedirectToPage(new { userId = currentUserId, credentialsUpdated = true });
        }
        catch (InvalidOperationException exception)
        {
            CredentialError = exception.Message;
            await LoadAsync(currentUserId, cancellationToken);
            return Page();
        }
    }

    private IActionResult? CheckAccess()
    {
        if (HttpContext.Session.GetString("UserId") is null) return RedirectToPage("/Index");
        return null;
    }

    private async Task LoadAsync(int? userId, CancellationToken cancellationToken)
    {
        var currentUserId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
        Users = await payrollRepository.GetUsersAsync(cancellationToken);
        var currentUser = Users.FirstOrDefault(user => user.UserId == currentUserId);
        if (currentUser is not null && string.IsNullOrWhiteSpace(AccountUsername))
            AccountUsername = currentUser.UserName ?? string.Empty;
        if (!CurrentUserIsAdmin) Users = Users.Where(user => user.UserId == currentUserId).ToList();
        Modules = (await payrollRepository.GetAllMenuControlsAsync(cancellationToken))
            .OrderBy(module => module.Id)
            .ToList();
        SelectedUserId = CurrentUserIsAdmin ? userId ?? Users.FirstOrDefault()?.UserId ?? 0 : currentUserId;
        SelectedUser = Users.FirstOrDefault(user => user.UserId == SelectedUserId);
        if (SelectedUser is null) return;

        SelectedModuleIds = SelectedUserIsAdmin
            ? Modules.Select(module => module.Id).ToList()
            : (SelectedUser.PrivilegeArray ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => int.TryParse(value, out var id) ? id : 0)
                .Where(id => id > 0)
                .ToList();
    }
}
