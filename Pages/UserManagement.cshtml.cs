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
    public List<NavigationItem> ModuleTree { get; private set; } = [];
    public List<NavigationItem> Menu { get; private set; } = [];
    public List<ControlItem> AllControls { get; private set; } = [];
    public List<string> ControlTypes { get; private set; } = ["MENU", "FORM", "REPORT", "BUTTON"];
    public List<CompanyItem> Companies { get; private set; } = [];
    public PayrollUser? SelectedUser { get; private set; }
    public bool SelectedUserIsAdmin => SelectedUser?.Admin == 1;
    public string CurrentUserName => HttpContext.Session.GetString("UserName") ?? "User";
    public string CompanyName => HttpContext.Session.GetString("CompanyName") ?? "Payroll";
    public bool CurrentUserIsAdmin => HttpContext.Session.GetString("IsAdmin") == "1";
    public bool Saved { get; private set; }
    public bool Created { get; private set; }
    public bool CredentialsUpdated { get; private set; }
    public bool ControlCreated { get; private set; }
    public bool ControlUpdated { get; private set; }
    public bool ControlDeleted { get; private set; }
    public bool CompanyCreated { get; private set; }
    public bool CompanyUpdated { get; private set; }
    public bool CompanyDeleted { get; private set; }
    public string? CreateError { get; private set; }
    public string? CredentialError { get; private set; }
    public string? ControlError { get; private set; }
    public string? CompanyError { get; private set; }

    [BindProperty(SupportsGet = true)] public string ActiveTab { get; set; } = "users";

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

    // Control Entry Form Bindings
    [BindProperty] public int EditControlId { get; set; }
    [BindProperty] public string ControlNameInput { get; set; } = string.Empty;
    [BindProperty] public int? CallingIdInput { get; set; }
    [BindProperty] public string ControlTypeInput { get; set; } = "MENU";
    [BindProperty] public int PriorityInput { get; set; } = 1;

    // Company Form Bindings
    [BindProperty] public int EditCompanyId { get; set; }
    [BindProperty] public string CompanyNameInput { get; set; } = string.Empty;
    [BindProperty] public string CompanyAddressInput { get; set; } = string.Empty;
    [BindProperty] public string CompanyRemarksInput { get; set; } = string.Empty;
    [BindProperty] public string CompanyLogoPathInput { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(
        int? userId,
        bool saved = false,
        bool created = false,
        bool credentialsUpdated = false,
        bool controlCreated = false,
        bool controlUpdated = false,
        bool controlDeleted = false,
        bool companyCreated = false,
        bool companyUpdated = false,
        bool companyDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var accessResult = CheckAccess();
        if (accessResult is not null) return accessResult;

        Saved = saved;
        Created = created;
        CredentialsUpdated = credentialsUpdated;
        ControlCreated = controlCreated;
        ControlUpdated = controlUpdated;
        ControlDeleted = controlDeleted;
        CompanyCreated = companyCreated;
        CompanyUpdated = companyUpdated;
        CompanyDeleted = companyDeleted;
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



    public async Task<IActionResult> OnPostCreateControlAsync(CancellationToken cancellationToken)
    {
        var accessResult = CheckAccess();
        if (accessResult is not null) return accessResult;
        if (!CurrentUserIsAdmin) return Forbid();

        if (string.IsNullOrWhiteSpace(ControlNameInput))
        {
            ControlError = "Control Name is required.";
            ActiveTab = "controls";
            await LoadAsync(null, cancellationToken);
            return Page();
        }

        try
        {
            await payrollRepository.CreateControlAsync(
                ControlNameInput,
                CallingIdInput,
                ControlTypeInput,
                PriorityInput,
                cancellationToken);
            return RedirectToPage(new { activeTab = "controls", controlCreated = true });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to create control");
            ControlError = "Failed to create control. Please check if duplicate or database error.";
            ActiveTab = "controls";
            await LoadAsync(null, cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostUpdateControlAsync(CancellationToken cancellationToken)
    {
        var accessResult = CheckAccess();
        if (accessResult is not null) return accessResult;
        if (!CurrentUserIsAdmin) return Forbid();

        if (EditControlId <= 0 || string.IsNullOrWhiteSpace(ControlNameInput))
        {
            ControlError = "Valid Control ID and Name are required.";
            ActiveTab = "controls";
            await LoadAsync(null, cancellationToken);
            return Page();
        }

        try
        {
            await payrollRepository.UpdateControlAsync(
                EditControlId,
                ControlNameInput,
                CallingIdInput,
                ControlTypeInput,
                PriorityInput,
                cancellationToken);
            return RedirectToPage(new { activeTab = "controls", controlUpdated = true });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to update control");
            ControlError = "Failed to update control: " + exception.Message;
            ActiveTab = "controls";
            await LoadAsync(null, cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteControlAsync(int controlId, CancellationToken cancellationToken)
    {
        var accessResult = CheckAccess();
        if (accessResult is not null) return accessResult;
        if (!CurrentUserIsAdmin) return Forbid();

        try
        {
            await payrollRepository.DeleteControlAsync(controlId, cancellationToken);
            return RedirectToPage(new { activeTab = "controls", controlDeleted = true });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to delete control");
            ControlError = "Failed to delete control: " + exception.Message;
            ActiveTab = "controls";
            await LoadAsync(null, cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostCreateCompanyAsync(CancellationToken cancellationToken)
    {
        var accessResult = CheckAccess();
        if (accessResult is not null) return accessResult;
        if (!CurrentUserIsAdmin) return Forbid();

        if (string.IsNullOrWhiteSpace(CompanyNameInput))
        {
            CompanyError = "Company Name is required.";
            ActiveTab = "company";
            await LoadAsync(null, cancellationToken);
            return Page();
        }

        try
        {
            await payrollRepository.CreateCompanyAsync(
                CompanyNameInput,
                CompanyAddressInput,
                CompanyRemarksInput,
                CompanyLogoPathInput,
                cancellationToken);
            return RedirectToPage(new { activeTab = "company", companyCreated = true });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to create company");
            CompanyError = "Failed to create company: " + exception.Message;
            ActiveTab = "company";
            await LoadAsync(null, cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostUpdateCompanyAsync(CancellationToken cancellationToken)
    {
        var accessResult = CheckAccess();
        if (accessResult is not null) return accessResult;
        if (!CurrentUserIsAdmin) return Forbid();

        if (EditCompanyId <= 0 || string.IsNullOrWhiteSpace(CompanyNameInput))
        {
            CompanyError = "Valid Company ID and Name are required.";
            ActiveTab = "company";
            await LoadAsync(null, cancellationToken);
            return Page();
        }

        try
        {
            await payrollRepository.UpdateCompanyAsync(
                EditCompanyId,
                CompanyNameInput,
                CompanyAddressInput,
                CompanyRemarksInput,
                CompanyLogoPathInput,
                cancellationToken);
            return RedirectToPage(new { activeTab = "company", companyUpdated = true });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to update company");
            CompanyError = "Failed to update company: " + exception.Message;
            ActiveTab = "company";
            await LoadAsync(null, cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteCompanyAsync(int companyId, CancellationToken cancellationToken)
    {
        var accessResult = CheckAccess();
        if (accessResult is not null) return accessResult;
        if (!CurrentUserIsAdmin) return Forbid();

        try
        {
            await payrollRepository.DeleteCompanyAsync(companyId, cancellationToken);
            return RedirectToPage(new { activeTab = "company", companyDeleted = true });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to delete company");
            CompanyError = "Failed to delete company: " + exception.Message;
            ActiveTab = "company";
            await LoadAsync(null, cancellationToken);
            return Page();
        }
    }

    private IActionResult? CheckAccess()
    {
        if (HttpContext.Session.GetString("UserId") is null) return RedirectToPage("/Index");
        if (!CurrentUserIsAdmin) return RedirectToPage("/Account");
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
        ModuleTree = await payrollRepository.GetAllMenuTreeAsync(cancellationToken);
        Menu = await payrollRepository.GetMenuAsync(currentUserId, cancellationToken);
        AllControls = await payrollRepository.GetControlsDetailedAsync(cancellationToken);
        ControlTypes = await payrollRepository.GetDistinctControlTypesAsync(cancellationToken);
        Companies = await payrollRepository.GetCompaniesAsync(cancellationToken);
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
