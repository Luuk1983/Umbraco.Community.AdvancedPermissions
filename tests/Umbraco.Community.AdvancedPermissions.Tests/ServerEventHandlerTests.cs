using Microsoft.Extensions.Logging;
using NSubstitute;
using Umbraco.Cms.Core.Models.ServerEvents;
using Umbraco.Cms.Core.ServerEvents;
using Umbraco.Community.AdvancedPermissions.Notifications;
using Umbraco.Community.AdvancedPermissions.ServerEvents;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests that this package's own change notifications reach the server-event hub under the right
/// source and key, and that a failure to route one can never propagate back into the write that
/// raised it.
/// </summary>
public sealed class ServerEventHandlerTests
{
    /// <summary>The router the handler under test routes through.</summary>
    private readonly IServerEventRouter _router = Substitute.For<IServerEventRouter>();

    /// <summary>The logger the handler under test records a swallowed failure to.</summary>
    private readonly ILogger<AdvancedPermissionsServerEventHandler> _logger = Substitute.For<ILogger<AdvancedPermissionsServerEventHandler>>();

    /// <summary>Builds the handler under test.</summary>
    /// <returns>A handler wired to the substitute router and logger.</returns>
    private AdvancedPermissionsServerEventHandler CreateHandler() => new(_router, _logger);

    /// <summary>Gets whether the handler logged an error.</summary>
    /// <returns><see langword="true"/> if an error-level log entry was written.</returns>
    private bool LoggedAnError() =>
        _logger.ReceivedCalls().Any(c =>
            c.GetMethodInfo().Name == nameof(ILogger.Log) && c.GetArguments()[0] is LogLevel.Error);

    /// <summary>Asserts that exactly one event was routed, under the given source and key.</summary>
    /// <param name="source">The expected source.</param>
    /// <param name="key">The expected key.</param>
    private async Task AssertRoutedOnceAsync(string source, Guid key)
    {
        await _router.Received(1).RouteEventAsync(Arg.Is<ServerEvent>(e =>
            e.EventSource == source &&
            e.EventType == AdvancedPermissionsServerEvents.EventType.Updated &&
            e.Key == key));
        await _router.Received(1).RouteEventAsync(Arg.Any<ServerEvent>());
    }

    /// <summary>A content permission change is routed under the node-permissions source, keyed by node.</summary>
    [Fact]
    public async Task PermissionChanged_RoutesNodePermissionsEvent()
    {
        var nodeKey = Guid.Parse("77777777-7777-7777-7777-777777777777");

        await CreateHandler().HandleAsync(
            new AdvancedPermissionsChangedNotification(nodeKey, "editors", ["Umb.Document.Read"]),
            CancellationToken.None);

        await AssertRoutedOnceAsync(AdvancedPermissionsServerEvents.NodePermissionsSource, nodeKey);
    }

    /// <summary>
    /// A library element permission change is routed under its own source, not the content one, so
    /// the content editors are not woken by a library save.
    /// </summary>
    [Fact]
    public async Task ElementPermissionChanged_RoutesElementPermissionsEvent()
    {
        var nodeKey = Guid.Parse("66666666-6666-6666-6666-666666666666");

        await CreateHandler().HandleAsync(
            new ElementPermissionsChangedNotification(nodeKey, "editors", ["Umb.Element.Read"]),
            CancellationToken.None);

        await AssertRoutedOnceAsync(AdvancedPermissionsServerEvents.ElementPermissionsSource, nodeKey);
    }

    /// <summary>
    /// A change to a document type is routed under the document-type source only. The element-type
    /// editor must not wake for it.
    /// </summary>
    [Fact]
    public async Task DocTypePermissionChanged_DocumentFamily_RoutesOnlyTheDocTypeSource()
    {
        var nodeKey = Guid.Parse("88888888-8888-8888-8888-888888888888");

        await CreateHandler().HandleAsync(
            new DocTypePermissionsChangedNotification(nodeKey, "editors", Guid.NewGuid(), DocTypePermissionFamily.Document),
            CancellationToken.None);

        await AssertRoutedOnceAsync(AdvancedPermissionsServerEvents.DocTypePermissionsSource, nodeKey);
    }

    /// <summary>
    /// A change to an element type is routed under the element-type source only. The document-type
    /// editor must not wake for it.
    /// </summary>
    [Fact]
    public async Task DocTypePermissionChanged_ElementFamily_RoutesOnlyTheElementTypeSource()
    {
        var nodeKey = Guid.Parse("99999999-9999-9999-9999-999999999999");

        await CreateHandler().HandleAsync(
            new DocTypePermissionsChangedNotification(nodeKey, "editors", Guid.NewGuid(), DocTypePermissionFamily.Element),
            CancellationToken.None);

        await AssertRoutedOnceAsync(AdvancedPermissionsServerEvents.ElementTypePermissionsSource, nodeKey);
    }

    /// <summary>
    /// When the family could not be determined the event goes to both sources. A spurious wake costs
    /// one refetch that reconciles to no change; a missed wake is a screen that stays silently stale.
    /// </summary>
    [Fact]
    public async Task DocTypePermissionChanged_UnknownFamily_RoutesToBothSources()
    {
        var nodeKey = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        await CreateHandler().HandleAsync(
            new DocTypePermissionsChangedNotification(nodeKey, "editors", Guid.NewGuid(), DocTypePermissionFamily.Unknown),
            CancellationToken.None);

        await _router.Received(1).RouteEventAsync(Arg.Is<ServerEvent>(e =>
            e.EventSource == AdvancedPermissionsServerEvents.DocTypePermissionsSource && e.Key == nodeKey));
        await _router.Received(1).RouteEventAsync(Arg.Is<ServerEvent>(e =>
            e.EventSource == AdvancedPermissionsServerEvents.ElementTypePermissionsSource && e.Key == nodeKey));
        await _router.Received(2).RouteEventAsync(Arg.Any<ServerEvent>());
    }

