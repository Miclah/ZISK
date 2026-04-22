using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ZISK.Data;
using ZISK.Data.Entities;

namespace ZISK.Services;

public class EmailConfirmationCodeService
{
    private readonly ApplicationDbContext _context;

    public EmailConfirmationCodeService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> IssueAsync(ApplicationUser user, CancellationToken ct = default)
    {
        var existing = await _context.EmailConfirmationCodes
            .Where(c => c.UserId == user.Id && c.UsedAt == null)
            .ToListAsync(ct);

        if (existing.Count > 0)
            _context.EmailConfirmationCodes.RemoveRange(existing);

        var code = GenerateNumericCode();
        _context.EmailConfirmationCodes.Add(new EmailConfirmationCode
        {
            UserId = user.Id,
            CodeHash = HashCode(code),
            ExpiresAt = DateTime.UtcNow.AddMinutes(EmailConfirmationCode.LifetimeMinutes),
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(ct);
        return code;
    }

    public async Task<CodeRedemptionResult> RedeemAsync(string userId, string code, CancellationToken ct = default)
    {
        var entry = await _context.EmailConfirmationCodes
            .Where(c => c.UserId == userId && c.UsedAt == null)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (entry is null)
            return CodeRedemptionResult.NotFound;

        if (entry.ExpiresAt < DateTime.UtcNow)
            return CodeRedemptionResult.Expired;

        if (entry.AttemptCount >= EmailConfirmationCode.MaxAttempts)
            return CodeRedemptionResult.TooManyAttempts;

        entry.AttemptCount++;

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(entry.CodeHash),
                Encoding.UTF8.GetBytes(HashCode(code))))
        {
            await _context.SaveChangesAsync(ct);
            return entry.AttemptCount >= EmailConfirmationCode.MaxAttempts
                ? CodeRedemptionResult.TooManyAttempts
                : CodeRedemptionResult.Invalid;
        }

        entry.UsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return CodeRedemptionResult.Success;
    }

    private static string GenerateNumericCode()
    {
        Span<byte> buffer = stackalloc byte[4];
        RandomNumberGenerator.Fill(buffer);
        var value = BitConverter.ToUInt32(buffer) % 1_000_000;
        return value.ToString("D6");
    }

    public static string HashCode(string code)
    {
        var bytes = Encoding.UTF8.GetBytes(code);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}

public enum CodeRedemptionResult
{
    Success,
    Invalid,
    Expired,
    TooManyAttempts,
    NotFound
}
