using ZISK.Shared.Localization;

namespace ZISK.Services;

public class CurrentLanguage : ICurrentLanguage
{
    public Lang Current { get; }

    public CurrentLanguage(IHttpContextAccessor accessor)
    {
        var header = accessor.HttpContext?.Request.Headers.AcceptLanguage.ToString();
        Current = header?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true ? Lang.En : Lang.Sk;
    }
}
