using Microsoft.AspNetCore.Identity;
using ZISK.Shared.Localization;

namespace ZISK.Services;

public static class IdentityErrorLocalizer
{
    public static string Localize(IdentityError error, Lang lang) => error.Code switch
    {
        "DuplicateUserName" => string.Format(Translations.Get(lang, "identityError.duplicateUserName"), ExtractQuoted(error.Description)),
        "DuplicateEmail" => string.Format(Translations.Get(lang, "identityError.duplicateEmail"), ExtractQuoted(error.Description)),
        "InvalidUserName" => Translations.Get(lang, "identityError.invalidUserName"),
        "InvalidEmail" => Translations.Get(lang, "identityError.invalidEmail"),
        "PasswordTooShort" => string.Format(Translations.Get(lang, "identityError.passwordTooShort"), ExtractNumber(error.Description)),
        "PasswordRequiresNonAlphanumeric" => Translations.Get(lang, "identityError.passwordRequiresNonAlphanumeric"),
        "PasswordRequiresDigit" => Translations.Get(lang, "identityError.passwordRequiresDigit"),
        "PasswordRequiresLower" => Translations.Get(lang, "identityError.passwordRequiresLower"),
        "PasswordRequiresUpper" => Translations.Get(lang, "identityError.passwordRequiresUpper"),
        "PasswordRequiresUniqueChars" => Translations.Get(lang, "identityError.passwordRequiresUniqueChars"),
        "PasswordMismatch" => Translations.Get(lang, "identityError.passwordMismatch"),
        "InvalidToken" => Translations.Get(lang, "identityError.invalidToken"),
        "UserNotInRole" => Translations.Get(lang, "identityError.userNotInRole"),
        "UserAlreadyInRole" => Translations.Get(lang, "identityError.userAlreadyInRole"),
        "UserAlreadyHasPassword" => Translations.Get(lang, "identityError.userAlreadyHasPassword"),
        "UserLockoutNotEnabled" => Translations.Get(lang, "identityError.userLockoutNotEnabled"),
        "UserLockedOut" => Translations.Get(lang, "identityError.userLockedOut"),
        "ConcurrencyFailure" => Translations.Get(lang, "identityError.concurrencyFailure"),
        "DefaultError" => Translations.Get(lang, "identityError.defaultError"),
        _ => error.Description
    };

    public static string LocalizeFirst(IEnumerable<IdentityError> errors, Lang lang)
    {
        var first = errors.FirstOrDefault();
        return first is null ? Translations.Get(lang, "identityError.defaultError") : Localize(first, lang);
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