    /// <summary>
    /// The default value of the family enum is the "wake both" one, so a notification built without
    /// setting a family can never silently wake nobody.
    /// </summary>
    [Fact]
    public void DocTypePermissionFamily_DefaultValue_IsUnknown() =>
        Assert.Equal(DocTypePermissionFamily.Unknown, default(DocTypePermissionFamily));

    /// <summary>
    /// If the router fails for one of the two sources an unknown-family change goes to, the other still
    /// goes out: a failure of one must not cost the other its wake.
    /// </summary>
    [Fact]
    public async Task DocTypePermissionChanged_UnknownFamily_OneSourceFails_OtherStillRouted()
    {
        var nodeKey = Guid.NewGuid();
        _router
            .RouteEventAsync(Arg.Is<ServerEvent>(e => e.EventSource == AdvancedPermissionsServerEvents.DocTypePermissionsSource))
            .Returns(Task.FromException(new InvalidOperationException("Simulated hub failure.")));

        await CreateHandler().HandleAsync(
            new DocTypePermissionsChangedNotification(nodeKey, "editors", Guid.NewGuid(), DocTypePermissionFamily.Unknown),
            CancellationToken.None);

        await _router.Received(1).RouteEventAsync(Arg.Is<ServerEvent>(e =>
            e.EventSource == AdvancedPermissionsServerEvents.ElementTypePermissionsSource && e.Key == nodeKey));
    }

    /// <summary>
    /// The router throwing must not propagate out of the handler: these notifications are published by
    /// the permission services after their write has committed, and an escaping exception would report
    /// a false failure for a save that succeeded. It is logged instead.
    /// </summary>
    /// <param name="notification">The notification to handle, of each kind the handler accepts.</param>
    [Theory]
    [MemberData(nameof(OwnNotifications))]
    public async Task RouterThrows_DoesNotPropagate_AndIsLogged(object notification)
    {
        _router
            .RouteEventAsync(Arg.Any<ServerEvent>())
            .Returns(Task.FromException(new InvalidOperationException("Simulated hub failure.")));

        await HandleAsync(CreateHandler(), notification, CancellationToken.None);

        Assert.True(LoggedAnError());
    }

    /// <summary>
    /// A cancelled request is not a handler failure and must not be swallowed: hiding a genuine
    /// cancellation would leave the caller running on.
    /// </summary>
    /// <param name="notification">The notification to handle, of each kind the handler accepts.</param>
    [Theory]
    [MemberData(nameof(OwnNotifications))]
    public async Task RouterCancelled_RethrowsOperationCanceled(object notification)
    {
        _router
            .RouteEventAsync(Arg.Any<ServerEvent>())
            .Returns(Task.FromException(new OperationCanceledException()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            HandleAsync(CreateHandler(), notification, CancellationToken.None));
    }

    /// <summary>
    /// The two sources an unknown-family change goes to are routed concurrently, not one after the
    /// other: the handler runs inside the write's scope, so every serial hop extends how long it stays
    /// open. The first route is held open, and the second must start regardless.
    /// </summary>
    [Fact]
    public async Task DocTypePermissionChanged_UnknownFamily_RoutesConcurrently()
    {
        var gate = new TaskCompletionSource();
        _router
            .RouteEventAsync(Arg.Is<ServerEvent>(e => e.EventSource == AdvancedPermissionsServerEvents.DocTypePermissionsSource))
            .Returns(gate.Task);

        var handling = CreateHandler().HandleAsync(
            new DocTypePermissionsChangedNotification(Guid.NewGuid(), "editors", Guid.NewGuid(), DocTypePermissionFamily.Unknown),
            CancellationToken.None);

        // The second route has been issued although the first has not completed.
        await _router.Received(1).RouteEventAsync(Arg.Is<ServerEvent>(e =>
            e.EventSource == AdvancedPermissionsServerEvents.ElementTypePermissionsSource));
        Assert.False(handling.IsCompleted);

        gate.SetResult();
        await handling;
    }

    /// <summary>One notification of each kind this handler accepts, for the theories above.</summary>
    /// <returns>The test cases.</returns>
    public static TheoryData<object> OwnNotifications() =>
    [
        new AdvancedPermissionsChangedNotification(Guid.NewGuid(), "editors", []),
        new ElementPermissionsChangedNotification(Guid.NewGuid(), "editors", []),
        new DocTypePermissionsChangedNotification(Guid.NewGuid(), "editors", Guid.NewGuid(), DocTypePermissionFamily.Document),
        new DocTypePermissionsChangedNotification(Guid.NewGuid(), "editors", Guid.NewGuid(), DocTypePermissionFamily.Element),
        new DocTypePermissionsChangedNotification(Guid.NewGuid(), "editors", Guid.NewGuid(), DocTypePermissionFamily.Unknown),
    ];

    /// <summary>Dispatches a notification to the handler method that accepts its type.</summary>
    /// <param name="handler">The handler.</param>
    /// <param name="notification">The notification.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The handler's task.</returns>
    private static Task HandleAsync(AdvancedPermissionsServerEventHandler handler, object notification, CancellationToken cancellationToken) =>
        notification switch
        {
            AdvancedPermissionsChangedNotification n => handler.HandleAsync(n, cancellationToken),
            ElementPermissionsChangedNotification n => handler.HandleAsync(n, cancellationToken),
            DocTypePermissionsChangedNotification n => handler.HandleAsync(n, cancellationToken),
            _ => throw new ArgumentException($"Unexpected notification {notification.GetType().Name}.", nameof(notification)),
        };
}
