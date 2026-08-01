using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using ZISK.Client.Components;
using ZISK.Client.Display;
using ZISK.Client.Services;
using ZISK.Shared.DTOs.Users;

namespace ZISK.Client.Pages;

public partial class AdminUsers
{
    [Inject] private IUsersApi UsersApi { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private bool _isLoading = true;
    private bool _isSaving;
    private string? _error;

    private List<UserListDto> _users = new();
    private List<ParentOptionDto> _parents = new();

    // Search/role are routed through properties so the filtered list is rebuilt once per input change.
    // Previously FilteredUsers was a computed IEnumerable that the markup enumerated three times
    // (desktop table, mobile .Any(), mobile @foreach), so every keystroke re-ran the whole
    // filter + sort three times over the full roster.
    private string _searchText = string.Empty;
    private string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            ApplyFilter();
        }
    }

    private string? _selectedRole;
    private string? SelectedRole
    {
        get => _selectedRole;
        set
        {
            if (_selectedRole == value) return;
            _selectedRole = value;
            ApplyFilter();
        }
    }

    private bool _editDialogVisible;
    private UserListDto? _selectedUser;

    private EditUserFormModel _editModel = new();
    private HashSet<string> _selectedParentIds = new();

    private string _parentSearch = string.Empty;
    private string ParentSearch
    {
        get => _parentSearch;
        set
        {
            if (_parentSearch == value) return;
            _parentSearch = value;
            ApplyParentFilter();
        }
    }

    private List<UserListDto> _filteredUsers = new();
    private List<ParentOptionDto> _filteredParents = new();

    private void ApplyFilter()
    {
        IEnumerable<UserListDto> query = _users;

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            query = query.Where(u =>
                ($"{u.FirstName} {u.LastName}").Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(u.PhoneNumber) && u.PhoneNumber.Contains(_searchText, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(_selectedRole))
            query = query.Where(u => u.Role == _selectedRole);

        _filteredUsers = query
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToList();
    }

    private void ApplyParentFilter()
    {
        _filteredParents = string.IsNullOrWhiteSpace(_parentSearch)
            ? _parents
            : _parents.Where(p => p.FullName.Contains(_parentSearch, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
        if (NavigationManager.Uri.Contains("action=create", StringComparison.OrdinalIgnoreCase))
            await OpenCreateDialog();
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

            ApplyFilter();
            ApplyParentFilter();
        }
        catch (Exception ex)
        {
            _error = $"Nepodarilo sa načítať používateľov: {ApiErrorFormatter.ToUserMessage(ex)}";
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
                Role = "Parent"
            }
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, CloseButton = true };
        var dialog = await DialogService.ShowAsync<AddUserDialog>("Pridať používateľa", parameters, options);
        var result = await dialog.Result;

        if (result is null || result.Canceled || result.Data is not AddUserDialog.AddUserDialogModel model)
            return;

        _isSaving = true;
        try
        {
            var createEmail = BuildCreateEmail(model);
            var request = model.ToRequest(createEmail);

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
        _selectedUser = user;
        _editModel = new EditUserFormModel
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            Phone = user.PhoneNumber
        };
        _selectedParentIds = new HashSet<string>();
        ParentSearch = string.Empty;
        ApplyParentFilter();

        try
        {
            var detail = await UsersApi.GetUserAsync(user.Id);
            _editModel.Phone = detail.PhoneNumber;
            _editModel.RodneCislo = detail.RodneCislo;
            _editModel.Bydlisko = detail.Bydlisko;
            _editModel.DateOfBirth = detail.DateOfBirth?.ToDateTime(TimeOnly.MinValue);
            _selectedParentIds = detail.Parents.Select(p => p.Id).ToHashSet();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Nepodarilo sa načítať detail používateľa: {ApiErrorFormatter.ToUserMessage(ex)}", Severity.Warning);
        }

        _editDialogVisible = true;
    }

    private void CloseEditDialog()
    {
        _editDialogVisible = false;
    }

    private async Task SaveUser()
    {
        if (_editModel.Role == "Child" && _selectedParentIds.Count == 0)
        {
            Snackbar.Add("Pre dieťa je potrebné vybrať aspoň jedného rodiča.", Severity.Warning);
            return;
        }

        // Business rule: a child can have at most 2 parents. Enforced here on the client; server also validates via EnsureChildParentLinksAsync.
        if (_editModel.Role == "Child" && _selectedParentIds.Count > 2)
        {
            Snackbar.Add("Dieťa môže mať maximálne 2 rodičov.", Severity.Warning);
            return;
        }

        if (_editModel.Role == "Child" && !_editModel.DateOfBirth.HasValue)
        {
            Snackbar.Add("Pre dieťa je dátum narodenia povinný.", Severity.Warning);
            return;
        }

        var dateOfBirth = _editModel.DateOfBirth.HasValue
            ? DateOnly.FromDateTime(_editModel.DateOfBirth.Value)
            : (DateOnly?)null;

        _isSaving = true;
        try
        {
            if (_selectedUser != null)
            {
                var updateRequest = new UpdateUserRequest(
                    _editModel.FirstName,
                    _editModel.LastName,
                    _editModel.Role,
                    null,
                    null,
                    string.IsNullOrWhiteSpace(_editModel.Phone) ? null : _editModel.Phone,
                    string.IsNullOrWhiteSpace(_editModel.RodneCislo) ? null : _editModel.RodneCislo,
                    string.IsNullOrWhiteSpace(_editModel.Bydlisko) ? null : _editModel.Bydlisko,
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
            Snackbar.Add($"Chyba pri ukladaní: {ApiErrorFormatter.ToUserMessage(ex)}", Severity.Error);
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
            Snackbar.Add($"Chyba pri mazaní: {ApiErrorFormatter.ToUserMessage(ex)}", Severity.Error);
        }
    }




    private static string GetUserInitials(UserListDto user)
    {
        var first = string.IsNullOrWhiteSpace(user.FirstName) ? "?" : user.FirstName.Trim()[0].ToString().ToUpperInvariant();
        var last = string.IsNullOrWhiteSpace(user.LastName) ? string.Empty : user.LastName.Trim()[0].ToString().ToUpperInvariant();
        return $"{first}{last}";
    }

    private static string BuildCreateEmail(AddUserDialog.AddUserDialogModel model)
    {
        if (!string.IsNullOrWhiteSpace(model.Email))
            return model.Email.Trim();

        var phoneDigits = new string((model.PhoneNumber ?? string.Empty).Where(char.IsDigit).ToArray());
        var suffix = string.IsNullOrWhiteSpace(phoneDigits) ? Guid.NewGuid().ToString("N")[..8] : phoneDigits;
        return $"{suffix}@zisk.local";
    }

    // Zatial pre testovanie vypnute - heslo sa zadava priamo v dialogu
    // private static string GenerateInitialPassword(AddUserDialog.AddUserDialogModel model)
    // {
    //     var phoneDigits = new string((model.PhoneNumber ?? string.Empty).Where(char.IsDigit).ToArray());
    //
    //     if (model.GeneratePassword)
    //         return $"Zisk!{Random.Shared.Next(1000, 9999)}";
    //
    //     var datePart = model.DateOfBirth?.ToString("ddMMyyyy") ?? "01011990";
    //     var phonePart = phoneDigits.Length >= 4 ? phoneDigits[^4..] : phoneDigits.PadLeft(4, '0');
    //     return $"{datePart}{phonePart}";
    // }

    private void OnSelectedParentsChanged(IEnumerable<string> parentIds)
    {
        _selectedParentIds = parentIds.ToHashSet();
    }

    private async Task ConfirmUpgradeToAthlete(UserListDto user)
    {
        bool? result = await DialogService.ShowMessageBoxAsync(
            "Povýšiť na športovca",
            $"Naozaj chcete povýšiť '{user.FirstName} {user.LastName}' z roly Dieťa na Športovca?",
            yesText: "Áno, povýšiť",
            cancelText: "Zrušiť",
            options: new DialogOptions { MaxWidth = MaxWidth.Small });

        if (result != true)
            return;

        try
        {
            await UsersApi.UpgradeToAthleteAsync(user.Id);
            Snackbar.Add("Používateľ bol povýšený na Športovca.", Severity.Success);
            await LoadData();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Chyba: {ApiErrorFormatter.ToUserMessage(ex)}", Severity.Error);
        }
    }

    public class EditUserFormModel
    {
        [Required(ErrorMessage = "Meno je povinné.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Meno musí mať 2 – 100 znakov.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Priezvisko je povinné.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Priezvisko musí mať 2 – 100 znakov.")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Rola je povinná.")]
        public string Role { get; set; } = "Parent";

        [Phone(ErrorMessage = "Neplatný formát telefónneho čísla.")]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "Rodné číslo je povinné.")]
        [RegularExpression(@"^\d{6}[/]?\d{3,4}$", ErrorMessage = "Formát: 991231/1234")]
        public string? RodneCislo { get; set; }

        [StringLength(300, ErrorMessage = "Bydlisko môže mať max 300 znakov.")]
        public string? Bydlisko { get; set; }

        public DateTime? DateOfBirth { get; set; }
    }
}
