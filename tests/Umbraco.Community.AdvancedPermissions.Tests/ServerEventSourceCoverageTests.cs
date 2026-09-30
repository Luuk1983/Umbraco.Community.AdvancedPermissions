using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Models.ServerEvents;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.ServerEvents;
using Umbraco.Community.AdvancedPermissions.Notifications;
using Umbraco.Community.AdvancedPermissions.ServerEvents;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests that every source the server-event handlers actually route is one the authorizer claims, and
/// that every source the authorizer claims is routed by some handler.
/// </summary>
/// <remarks>
/// The authorizer's own tests compare its list with the declared constants and with the literal wire
/// strings. This is the other half: it drives every notification through the real handlers and checks
/// the sources that come out the other end, so a handler that routes to a source nobody declared or
/// claimed - a typo, a new source added to a handler and nowhere else - fails here rather than
/// delivering to nobody. Core delivers no source that no authorizer claims, so the failure it guards
/// against is silent.
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
        await own.HandleAsync(new DocTypePermissionsChangedNotification(key, "editors", [key]), ct);

        await access.HandleAsync(new UserGroupSavedNotification(WithKey<IUserGroup>(key), messages), ct);
        await access.HandleAsync(new UserGroupDeletedNotification(WithKey<IUserGroup>(key), messages), ct);
        await access.HandleAsync(new UserSavedNotification(WithKey<IUser>(key), messages), ct);
        await access.HandleAsync(new ContentMovedNotification(new MoveEventInfo<IContent>(WithKey<IContent>(key), "-1,-20", -20), messages), ct);
        await access.HandleAsync(new ContentMovedToRecycleBinNotification(new MoveToRecycleBinEventInfo<IContent>(WithKey<IContent>(key), "-1,-20"), messages), ct);
        await access.HandleAsync(new ContentDeletedNotification(WithKey<IContent>(key), messages), ct);

        return routed.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// The notification kinds driven in <see cref="RoutedSourcesAsync"/> must match what the handlers
    /// implement, so adding a handler method without adding it to the drive fails here instead of
    /// leaving its source unchecked.
    /// </summary>
    [Fact]
    public void EveryNotificationTheHandlersImplement_IsDrivenByTheCoverageTest()
    {
        // The drive covers 8 notification types: 2 on the own-notification handler and 6 on the
        // access handler.
        var implemented = new[] { typeof(AdvancedPermissionsServerEventHandler), typeof(AccessServerEventHandler) }
            .SelectMany(t => t.GetInterfaces())
            .Count(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INotificationAsyncHandler<>));

        Assert.Equal(2 + 6, implemented);
    }

    /// <summary>
    /// Every source the handlers route is claimed by the authorizer: nothing is published that core
    /// would silently refuse to deliver.
    /// </summary>
    [Fact]
    public async Task EverySourceTheHandlersRoute_IsClaimedByTheAuthorizer()
    {
        var authorizer = new AdvancedPermissionsEventAuthorizer(Substitute.For<IAuthorizationService>());

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
        var authorizer = new AdvancedPermissionsEventAuthorizer(Substitute.For<IAuthorizationService>());

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
        where T : class, IEntity
    {
        var entity = Substitute.For<T>();
        entity.Key.Returns(key);
        return entity;
    }
}
