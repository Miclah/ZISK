using Microsoft.AspNetCore.Identity;

namespace ZISK.Services;

public static class IdentityErrorLocalizer
{
    public static string Localize(IdentityError error) => error.Code switch
    {
        "DuplicateUserName" => $"Používateľské meno '{ExtractQuoted(error.Description)}' je už obsadené.",
        "DuplicateEmail" => $"Email '{ExtractQuoted(error.Description)}' je už obsadený.",
        "InvalidUserName" => "Používateľské meno obsahuje nepovolené znaky.",
        "InvalidEmail" => "Neplatný formát emailovej adresy.",
        "PasswordTooShort" => $"Heslo musí mať aspoň {ExtractNumber(error.Description)} znakov.",
        "PasswordRequiresNonAlphanumeric" => "Heslo musí obsahovať aspoň jeden špeciálny znak.",
        "PasswordRequiresDigit" => "Heslo musí obsahovať aspoň jednu číslicu.",
        "PasswordRequiresLower" => "Heslo musí obsahovať aspoň jedno malé písmeno.",
        "PasswordRequiresUpper" => "Heslo musí obsahovať aspoň jedno veľké písmeno.",
        "PasswordRequiresUniqueChars" => "Heslo musí obsahovať viac rôznych znakov.",
        "PasswordMismatch" => "Nesprávne heslo.",
        "InvalidToken" => "Neplatný alebo expirovaný token.",
        "UserNotInRole" => "Používateľ nemá požadovanú rolu.",
        "UserAlreadyInRole" => "Používateľ už má túto rolu.",
        "UserAlreadyHasPassword" => "Používateľ už má nastavené heslo.",
        "UserLockoutNotEnabled" => "Blokovanie účtu nie je povolené.",
        "UserLockedOut" => "Účet je dočasne zablokovaný. Skúste to neskôr.",
        "ConcurrencyFailure" => "Záznam bol medzičasom zmenený. Skúste to znova.",
        "DefaultError" => "Nastala neočakávaná chyba.",
        _ => error.Description
    };

    public static string LocalizeFirst(IEnumerable<IdentityError> errors)
    {
        var first = errors.FirstOrDefault();
        return first is null ? "Nastala neočakávaná chyba." : Localize(first);
    }

    private static string ExtractQuoted(string description)
    {
        var start = description.IndexOf('\'');
        var end = description.LastIndexOf('\'');
        return start >= 0 && end > start
            ? description[(start + 1)..end]
            : description;
    }

    private static string ExtractNumber(string description)
    {
        var digits = new string(description.Where(char.IsDigit).ToArray());
        return string.IsNullOrEmpty(digits) ? "?" : digits;
    }
}
