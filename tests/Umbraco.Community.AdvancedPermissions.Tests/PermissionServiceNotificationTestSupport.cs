using Microsoft.Extensions.Logging;
using NSubstitute;
using Umbraco.Cms.Core.Cache;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Helpers shared by the notification tests of the permission services: caches that record their
/// invalidations, and a check for a logged error.
/// </summary>
/// <remarks>
/// The permission caches are concrete classes over Umbraco's <see cref="AppCaches"/>, so the only seam
/// for observing an invalidation is the <see cref="IAppPolicyCache"/> underneath them. Recording at that
/// seam is what lets a test see whether an invalidation happened at all, how often, and before or after
/// the write and the publish.
/// </remarks>
internal static class PermissionServiceNotificationTestSupport
{
    /// <summary>
    /// Wraps a runtime cache substitute in an <see cref="AppCaches"/>.
    /// </summary>
    /// <param name="runtimeCache">The runtime cache to expose.</param>
    /// <returns>The caches.</returns>
    internal static AppCaches AppCachesOver(IAppPolicyCache runtimeCache) =>
        new(runtimeCache, NoAppCache.Instance, new IsolatedCaches(_ => NoAppCache.Instance));

    /// <summary>
    /// Builds caches whose runtime cache records each invalidation, so a test can see whether the
    /// invalidation happened before or after another step.
    /// </summary>
    /// <param name="order">
    /// The list the <c>invalidate-role</c> (one user group's cached entries) and <c>invalidate-all</c>
    /// (every user's resolved permissions) markers are appended to.
    /// </param>
    /// <returns>Caches wired to the recording runtime cache.</returns>
    internal static AppCaches RecordingCaches(List<string> order)
    {
        var runtimeCache = Substitute.For<IAppPolicyCache>();
        runtimeCache.When(c => c.Clear(Arg.Any<string>())).Do(_ => order.Add("invalidate-role"));
        runtimeCache.When(c => c.ClearByKey(Arg.Any<string>())).Do(_ => order.Add("invalidate-all"));
        return AppCachesOver(runtimeCache);
    }

    /// <summary>
    /// Gets whether a logger substitute recorded a call at error level.
    /// </summary>
    /// <param name="logger">The logger substitute.</param>
    /// <returns><see langword="true"/> if an error was logged.</returns>
    internal static bool LoggedAnError(ILogger logger) =>
        logger.ReceivedCalls().Any(c =>
            c.GetMethodInfo().Name == nameof(ILogger.Log) &&
            c.GetArguments()[0] is LogLevel.Error);
}
