using ZISK.Shared.Localization;

namespace ZISK.Services;

public class CurrentLanguage : ICurrentLanguage
{
    public Lang Current { get; }

    public CurrentLanguage(IHttpContextAccessor accessor)
    {
        // AcceptLanguageHandler stamps this header on every client call, so "sk" here means the
        // visitor actually chose Slovak. Anything else, including a missing header, gets English:
        // the default has to match the client's, or a validation message would come back in one
        // language while the page around it is in the other.
        var header = accessor.HttpContext?.Request.Headers.AcceptLanguage.ToString();
        Current = header?.StartsWith("sk", StringComparison.OrdinalIgnoreCase) == true ? Lang.Sk : Lang.En;
    }
}
