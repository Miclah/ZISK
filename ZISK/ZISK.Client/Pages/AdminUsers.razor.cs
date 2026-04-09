using Microsoft.AspNetCore.Components;
using MudBlazor;
using ZISK.Client.Components;
using ZISK.Client.Services;
using ZISK.Shared.DTOs.Users;

namespace ZISK.Client.Pages;

public partial class AdminUsers
{
    [Inject] private IUsersApi UsersApi { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    private bool _isLoading = true;
    private bool _isSaving;
    private string? _error;

    private List<UserListDto> _users = new();
    private List<ParentOptionDto> _parents = new();

    private string _searchText = string.Empty;
    private string? _selectedRole;

    private bool _editDialogVisible;
    private bool _isCreating;
    private UserListDto? _selectedUser;

    private string _editFirstName = string.Empty;
    private string _editLastName = string.Empty;
    private string _editEmail = string.Empty;
    private string _editRole = "Parent";
    private string _editPhone = string.Empty;
    private string _editRodneCislo = string.Empty;
    private string _editBydlisko = string.Empty;
    private DateTime? _editDateOfBirthDate;
    private bool _generateRandomPassword = true;

    private HashSet<string> _selectedParentIds = new();
    private string _parentSearch = string.Empty;

    private readonly List<BreadcrumbItem> _breadcrumbs =
    [
        new BreadcrumbItem("Domov", href: "/"),
        new BreadcrumbItem("Administrácia", href: "/admin"),
        new BreadcrumbItem("Používatelia", href: null, disabled: true)
    ];

    private IEnumerable<UserListDto> FilteredUsers => _users
        .Where(u => string.IsNullOrWhiteSpace(_searchText)
            || ($"{u.FirstName} {u.LastName}").Contains(_searchText, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(u.PhoneNumber) && u.PhoneNumber.Contains(_searchText, StringComparison.OrdinalIgnoreCase)))
        .Where(u => string.IsNullOrWhiteSpace(_selectedRole) || u.Role == _selectedRole)
        .OrderBy(u => u.LastName)
        .ThenBy(u => u.FirstName);

    private IEnumerable<ParentOptionDto> FilteredParents => _parents
        .Where(p => string.IsNullOrWhiteSpace(_parentSearch) || p.FullName.Contains(_parentSearch, StringComparison.OrdinalIgnoreCase));

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        try
        {
            _isLoading = true;
            _error = null;

            var usersTask = UsersApi.GetUsersAsync();
            var parentsTask = UsersApi.GetParentsAsync();

            await Task.WhenAll(usersTask, parentsTask);
            _users = await usersTask;
            _parents = await parentsTask;
        }
        catch (Exception ex)
        {
            _error = $"Nepodarilo sa načítať používateľov: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task OpenCreateDialog()
    {
        var parameters = new DialogParameters
        {
            [nameof(AddUserDialog.Model)] = new AddUserDialog.AddUserDialogModel()
            {
                Role = "Parent",
                GeneratePassword = true
            }
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, CloseButton = true };
        var dialog = await DialogService.ShowAsync<AddUserDialog>("Pridať používateľa", parameters, options);
        var result = await dialog.Result;

        if (result.Canceled || result.Data is not AddUserDialog.AddUserDialogModel model)
            return;

        _isSaving = true;
        try
        {
            var createEmail = BuildCreateEmail(model);
            var initialPassword = GenerateInitialPassword(model);
            var request = model.ToRequest(initialPassword, createEmail);

            await UsersApi.CreateUserAsync(request);
            Snackbar.Add("Používateľ bol vytvorený.", Severity.Success);
            await LoadData();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Chyba pri ukladaní: {ApiErrorFormatter.ToUserMessage(ex)}", Severity.Error);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task OpenEditDialog(UserListDto user)
    {
        _isCreating = false;
        _selectedUser = user;
        _editFirstName = user.FirstName;
        _editLastName = user.LastName;
        _editRole = user.Role;
        _editPhone = user.PhoneNumber ?? string.Empty;
        _editRodneCislo = string.Empty;
        _editBydlisko = string.Empty;
        _editDateOfBirthDate = null;
        _selectedParentIds = new HashSet<string>();
        _parentSearch = string.Empty;

        try
        {
            var detail = await UsersApi.GetUserAsync(user.Id);
            _editPhone = detail.PhoneNumber ?? string.Empty;
            _editRodneCislo = detail.RodneCislo ?? string.Empty;
            _editBydlisko = detail.Bydlisko ?? string.Empty;
            _editDateOfBirthDate = detail.DateOfBirth?.ToDateTime(TimeOnly.MinValue);
            _selectedParentIds = detail.Parents.Select(p => p.Id).ToHashSet();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Nepodarilo sa načítať detail používateľa: {ex.Message}", Severity.Warning);
        }

        _editDialogVisible = true;
    }

    private void CloseEditDialog()
    {
        _editDialogVisible = false;
    }

    private async Task SaveUser()
    {
        if (string.IsNullOrWhiteSpace(_editFirstName) || string.IsNullOrWhiteSpace(_editLastName))
        {
            Snackbar.Add("Meno a priezvisko sú povinné.", Severity.Warning);
            return;
        }

        if (_isCreating && string.IsNullOrWhiteSpace(_editPhone))
        {
            Snackbar.Add("Pri vytváraní je telefón povinný.", Severity.Warning);
            return;
        }

        if (_editRole == "Child" && _selectedParentIds.Count == 0)
        {
            Snackbar.Add("Pre dieťa je potrebné vybrať aspoň jedného rodiča.", Severity.Warning);
            return;
        }

        var dateOfBirth = _editDateOfBirthDate.HasValue
            ? DateOnly.FromDateTime(_editDateOfBirthDate.Value)
            : (DateOnly?)null;

        if (_editRole == "Child" && !dateOfBirth.HasValue)
        {
            Snackbar.Add("Pre dieťa je dátum narodenia povinný.", Severity.Warning);
            return;
        }

        _isSaving = true;
        try
        {
            if (_isCreating)
            {
                var createEmail = BuildCreateEmail();
                var initialPassword = GenerateInitialPassword();

                var createRequest = new CreateUserRequest(
                    _editFirstName,
                    _editLastName,
                    createEmail,
                    initialPassword,
                    _editRole,
                    string.IsNullOrWhiteSpace(_editPhone) ? null : _editPhone,
                    string.IsNullOrWhiteSpace(_editRodneCislo) ? null : _editRodneCislo,
                    string.IsNullOrWhiteSpace(_editBydlisko) ? null : _editBydlisko,
                    dateOfBirth,
                    _selectedParentIds.ToList());

                await UsersApi.CreateUserAsync(createRequest);
                Snackbar.Add("Používateľ bol vytvorený.", Severity.Success);
            }
            else if (_selectedUser != null)
            {
                var updateRequest = new UpdateUserRequest(
                    _editFirstName,
                    _editLastName,
                    _editRole,
                    null,
                    null,
                    string.IsNullOrWhiteSpace(_editPhone) ? null : _editPhone,
                    string.IsNullOrWhiteSpace(_editRodneCislo) ? null : _editRodneCislo,
                    string.IsNullOrWhiteSpace(_editBydlisko) ? null : _editBydlisko,
                    dateOfBirth,
                    _selectedParentIds.ToList());

                await UsersApi.UpdateUserAsync(_selectedUser.Id, updateRequest);
                Snackbar.Add("Používateľ bol upravený.", Severity.Success);
            }

            _editDialogVisible = false;
            await LoadData();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Chyba pri ukladaní: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task ConfirmDeleteUser(UserListDto user)
    {
        bool? result = await DialogService.ShowMessageBoxAsync(
            "Upozornenie: trvalé zmazanie",
            $"Naozaj chcete natrvalo vymazať používateľa '{user.FirstName} {user.LastName}'? Táto akcia je nevratná.",
            yesText: "Áno, vymazať",
            cancelText: "Zrušiť",
            options: new DialogOptions { MaxWidth = MaxWidth.Small });

        if (result != true)
            return;

        try
        {
            await UsersApi.DeleteUserAsync(user.Id);
            Snackbar.Add("Používateľ bol odstránený.", Severity.Success);
            await LoadData();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Chyba pri mazaní: {ex.Message}", Severity.Error);
        }
    }

    private static string GetRoleText(string role) => role switch
    {
        "Admin" => "Admin",
        "Coach" => "Tréner",
        "Parent" => "Rodič",
        "Athlete" => "Športovec",
        "Child" => "Dieťa",
        _ => role
    };

    private static Color GetRoleColor(string role) => role switch
    {
        "Admin" => Color.Error,
        "Coach" => Color.Primary,
        "Parent" => Color.Secondary,
        "Athlete" => Color.Info,
        "Child" => Color.Success,
        _ => Color.Default
    };

    private static string GetUserInitials(UserListDto user)
    {
        var first = string.IsNullOrWhiteSpace(user.FirstName) ? "?" : user.FirstName.Trim()[0].ToString().ToUpperInvariant();
        var last = string.IsNullOrWhiteSpace(user.LastName) ? string.Empty : user.LastName.Trim()[0].ToString().ToUpperInvariant();
        return $"{first}{last}";
    }

    private string BuildCreateEmail()
    {
        if (!string.IsNullOrWhiteSpace(_editEmail))
            return _editEmail.Trim();

        var phoneDigits = new string((_editPhone ?? string.Empty).Where(char.IsDigit).ToArray());
        var suffix = string.IsNullOrWhiteSpace(phoneDigits) ? Guid.NewGuid().ToString("N")[..8] : phoneDigits;
        return $"{suffix}@zisk.local";
    }

    private static string BuildCreateEmail(AddUserDialog.AddUserDialogModel model)
    {
        if (!string.IsNullOrWhiteSpace(model.Email))
            return model.Email.Trim();

        var phoneDigits = new string((model.PhoneNumber ?? string.Empty).Where(char.IsDigit).ToArray());
        var suffix = string.IsNullOrWhiteSpace(phoneDigits) ? Guid.NewGuid().ToString("N")[..8] : phoneDigits;
        return $"{suffix}@zisk.local";
    }

    private string GenerateInitialPassword()
    {
        var phoneDigits = new string((_editPhone ?? string.Empty).Where(char.IsDigit).ToArray());

        if (_generateRandomPassword)
            return $"Zisk!{Random.Shared.Next(1000, 9999)}";

        var datePart = _editDateOfBirthDate?.ToString("ddMMyyyy") ?? "01011990";
        var phonePart = phoneDigits.Length >= 4 ? phoneDigits[^4..] : phoneDigits.PadLeft(4, '0');
        return $"{datePart}{phonePart}";
    }

    private static string GenerateInitialPassword(AddUserDialog.AddUserDialogModel model)
    {
        var phoneDigits = new string((model.PhoneNumber ?? string.Empty).Where(char.IsDigit).ToArray());

        if (model.GeneratePassword)
            return $"Zisk!{Random.Shared.Next(1000, 9999)}";

        var datePart = model.DateOfBirth?.ToString("ddMMyyyy") ?? "01011990";
        var phonePart = phoneDigits.Length >= 4 ? phoneDigits[^4..] : phoneDigits.PadLeft(4, '0');
        return $"{datePart}{phonePart}";
    }

    private void OnSelectedParentsChanged(IEnumerable<string> parentIds)
    {
        _selectedParentIds = parentIds.ToHashSet();
    }
}
