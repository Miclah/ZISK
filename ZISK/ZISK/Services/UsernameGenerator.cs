using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using ZISK.Data;

namespace ZISK.Services
{
    public class UsernameGenerator(UserManager<ApplicationUser> userManager)
    {
        public async Task<string> GenerateAsync(string firstName, string lastName)
        {
            var baseUsername = BuildBase(firstName, lastName);
            var candidate = baseUsername;
            var counter = 2;

            while (await userManager.FindByNameAsync(candidate) is not null)
            {
                candidate = $"{baseUsername}.{counter}";
                counter++;
            }

            return candidate;
        }

        public static string BuildBase(string firstName, string lastName)
        {
            return $"{Normalize(firstName)}.{Normalize(lastName)}";
        }

        private static string Normalize(string input)
        {
            var decomposed = input.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            var result = Regex.Replace(sb.ToString().ToLowerInvariant(), @"[^a-z0-9]", "");
            return string.IsNullOrEmpty(result) ? "user" : result;
        }
    }
}
