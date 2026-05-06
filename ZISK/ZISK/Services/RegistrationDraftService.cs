using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace ZISK.Services;

public sealed class RegistrationDraft
{
    public const int MaxAttempts = 5;
    public const int LifetimeMinutes = 30;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public string RodneCislo { get; set; } = string.Empty;
    public string Bydlisko { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string Password { get; set; } = string.Empty;
    public bool GdprAccepted { get; set; }
    public bool RulesAccepted { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public int AttemptCount { get; set; }
    public string? ReturnUrl { get; set; }
}

public enum DraftCodeResult
{
    Success,
    Invalid,
    Expired,
    TooManyAttempts,
    NotFound
}

public class RegistrationDraftService
{
    private const string CookieName = "zisk-registration-draft";
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IDataProtector _protector;
    private readonly IHttpContextAccessor _http;

    public RegistrationDraftService(IDataProtectionProvider provider, IHttpContextAccessor http)
    {
        _protector = provider.CreateProtector("ZISK.RegistrationDraft.v1");
        _http = http;
    }

    public string GenerateCode()
    {
        Span<byte> buffer = stackalloc byte[4];
        RandomNumberGenerator.Fill(buffer);
        var value = BitConverter.ToUInt32(buffer) % 1_000_000;
        return value.ToString("D6");
    }

    public static string HashCode(string code)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    public void Save(RegistrationDraft draft)
    {
        var ctx = _http.HttpContext ?? throw new InvalidOperationException("No HttpContext.");
        var json = JsonSerializer.Serialize(draft, JsonOpts);
        var protectedValue = _protector.Protect(json);
        ctx.Response.Cookies.Append(CookieName, protectedValue, new CookieOptions
        {
            HttpOnly = true,
            Secure = ctx.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddMinutes(RegistrationDraft.LifetimeMinutes),
            IsEssential = true,
            Path = "/"
        });
    }

    public RegistrationDraft? Get()
    {
        var ctx = _http.HttpContext;
        if (ctx is null) return null;
        if (!ctx.Request.Cookies.TryGetValue(CookieName, out var protectedValue) || string.IsNullOrEmpty(protectedValue))
            return null;

        try
        {
            var json = _protector.Unprotect(protectedValue);
            var draft = JsonSerializer.Deserialize<RegistrationDraft>(json, JsonOpts);
            if (draft is null) return null;
            if (draft.ExpiresAt < DateTime.UtcNow) return null;
            return draft;
        }
        catch
        {
            return null;
        }
    }

    public void Clear()
    {
        var ctx = _http.HttpContext;
        ctx?.Response.Cookies.Delete(CookieName, new CookieOptions
        {
            Path = "/",
            Secure = ctx.Request.IsHttps,
            SameSite = SameSiteMode.Lax
        });
    }

    public DraftCodeResult ValidateAndConsumeCode(string submittedCode, out RegistrationDraft? draft)
    {
        draft = Get();
        if (draft is null) return DraftCodeResult.NotFound;
        if (draft.ExpiresAt < DateTime.UtcNow) return DraftCodeResult.Expired;
        if (draft.AttemptCount >= RegistrationDraft.MaxAttempts) return DraftCodeResult.TooManyAttempts;

        // FixedTimeEquals runs in constant time regardless of where the bytes differ — prevents timing attacks
        // that could leak information about the correct code by measuring response time.
        var expected = Encoding.UTF8.GetBytes(draft.CodeHash);
        var actual = Encoding.UTF8.GetBytes(HashCode(submittedCode));
        if (expected.Length != actual.Length || !CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            draft.AttemptCount++;
            Save(draft);
            return draft.AttemptCount >= RegistrationDraft.MaxAttempts
                ? DraftCodeResult.TooManyAttempts
                : DraftCodeResult.Invalid;
        }

        return DraftCodeResult.Success;
    }
}
