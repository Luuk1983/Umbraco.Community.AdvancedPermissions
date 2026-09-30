using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Models.ServerEvents;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.ServerEvents;
using Umbraco.Community.AdvancedPermissions.Composing;
using Umbraco.Community.AdvancedPermissions.Notifications;
using Umbraco.Community.AdvancedPermissions.ServerEvents;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests how the composer registers the server-event handlers, and that the sources those handlers
/// publish are all claimed by the authorizer.
/// </summary>
/// <remarks>
/// <para>
/// Both are silent-failure surfaces. Umbraco runs the handlers of one notification in registration
/// order, so a server-event handler registered ahead of a cache invalidator raises its event before the
/// cache has been told: a client that refetches on it reads the very snapshot the change replaced, and
/// - having consumed its one notification - never asks again. And a source no authorizer claims is
/// never delivered at all. Neither produces an error anywhere, so neither can be left to inspection.
/// </para>
/// <para>
/// The composer's <c>RegisterNotificationHandlers</c> is private, so it is invoked by reflection over a
/// substitute builder that exposes a real service collection. Handler registrations are transient
/// descriptors, and their order in that collection is the order Umbraco resolves them in.
/// </para>
/// </remarks>
public sealed class ServerEventRegistrationTests
{
    /// <summary>The two handler types whose registrations must come last.</summary>
    private static readonly Type[] ServerEventHandlerTypes =
        [typeof(AdvancedPermissionsServerEventHandler), typeof(AccessServerEventHandler)];

