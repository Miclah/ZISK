using System.ComponentModel.DataAnnotations;

namespace ZISK.Tests;

public class AuthPasswordValidationTests
{
    private static List<ValidationResult> Validate(object model)
    {
        var ctx = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, ctx, results, validateAllProperties: true);
        return results;
    }

    // ─── ResetPassword InputModel ────────────────────────────────────────────

    private sealed class ResetPasswordInput
    {
        [Required(ErrorMessage = "Heslo je povinné")]
        [StringLength(100, ErrorMessage = "Heslo musí mať aspoň {2} a najviac {1} znakov.", MinimumLength = 8)]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$",
            ErrorMessage = "Heslo musí obsahovať malé písmeno, veľké písmeno a číslicu.")]
        public string Password { get; set; } = "";

        [Compare("Password", ErrorMessage = "Heslá sa nezhodujú.")]
        public string ConfirmPassword { get; set; } = "";
    }

    [Theory]
    [InlineData("Short1")]          // too short (6 chars)
    [InlineData("alllowercase1")]   // no uppercase
    [InlineData("ALLUPPERCASE1")]   // no lowercase
    [InlineData("NoDigitsHere")]    // no digit
    [InlineData("")]                // empty
    public void ResetPassword_InvalidPasswords_FailValidation(string password)
    {
        var model = new ResetPasswordInput { Password = password, ConfirmPassword = password };
        var errors = Validate(model);
        Assert.NotEmpty(errors);
    }

    [Theory]
    [InlineData("ValidPass1")]
    [InlineData("Correct8!")]
    [InlineData("LongPassword99")]
    public void ResetPassword_ValidPasswords_PassValidation(string password)
    {
        var model = new ResetPasswordInput { Password = password, ConfirmPassword = password };
        var errors = Validate(model);
        Assert.Empty(errors);
    }

    [Fact]
    public void ResetPassword_MismatchedConfirm_FailsValidation()
    {
        var model = new ResetPasswordInput { Password = "ValidPass1", ConfirmPassword = "Different1" };
        var errors = Validate(model);
        Assert.Contains(errors, e => e.ErrorMessage!.Contains("nezhodujú"));
    }

    [Fact]
    public void ResetPassword_MinimumLength_Is8()
    {
        var short7 = new ResetPasswordInput { Password = "Short1A", ConfirmPassword = "Short1A" };
        var exact8  = new ResetPasswordInput { Password = "Correct1", ConfirmPassword = "Correct1" };

        Assert.NotEmpty(Validate(short7));
        Assert.Empty(Validate(exact8));
    }

    // ─── ChangePassword InputModel ───────────────────────────────────────────

    private sealed class ChangePasswordInput
    {
        [Required(ErrorMessage = "Staré heslo je povinné")]
        public string OldPassword { get; set; } = "";

        [Required(ErrorMessage = "Nové heslo je povinné")]
        [StringLength(100, ErrorMessage = "Heslo musí mať aspoň {2} a najviac {1} znakov.", MinimumLength = 8)]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$",
            ErrorMessage = "Heslo musí obsahovať malé písmeno, veľké písmeno a číslicu.")]
        public string NewPassword { get; set; } = "";

        [Compare("NewPassword", ErrorMessage = "Heslá sa nezhodujú.")]
        public string ConfirmPassword { get; set; } = "";
    }

    [Fact]
    public void ChangePassword_EmptyOldPassword_FailsValidation()
    {
        var model = new ChangePasswordInput { OldPassword = "", NewPassword = "ValidNew1", ConfirmPassword = "ValidNew1" };
        var errors = Validate(model);
        Assert.Contains(errors, e => e.ErrorMessage!.Contains("povinné"));
    }

    [Fact]
    public void ChangePassword_WeakNewPassword_FailsValidation()
    {
        var model = new ChangePasswordInput { OldPassword = "oldPwd", NewPassword = "weakpwd", ConfirmPassword = "weakpwd" };
        var errors = Validate(model);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void ChangePassword_StrongNewPassword_PassesValidation()
    {
        var model = new ChangePasswordInput { OldPassword = "oldPwd", NewPassword = "StrongNew1", ConfirmPassword = "StrongNew1" };
        var errors = Validate(model);
        Assert.Empty(errors);
    }
}
