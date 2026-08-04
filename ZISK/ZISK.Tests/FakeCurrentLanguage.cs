using ZISK.Services;
using ZISK.Shared.Localization;

namespace ZISK.Tests;

/// <summary>Shared no-op ICurrentLanguage double - every test exercises the Slovak error text, matching what the DTOs/Services carried before Krok 6C introduced translation keys.</summary>
internal sealed class FakeCurrentLanguage : ICurrentLanguage
{
    public Lang Current => Lang.Sk;
}
