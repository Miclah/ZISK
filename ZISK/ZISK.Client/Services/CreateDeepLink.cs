using Microsoft.AspNetCore.Components;

namespace ZISK.Client.Services;

/// <summary>
/// The <c>?action=create</c> convention the admin dashboard's quick actions use to land on a page
/// with its create dialog already open.
/// </summary>
public static class CreateDeepLink
{
    private const string Marker = "action=create";

    /// <summary>
    /// Returns true when the current URL asks for the create dialog, and strips the query on the way
    /// out so it is only ever honoured once. Without the strip the marker stays in the address bar,
    /// and every later re-initialisation of the page (a refresh, or a re-render that rebuilds it)
    /// opens the dialog again.
    /// </summary>
    public static bool Consume(NavigationManager navigation)
    {
        var uri = navigation.Uri;
        if (!uri.Contains(Marker, StringComparison.OrdinalIgnoreCase))
            return false;

        // replace: true so the marked URL does not become a back-button destination that reopens
        // the dialog.
        navigation.NavigateTo(uri.Split('?')[0], forceLoad: false, replace: true);
        return true;
    }
}
