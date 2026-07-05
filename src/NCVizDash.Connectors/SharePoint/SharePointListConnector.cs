using NCVizDash.Core.Abstractions;
using NCVizDash.Models;

namespace NCVizDash.Connectors.SharePoint;

/// <summary>
/// SharePoint List connector — **not implemented**. SharePoint requires OAuth 2.0
/// (Azure AD app registration, token acquisition/refresh via MSAL) which is a
/// meaningfully larger scope than the other Phase 14 connectors: it needs UI for
/// sign-in, secure token storage, and tenant-admin consent flows that don't fit
/// this pass. This class exists so the connector shape is visible and registered,
/// but every method throws with a clear explanation rather than silently returning
/// empty data, so a caller can't mistake "not implemented" for "no data available".
/// </summary>
public sealed class SharePointListConnector : IDataConnector
{
    /// <inheritdoc/>
    public string ConnectorType => "sharepoint";

    /// <inheritdoc/>
    public Task<IReadOnlyList<DataSourceDescriptor>> DiscoverAsync(string connectionInfo, CancellationToken ct = default) =>
        throw NotImplemented();

    /// <inheritdoc/>
    public Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ReadRowsAsync(
        DataSourceDescriptor descriptor, string connectionInfo, CancellationToken ct = default) =>
        throw NotImplemented();

    private static NotSupportedException NotImplemented() => new(
        "SharePoint List connector requires OAuth 2.0 authentication (Azure AD app " +
        "registration + MSAL token acquisition), which is out of scope for this pass. " +
        "Implement using Microsoft.Graph SDK against the /sites/{site-id}/lists/{list-id}/items " +
        "endpoint once an auth flow exists in the host application.");
}