    /// <summary>
    /// Runs the composer's notification-handler registration against a fresh service collection.
    /// </summary>
    /// <returns>The descriptors it registered, in registration order.</returns>
    private static IReadOnlyList<ServiceDescriptor> RegisteredHandlers()
    {
        var services = new ServiceCollection();
        var builder = Substitute.For<IUmbracoBuilder>();
        builder.Services.Returns(services);

        var method = typeof(AdvancedPermissionsComposer).GetMethod(
            "RegisterNotificationHandlers",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        method.Invoke(null, [builder]);

        return services.Where(d => NotificationTypeOf(d.ServiceType) is not null).ToList();
    }

    /// <summary>
    /// Gets the notification type a handler service type (sync or async) is registered for.
    /// </summary>
    /// <param name="serviceType">The registered service type.</param>
    /// <returns>The notification type, or <see langword="null"/> if it is not a notification handler.</returns>
    private static Type? NotificationTypeOf(Type serviceType) =>
        serviceType.IsGenericType
        && (serviceType.GetGenericTypeDefinition() == typeof(INotificationHandler<>)
            || serviceType.GetGenericTypeDefinition() == typeof(INotificationAsyncHandler<>))
            ? serviceType.GetGenericArguments()[0]
            : null;

    /// <summary>
    /// Every server-event registration comes after every other handler registration. This is the whole
    /// ordering rule stated once: it covers each notification that a cache invalidator, a cleanup or the
    /// user-group seeder also handles, and any handler added later, without naming them.
    /// </summary>
    [Fact]
    public void ServerEventHandlers_AreRegisteredAfterEveryOtherHandler()
    {
        var registrations = RegisteredHandlers();
        var firstServerEvent = IndexOfFirst(registrations, isServerEvent: true);
        var lastOther = IndexOfLast(registrations, isServerEvent: false);

        Assert.True(firstServerEvent >= 0, "No server-event handler is registered at all.");
        Assert.True(lastOther >= 0, "No other handler is registered, so the ordering is untested.");
        Assert.True(
            firstServerEvent > lastOther,
            $"A server-event handler is registered at position {firstServerEvent}, ahead of another handler at " +
            $"position {lastOther} ({registrations[lastOther].ImplementationType?.Name}). It would raise its event before " +
            "that handler has run, so a client refetching on it could read a stale cache and never ask again.");
    }

    /// <summary>
    /// For each notification a server-event handler subscribes to, its registration comes after every
    /// other handler of that same notification. The general rule above implies this; it is stated per
    /// notification as well so a failure names the notification, and so the test cannot pass vacuously
    /// for the Umbraco-raised notifications that really do have an invalidator to overtake.
    /// </summary>
    [Fact]
    public void EveryNotificationTheServerEventHandlersSubscribeTo_HasThemRegisteredAfterItsOtherHandlers()
    {
        var registrations = RegisteredHandlers();
        var subscribed = registrations
            .Where(d => IsServerEvent(d))
            .Select(d => NotificationTypeOf(d.ServiceType)!)
            .Distinct()
            .ToList();
        var overtakeable = 0;

        foreach (var notification in subscribed)
        {
            var forThis = registrations.Where(d => NotificationTypeOf(d.ServiceType) == notification).ToList();
            var serverEventPositions = Enumerable.Range(0, forThis.Count).Where(i => IsServerEvent(forThis[i])).ToList();
            var otherPositions = Enumerable.Range(0, forThis.Count).Where(i => !IsServerEvent(forThis[i])).ToList();

            if (otherPositions.Count > 0)
            {
                overtakeable++;
                Assert.True(
                    serverEventPositions.Min() > otherPositions.Max(),
                    $"{notification.Name}: a server-event handler is registered before another handler of the same notification.");
            }
        }

        Assert.True(overtakeable >= 5, "Too few notifications have another handler to overtake - the registration list has changed shape.");
    }

    /// <summary>
    /// Every notification a server-event handler implements an interface for is actually registered. A
    /// handler that implements <c>INotificationAsyncHandler&lt;T&gt;</c> but is never registered for it
    /// never runs, and its screen silently never updates.
    /// </summary>
    [Fact]
    public void EveryNotificationTheHandlersImplement_IsRegistered()
    {
        var registrations = RegisteredHandlers();

        foreach (var handlerType in ServerEventHandlerTypes)
        {
            var implemented = handlerType.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INotificationAsyncHandler<>))
                .Select(i => i.GetGenericArguments()[0])
                .ToList();
            var registered = registrations
                .Where(d => d.ImplementationType == handlerType)
                .Select(d => NotificationTypeOf(d.ServiceType)!)
                .ToList();

            Assert.NotEmpty(implemented);
            Assert.Equal(
                implemented.Select(t => t.FullName).OrderBy(n => n, StringComparer.Ordinal),
                registered.Select(t => t.FullName).OrderBy(n => n, StringComparer.Ordinal));
        }
    }

    /// <summary>
    /// The ordering rule has teeth: a registration list with a server-event handler ahead of another
    /// handler is recognised as violating it. Without this the tests above could never fail, and would
    /// only look like protection.
    /// </summary>
    [Fact]
    public void OrderingCheck_CanFail_WhenAServerEventHandlerIsRegisteredEarly()
    {
        var registrations = RegisteredHandlers().ToList();
        var serverEvent = registrations.First(IsServerEvent);
        registrations.Remove(serverEvent);
        registrations.Insert(0, serverEvent);

        var firstServerEvent = IndexOfFirst(registrations, isServerEvent: true);
        var lastOther = IndexOfLast(registrations, isServerEvent: false);

        Assert.True(firstServerEvent < lastOther);
    }

    /// <summary>Gets whether a descriptor registers one of the server-event handlers.</summary>
    /// <param name="descriptor">The descriptor.</param>
    /// <returns><see langword="true"/> for a server-event handler.</returns>
    private static bool IsServerEvent(ServiceDescriptor descriptor) =>
        descriptor.ImplementationType is { } type && ServerEventHandlerTypes.Contains(type);

    /// <summary>Finds the first descriptor of the given kind.</summary>
    /// <param name="registrations">The registrations, in order.</param>
    /// <param name="isServerEvent">Whether to look for a server-event handler or any other.</param>
    /// <returns>The index, or -1.</returns>
    private static int IndexOfFirst(IReadOnlyList<ServiceDescriptor> registrations, bool isServerEvent)
    {
        for (var i = 0; i < registrations.Count; i++)
        {
            if (IsServerEvent(registrations[i]) == isServerEvent)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Finds the last descriptor of the given kind.</summary>
    /// <param name="registrations">The registrations, in order.</param>
    /// <param name="isServerEvent">Whether to look for a server-event handler or any other.</param>
    /// <returns>The index, or -1.</returns>
    private static int IndexOfLast(IReadOnlyList<ServiceDescriptor> registrations, bool isServerEvent)
    {
        for (var i = registrations.Count - 1; i >= 0; i--)
        {
            if (IsServerEvent(registrations[i]) == isServerEvent)
            {
                return i;
            }
        }

        return -1;
    }
}

/// <summary>
/// Tests that every source the server-event handlers actually route is one the authorizer claims.
/// </summary>
/// <remarks>
/// The authorizer's own tests compare its list with the declared constants. This is the other half:
/// it drives every notification through the real handlers and checks the sources that come out the
/// other end, so a handler that routes to a source nobody declared or claimed - a typo, a new family
/// added to a handler and nowhere else - fails here rather than delivering to nobody.
/// </remarks>
public sealed class ServerEventSourceCoverageTests
{
    /// <summary>
    /// Drives one notification of every kind the handlers accept through them and returns the sources
    /// they routed to.
    /// </summary>
    /// <returns>The distinct routed sources.</returns>
    private static async Task<IReadOnlyList<string>> RoutedSourcesAsync()
    {
        var routed = new List<string>();
        var router = Substitute.For<IServerEventRouter>();
        router
            .RouteEventAsync(Arg.Do<ServerEvent>(e =>
            {
                lock (routed)
                {
                    routed.Add(e.EventSource);
                }
            }))
            .Returns(Task.CompletedTask);

        var own = new AdvancedPermissionsServerEventHandler(router, Substitute.For<ILogger<AdvancedPermissionsServerEventHandler>>());
        var access = new AccessServerEventHandler(router, Substitute.For<ILogger<AccessServerEventHandler>>());
        var ct = CancellationToken.None;
        var messages = new EventMessages();
        var key = Guid.NewGuid();

        await own.HandleAsync(new AdvancedPermissionsChangedNotification(key, "editors", []), ct);
        await own.HandleAsync(new ElementPermissionsChangedNotification(key, "editors", []), ct);
        await own.HandleAsync(new DocTypePermissionsChangedNotification(key, "editors", key, DocTypePermissionFamily.Document), ct);
        await own.HandleAsync(new DocTypePermissionsChangedNotification(key, "editors", key, DocTypePermissionFamily.Element), ct);
        await own.HandleAsync(new DocTypePermissionsChangedNotification(key, "editors", key, DocTypePermissionFamily.Unknown), ct);

        await access.HandleAsync(new UserGroupSavedNotification(WithKey<IUserGroup>(key), messages), ct);
        await access.HandleAsync(new UserGroupDeletedNotification(WithKey<IUserGroup>(key), messages), ct);
        await access.HandleAsync(new UserSavedNotification(WithKey<IUser>(key), messages), ct);
        await access.HandleAsync(new ContentMovedNotification(new MoveEventInfo<IContent>(WithKey<IContent>(key), "-1,-20", -20), messages), ct);
        await access.HandleAsync(new ContentMovedToRecycleBinNotification(new MoveToRecycleBinEventInfo<IContent>(WithKey<IContent>(key), "-1,-20"), messages), ct);
        await access.HandleAsync(new ContentDeletedNotification(WithKey<IContent>(key), messages), ct);
        await access.HandleAsync(new ElementMovedNotification(new MoveEventInfo<IElement>(WithKey<IElement>(key), "-1,-20", -20), messages), ct);
        await access.HandleAsync(new ElementMovedToRecycleBinNotification(new MoveToRecycleBinEventInfo<IElement>(WithKey<IElement>(key), "-1,-20"), messages), ct);
        await access.HandleAsync(new ElementDeletedNotification(WithKey<IElement>(key), messages), ct);
        await access.HandleAsync(
            new EntityContainerMovedNotification(new MoveEventInfo<EntityContainer>(Container(key), "-1,-20", -20), messages), ct);
        await access.HandleAsync(
            new EntityContainerMovedToRecycleBinNotification(new MoveToRecycleBinEventInfo<EntityContainer>(Container(key), "-1,-20"), messages), ct);
        await access.HandleAsync(new EntityContainerDeletedNotification(Container(key), messages), ct);

        return routed.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// The number of notification kinds driven in <see cref="RoutedSourcesAsync"/> must match what the
    /// handlers implement, so adding a handler method without adding it to the drive fails here instead
    /// of leaving its source unchecked.
    /// </summary>
    [Fact]
    public void EveryNotificationTheHandlersImplement_IsDrivenByTheCoverageTest()
    {
        // The drive covers 15 notification types: 3 on the own-notification handler (its document-type
        // one is driven once per family) and 12 on the access handler.
        var implemented = new[] { typeof(AdvancedPermissionsServerEventHandler), typeof(AccessServerEventHandler) }
            .SelectMany(t => t.GetInterfaces())
            .Count(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INotificationAsyncHandler<>));

        Assert.Equal(3 + 12, implemented);
    }

    /// <summary>
    /// Every source the handlers route is claimed by the authorizer: nothing is published that core
    /// would silently refuse to deliver.
    /// </summary>
    [Fact]
    public async Task EverySourceTheHandlersRoute_IsClaimedByTheAuthorizer()
    {
        var authorizer = new AdvancedPermissionsEventAuthorizer(Substitute.For<Microsoft.AspNetCore.Authorization.IAuthorizationService>());

        var routed = await RoutedSourcesAsync();

        Assert.NotEmpty(routed);
        Assert.All(routed, source => Assert.Contains(source, authorizer.AuthorizableEventSources));
    }

    /// <summary>
    /// Every source the authorizer claims is routed by some handler. A claimed source nothing publishes
    /// is a dead feature that looks alive, so the two sets must be equal, not merely nested.
    /// </summary>
    [Fact]
    public async Task EverySourceTheAuthorizerClaims_IsRoutedByAHandler()
    {
        var authorizer = new AdvancedPermissionsEventAuthorizer(Substitute.For<Microsoft.AspNetCore.Authorization.IAuthorizationService>());

        var routed = await RoutedSourcesAsync();

        Assert.Equal(
            authorizer.AuthorizableEventSources.OrderBy(s => s, StringComparer.Ordinal),
            routed.OrderBy(s => s, StringComparer.Ordinal));
    }

    /// <summary>Builds a substitute of an entity type with the given key.</summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="key">The key the substitute reports.</param>
    /// <returns>The substitute.</returns>
    private static T WithKey<T>(Guid key)
        where T : class, Umbraco.Cms.Core.Models.Entities.IEntity
    {
        var entity = Substitute.For<T>();
        entity.Key.Returns(key);
        return entity;
    }

    /// <summary>Builds an element folder with the given key.</summary>
    /// <param name="key">The folder's key.</param>
    /// <returns>The folder.</returns>
    private static EntityContainer Container(Guid key) =>
        new(global::Umbraco.Cms.Core.Constants.ObjectTypes.Element) { Key = key };
}
