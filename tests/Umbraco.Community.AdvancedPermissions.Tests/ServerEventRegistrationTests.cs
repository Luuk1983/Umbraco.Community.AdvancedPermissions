using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.ServerEvents;
using Umbraco.Cms.Core.Services.Filters;
using Umbraco.Community.AdvancedPermissions.Composing;
using Umbraco.Community.AdvancedPermissions.ServerEvents;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests that the composer actually wires up the real-time server-event feature: every server-event
/// handler is registered for every notification it implements, and the authorizer that makes the
/// event sources deliverable is registered.
/// </summary>
/// <remarks>
/// <para>
/// This is a silent-failure surface, and the rest of the suite cannot see it. The handler tests build
/// the handlers by hand and call them directly, and the authorizer tests build the authorizer by
/// hand, so all of them pass whether or not the composer ever registers any of it. Delete every
/// registration line and nothing fails - the site simply never pushes a live update, and no error is
/// logged anywhere. These tests are the only thing that fails when that happens.
/// </para>
/// <para>
/// The composer's <c>RegisterNotificationHandlers</c> and <c>RegisterServices</c> are private, so they
/// are invoked by reflection. The handler registrations are recorded in a real service collection; the
/// authorizer registration lives in one of Umbraco's collection builders, which the substitute
/// <see cref="IUmbracoBuilder"/> is wired to hand out as real instances so it can be inspected.
/// </para>
/// </remarks>
public sealed class ServerEventRegistrationTests
{
    /// <summary>The two handler types that publish this package's changes on the server-event hub.</summary>
    private static readonly Type[] ServerEventHandlerTypes =
        [typeof(AdvancedPermissionsServerEventHandler), typeof(AccessServerEventHandler)];

    /// <summary>
    /// Runs the composer's notification-handler registration against a fresh service collection.
    /// </summary>
    /// <returns>Every descriptor it registered, in registration order.</returns>
    private static IReadOnlyList<ServiceDescriptor> RegisteredDescriptors()
    {
        var services = new ServiceCollection();
        var builder = Substitute.For<IUmbracoBuilder>();
        builder.Services.Returns(services);

        Invoke("RegisterNotificationHandlers", builder);

        return services.ToList();
    }

    /// <summary>
    /// Invokes one of the composer's private static registration methods.
    /// </summary>
    /// <param name="methodName">The method to invoke.</param>
    /// <param name="builder">The builder to pass it.</param>
    private static void Invoke(string methodName, IUmbracoBuilder builder)
    {
        var method = typeof(AdvancedPermissionsComposer).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        method.Invoke(null, [builder]);
    }

    /// <summary>
    /// Gets the notification type a handler service type is registered for.
    /// </summary>
    /// <param name="serviceType">The registered service type.</param>
    /// <returns>The notification type, or <see langword="null"/> if it is not an async notification handler.</returns>
    private static Type? NotificationTypeOf(Type serviceType) =>
        serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == typeof(INotificationAsyncHandler<>)
            ? serviceType.GetGenericArguments()[0]
            : null;

    /// <summary>
    /// Every notification interface a server-event handler implements is registered for that handler.
    /// </summary>
    /// <remarks>
    /// A handler that implements <c>INotificationAsyncHandler&lt;T&gt;</c> but is never registered for
    /// it never runs, and the screen it feeds silently never updates. Compared as an exact set per
    /// handler, so it fails both for a registration that is missing and for one that names a
    /// notification the handler does not implement. Before this test, commenting out all eight
    /// registrations in <c>RegisterNotificationHandlers</c> left the build green.
    /// </remarks>
    [Fact]
    public void EveryNotificationTheServerEventHandlersImplement_IsRegisteredForThem()
    {
        var descriptors = RegisteredDescriptors();

        foreach (var handlerType in ServerEventHandlerTypes)
        {
            var implemented = handlerType.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INotificationAsyncHandler<>))
                .Select(i => i.GetGenericArguments()[0].FullName!)
                .Order(StringComparer.Ordinal)
                .ToList();
            var registered = descriptors
                .Where(d => d.ImplementationType == handlerType)
                .Select(d => NotificationTypeOf(d.ServiceType)?.FullName ?? "(not an async notification handler)")
                .Order(StringComparer.Ordinal)
                .ToList();

            Assert.NotEmpty(implemented);
            Assert.Equal(implemented, registered);
        }
    }

    /// <summary>
    /// Both server-event handlers are registered at all, and between them for the eight notifications
    /// the feature is specified to listen to: two of this package's own and six of Umbraco's.
    /// </summary>
    /// <remarks>
    /// The test above compares each handler with what it implements, so it would stay green if a
    /// notification were dropped from the handler and from the composer together. This one pins the
    /// number from the outside, so shrinking the feature has to be a decision made in this test rather
    /// than a side effect of an edit elsewhere.
    /// </remarks>
    [Fact]
    public void ServerEventHandlers_AreRegisteredForEightNotificationsInTotal()
    {
        var descriptors = RegisteredDescriptors();

        var registrations = descriptors
            .Where(d => d.ImplementationType is { } type && ServerEventHandlerTypes.Contains(type))
            .ToList();

        Assert.All(
            ServerEventHandlerTypes,
            handlerType => Assert.Contains(registrations, d => d.ImplementationType == handlerType));
        Assert.Equal(8, registrations.Count);
    }

    /// <summary>
    /// The composer registers <see cref="AdvancedPermissionsEventAuthorizer"/> with Umbraco's event
    /// source authorizer collection.
    /// </summary>
    /// <remarks>
    /// Umbraco delivers no server-event source that no authorizer claims, and it drops the event
    /// without a word, so a composer that forgets this line publishes into the void: every handler
    /// runs, every route call succeeds, and no browser ever receives anything. The authorizer's own
    /// tests construct it directly and pass regardless. Before this test, removing the
    /// <c>EventSourceAuthorizers().Append</c> registration left the build green.
    /// </remarks>
    [Fact]
    public void Composer_RegistersTheEventSourceAuthorizer()
    {
        var authorizers = new EventSourceAuthorizerCollectionBuilder();
        var builder = Substitute.For<IUmbracoBuilder>();
        builder.Services.Returns(new ServiceCollection());
        builder.WithCollectionBuilder<EventSourceAuthorizerCollectionBuilder>().Returns(authorizers);
        builder.WithCollectionBuilder<ContentTypeFilterCollectionBuilder>().Returns(new ContentTypeFilterCollectionBuilder());

        Invoke("RegisterServices", builder);

        Assert.True(
            authorizers.Has<AdvancedPermissionsEventAuthorizer>(),
            "The composer does not register AdvancedPermissionsEventAuthorizer, so core will never deliver any of this package's server events.");
    }
}
