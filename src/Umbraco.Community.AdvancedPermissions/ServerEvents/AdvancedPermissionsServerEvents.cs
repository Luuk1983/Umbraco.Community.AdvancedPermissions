namespace Umbraco.Community.AdvancedPermissions.ServerEvents;

/// <summary>
/// The event-source and event-type strings this package publishes on Umbraco's built-in
/// server-events (SignalR) hub.
/// </summary>
/// <remarks>
/// <para>
/// Named in one place because the hub routes to a SignalR group named by the exact source string:
/// a source published under one spelling and authorized under another silently delivers nothing,
/// and nothing about that failure looks like a failure.
/// </para>
/// <para>
/// The events carry no payload beyond a single key — <c>ServerEvent</c> has no room for one — so
/// they are hints that something moved, never statements of what it moved to. Every consumer
/// refetches.
/// </para>
/// </remarks>
public static class AdvancedPermissionsServerEvents
{
    /// <summary>Permission entries for a content node changed. The key is the node key.</summary>
    public const string NodePermissionsSource = "AdvancedPermissions:NodePermissions";

    /// <summary>Document-type create permissions changed. The key is the content node key.</summary>
    public const string DocTypePermissionsSource = "AdvancedPermissions:DocTypePermissions";

    /// <summary>
    /// Something changed that shifts effective permissions without this package's store being
    /// touched: user group membership, a group's existence, or a node's position in the tree. The
    /// key is whichever entity Umbraco's own notification named.
    /// </summary>
    public const string AccessSource = "AdvancedPermissions:Access";

    /// <summary>Every source this package publishes, for the authorizer to claim.</summary>
    public static readonly IReadOnlyList<string> AllSources =
        [NodePermissionsSource, DocTypePermissionsSource, AccessSource];

    /// <summary>The event-type strings carried on a routed event.</summary>
    public static class EventType
    {
        /// <summary>The subject changed and should be re-read.</summary>
        public const string Updated = "Updated";
    }
}
