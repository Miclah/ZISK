using ZISK.Shared.Localization;

namespace ZISK.Services;

/// <summary>
/// Resolves the language the current API request should be answered in, from the
/// Accept-Language header the client's AcceptLanguageHandler stamps on every call. Used to
/// translate DataAnnotations validation-key messages (see Program.cs's
/// InvalidModelStateResponseFactory) and Services-layer business-rule error messages. Requests
/// with no header (e.g. server-side Identity pages, which stay Slovak-only by design) default
/// to Sk.
/// </summary>
public interface ICurrentLanguage
{
    Lang Current { get; }
}
