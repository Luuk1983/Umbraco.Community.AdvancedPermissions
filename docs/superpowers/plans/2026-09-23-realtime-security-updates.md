# Real-time security updates — implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every Advanced Permissions editor and viewer reacts to a security change while it is open — viewers refresh themselves, editors holding unsaved work are flagged at cell granularity and never silently overwritten, and the server refuses a stale save outright.

**Architecture:** No new SignalR hub. The server publishes three event sources onto Umbraco's built-in hub via `IServerEventRouter`, gated by one `IEventSourceAuthorizer`; the backoffice consumes them through `UMB_MANAGEMENT_API_SERVER_EVENT_CONTEXT`. Because `ServerEvent` carries no payload, every event is treated as a hint and truth comes from a refetch. Correctness is guaranteed independently of the transport by a concurrency **stamp** — a hash of the stored entries for one node and user group — checked by a new all-or-nothing batch save endpoint.

**Tech Stack:** .NET 10 / Umbraco 17.4.2, EF Core (SQLite + SQL Server), xunit + NSubstitute; TypeScript + Lit, Vite, new vitest setup.

**Spec:** `docs/superpowers/specs/2026-09-23-realtime-security-updates-design.md`
**Issue:** https://github.com/Luuk1983/Umbraco.Community.AdvancedPermissions/issues/55

---

## Read this first

**Project rules that apply to every task in this plan:**

- **Test-first, always.** Write the failing test, run it, watch it fail for the right reason, then implement. This is non-negotiable for backend code.
- **Primary constructors** for any class taking dependencies: `public sealed class Foo(IBar bar)`.
- **`record` over `class`** for new types unless you need reference equality or mutability.
- **`var` for locals** where the type is apparent.
- **XML docs on every type, method and property** — including `private` and `internal` members. Say what it does and why it exists, not a restatement of the name.
- **Never commit unless the user asks.** The commit steps below are written as `git add` only. Stage the work; leave the commit to the user.

**Namespace collision gotcha (CLAUDE.md #10):** the project namespace is `Umbraco.Community.AdvancedPermissions`, so the compiler resolves a bare `Umbraco.` prefix against the enclosing namespace first. Keep every `Umbraco.Cms.*` reference in a `using` directive at the top of the file. Never write `Umbraco.Cms.Core.Events.INotification` inline inside a class in this namespace — it will not compile.

**`exactOptionalPropertyTypes: true` (CLAUDE.md #5):** on the client, never assign `undefined` to an optional field `field?: T`. Declare `field: T | undefined = undefined` instead.

---

## File structure

### Server — new files

| File | Responsibility |
|---|---|
| `src/Umbraco.Community.AdvancedPermissions.Core/Concurrency/PermissionStamp.cs` | Pure canonicalisation + hash of a set of entries. No Umbraco dependency, so it is testable in `Core.Tests`. |
| `src/Umbraco.Community.AdvancedPermissions/Notifications/AdvancedPermissionsChangedNotification.cs` | The notification raised after a permission write commits. |
| `src/Umbraco.Community.AdvancedPermissions/Notifications/DocTypePermissionsChangedNotification.cs` | The document-type equivalent. |
| `src/Umbraco.Community.AdvancedPermissions/ServerEvents/AdvancedPermissionsServerEvents.cs` | The event-source and event-type string constants. One place, so a rename cannot drift. |
| `src/Umbraco.Community.AdvancedPermissions/ServerEvents/AdvancedPermissionsEventAuthorizer.cs` | Authorizes all three sources against `SectionAccessUsers`. |
| `src/Umbraco.Community.AdvancedPermissions/ServerEvents/AdvancedPermissionsServerEventHandler.cs` | Translates this package's own notifications into routed `ServerEvent`s. |
| `src/Umbraco.Community.AdvancedPermissions/ServerEvents/AccessServerEventHandler.cs` | Translates Umbraco's user/group/content notifications into the `Access` source. |
| `src/Umbraco.Community.AdvancedPermissions/Controllers/Models/BatchSavePermissionsModels.cs` | Batch request + 409 conflict response models. |

### Server — modified files

| File | Change |
|---|---|
| `src/.../Abstractions/Interfaces/IAdvancedPermissionRepository.cs` | Add `SaveManyAsync` (one transaction). |
| `src/.../Data/Repositories/AdvancedPermissionRepository.cs` | Implement `SaveManyAsync`. |
| `src/.../Abstractions/Interfaces/IAdvancedPermissionService.cs` | Add `SaveManyAsync`. |
| `src/.../AdvancedPermissions/Services/AdvancedPermissionService.cs` | Inject `IEventAggregator`; raise the notification after invalidation; implement `SaveManyAsync`. |
| `src/.../Controllers/Models/TreeNodeResponseModel.cs` | Add `Stamp`. |
| `src/.../Controllers/Models/SavePermissionsRequestModel.cs` | Add optional `ExpectedStamp` + `Force`. |
| `src/.../Controllers/AdvancedPermissionsPermissionController.cs` | Stamp on GET responses; the new batch endpoint; stamp check on the single PUT. |
| `src/.../Controllers/AdvancedPermissionsTreeController.cs` | Populate `Stamp` per node. |
| `src/.../Composing/AdvancedPermissionsComposer.cs` | Register the authorizer and both event handlers. |

### Client — new files

| File | Responsibility |
|---|---|
| `Client/vitest.config.ts` | Test runner config. |
| `Client/src/live/security-events.controller.ts` | Subscribes to core's server-event context, filters our sources, calls the host back. |
| `Client/src/live/coalesce.ts` | Pure: one run in flight, at most one queued. |
| `Client/src/live/coalesce.test.ts` | Its tests. |
| `Client/src/live/conflict.ts` | Pure: classify one cell from `base` / `mine` / `theirs`. |
| `Client/src/live/conflict.test.ts` | Its tests. |
| `Client/src/live/uap-live-banner.element.ts` | The "changed elsewhere" banner. |
| `Client/src/live/uap-conflict-dialog.element.ts` | The save-time confirmation dialog. |

### Client — modified files

| File | Change |
|---|---|
| `Client/package.json` | `vitest` dev dependency + `test` script. |
| `Client/src/api/advanced-permissions.api.ts` | `savePermissionsBatch`; stamps on reads. |
| `Client/src/models/permission.models.ts` | `stamp` on `TreeNode`; conflict types. |
| `Client/src/permissions-editor/uap-permissions-editor-root.element.ts` | Live wiring, flags, banner, batch save, 409 dialog. |
| `Client/src/access-viewer/uap-access-viewer-root.element.ts` | Live wiring + refreshed pill. |
| `Client/src/doc-type-permissions/uap-doc-type-permissions-editor-root.element.ts` | Live wiring, flags, banner, batch save, 409 dialog. |
| `Client/src/doc-type-permissions/uap-doc-type-create-audit-root.element.ts` | Live wiring + refreshed pill. |
| `Client/src/localization/*.ts` (28 files) | New UI strings. |

---

## Phase 1 — the stamp

### Task 1: `PermissionStamp` — canonicalise and hash

**Files:**
- Create: `src/Umbraco.Community.AdvancedPermissions.Core/Concurrency/PermissionStamp.cs`
- Test: `tests/Umbraco.Community.AdvancedPermissions.Core.Tests/Concurrency/PermissionStampTests.cs`

This lives in `Core` rather than the main package because it has no Umbraco dependency and `Core.Tests` can exercise it without booting anything.

- [ ] **Step 1: Write the failing tests**

Create `tests/Umbraco.Community.AdvancedPermissions.Core.Tests/Concurrency/PermissionStampTests.cs`:

```csharp
using Umbraco.Community.AdvancedPermissions.Core.Concurrency;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Core.Tests.Concurrency;

/// <summary>
/// Tests for <see cref="PermissionStamp"/>, the concurrency stamp used to detect that stored
/// permission entries moved underneath a client between load and save.
/// </summary>
public sealed class PermissionStampTests
{
    /// <summary>Builds an entry with the fields the stamp is computed from.</summary>
    /// <param name="verb">The permission verb.</param>
    /// <param name="state">The permission state.</param>
    /// <param name="scope">The permission scope.</param>
    /// <param name="isPriorityOverride">Whether the entry is a priority override.</param>
    /// <returns>The entry.</returns>
    private static AdvancedPermissionEntry Entry(
        string verb,
        PermissionState state = PermissionState.Allow,
        PermissionScope scope = PermissionScope.ThisNodeOnly,
        bool isPriorityOverride = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            NodeKey = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            RoleAlias = "editors",
            Verb = verb,
            State = state,
            Scope = scope,
            IsPriorityOverride = isPriorityOverride,
        };

    /// <summary>An empty set must hash to one fixed, non-empty value rather than to nothing.</summary>
    [Fact]
    public void Compute_EmptySet_ReturnsStableNonEmptyStamp()
    {
        var first = PermissionStamp.Compute([]);
        var second = PermissionStamp.Compute([]);

        Assert.False(string.IsNullOrWhiteSpace(first));
        Assert.Equal(first, second);
    }

    /// <summary>The stamp must not depend on the order entries arrive in.</summary>
    [Fact]
    public void Compute_ReorderedEntries_ReturnsSameStamp()
    {
        var a = Entry("Umb.Document.Read");
        var b = Entry("Umb.Document.Publish", PermissionState.Deny);

        Assert.Equal(PermissionStamp.Compute([a, b]), PermissionStamp.Compute([b, a]));
    }

    /// <summary>The row id is identity, not content, so it must not affect the stamp.</summary>
    [Fact]
    public void Compute_DifferentIdsSameContent_ReturnsSameStamp()
    {
        var a = Entry("Umb.Document.Read");
        var b = Entry("Umb.Document.Read");

        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(PermissionStamp.Compute([a]), PermissionStamp.Compute([b]));
    }

    /// <summary>Every field the stamp claims to cover must actually change it.</summary>
    /// <param name="verb">The verb to use.</param>
    /// <param name="state">The state to use.</param>
    /// <param name="scope">The scope to use.</param>
    /// <param name="isPriorityOverride">The priority-override flag to use.</param>
    [Theory]
    [InlineData("Umb.Document.Publish", PermissionState.Allow, PermissionScope.ThisNodeOnly, false)]
    [InlineData("Umb.Document.Read", PermissionState.Deny, PermissionScope.ThisNodeOnly, false)]
    [InlineData("Umb.Document.Read", PermissionState.Allow, PermissionScope.DescendantsOnly, false)]
    [InlineData("Umb.Document.Read", PermissionState.Allow, PermissionScope.ThisNodeOnly, true)]
    public void Compute_AnyFieldChanged_ReturnsDifferentStamp(
        string verb,
        PermissionState state,
        PermissionScope scope,
        bool isPriorityOverride)
    {
        var baseline = PermissionStamp.Compute([Entry("Umb.Document.Read")]);
        var changed = PermissionStamp.Compute([Entry(verb, state, scope, isPriorityOverride)]);

        Assert.NotEqual(baseline, changed);
    }

    /// <summary>
    /// A golden vector. The stamp crosses the wire and is compared against a value the client
    /// was handed earlier, so an accidental change to the canonical form is a breaking change:
    /// it would reject saves that are perfectly valid. This test is the tripwire for that.
    /// </summary>
    [Fact]
    public void Compute_KnownInput_MatchesGoldenVector()
    {
        var entries = new[]
        {
            Entry("Umb.Document.Read"),
            Entry("Umb.Document.Publish", PermissionState.Deny, PermissionScope.ThisNodeAndDescendants, true),
        };

        Assert.Equal("REPLACE_WITH_OBSERVED_VALUE", PermissionStamp.Compute(entries));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test tests/Umbraco.Community.AdvancedPermissions.Core.Tests/ --filter PermissionStampTests
```

Expected: build failure — `PermissionStamp` does not exist.

- [ ] **Step 3: Implement `PermissionStamp`**

Create `src/Umbraco.Community.AdvancedPermissions.Core/Concurrency/PermissionStamp.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Core.Concurrency;

/// <summary>
/// Computes a concurrency stamp over the stored permission entries for one node and user group.
/// </summary>
/// <remarks>
/// <para>
/// The stamp is what lets the server tell "this client is writing back a picture it loaded before
/// somebody else changed it" from an ordinary save. It is a hash rather than a row version because
/// it needs no schema change and behaves identically on SQLite and SQL Server, and because the
/// unit it has to describe is a *set* of rows, which no single row's version can stand for.
/// </para>
/// <para>
/// Only content is hashed, never identity: two entries that say the same thing produce the same
/// stamp even though their row ids differ, because re-saving the identical permission must not
/// look like a change to anybody else.
/// </para>
/// <para>
/// The canonical form is part of the wire contract — the client compares a stamp it was handed
/// earlier — so changing it rejects saves that are valid. <c>PermissionStampTests</c> holds a
/// golden vector that fails if this changes by accident.
/// </para>
/// </remarks>
public static class PermissionStamp
{
    /// <summary>The separator between fields within one entry. Not legal in any verb.</summary>
    private const char FieldSeparator = '\u001f';

    /// <summary>The separator between entries. Not legal in any verb.</summary>
    private const char EntrySeparator = '\u001e';

    /// <summary>
    /// Computes the stamp for a set of entries.
    /// </summary>
    /// <param name="entries">
    /// The stored entries for one node and user group, in any order. An empty set is valid and
    /// hashes to one fixed value.
    /// </param>
    /// <returns>The stamp, as lowercase hex.</returns>
    public static string Compute(IEnumerable<AdvancedPermissionEntry> entries)
    {
        var canonical = new StringBuilder();

        // Ordered on every field the stamp covers, so two sets that differ only in the order they
        // came back from the database — which EF does not promise to keep stable — hash the same.
        var ordered = entries
            .Select(e => (e.Verb, e.State, e.Scope, e.IsPriorityOverride))
            .OrderBy(e => e.Verb, StringComparer.Ordinal)
            .ThenBy(e => (int)e.State)
            .ThenBy(e => (int)e.Scope)
            .ThenBy(e => e.IsPriorityOverride);

        foreach (var entry in ordered)
        {
            // The verb is length-prefixed because it is the only variable-length field. Without
            // the prefix, a verb containing a separator character could be read as structure — a
            // single entry whose verb embedded the right bytes would canonicalise identically to
            // two unrelated entries, so two different permission pictures would hash the same and
            // the concurrency check would pass when it should fail. Verbs are allowlist-validated
            // at the controller today, but nothing about this type enforces that, and a hash whose
            // safety rests on a convention elsewhere is not safe.
            canonical
                .Append(entry.Verb.Length).Append(FieldSeparator)
                .Append(entry.Verb).Append(FieldSeparator)
                .Append((int)entry.State).Append(FieldSeparator)
                .Append((int)entry.Scope).Append(FieldSeparator)
                .Append(entry.IsPriorityOverride ? '1' : '0').Append(EntrySeparator);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return Convert.ToHexStringLower(hash);
    }
}
```

- [ ] **Step 4: Run the tests and fill in the golden vector**

```bash
dotnet test tests/Umbraco.Community.AdvancedPermissions.Core.Tests/ --filter PermissionStampTests
```

Every test passes except `Compute_KnownInput_MatchesGoldenVector`, which fails with the actual
hash in its message. Copy that value into the test, replacing `REPLACE_WITH_OBSERVED_VALUE`.
This is the one place in this plan where a test is written to match the implementation, and it is
deliberate: the vector's job is to detect *future* accidental changes, not to specify the hash.

- [ ] **Step 5: Re-run — all green**

```bash
dotnet test tests/Umbraco.Community.AdvancedPermissions.Core.Tests/ --filter PermissionStampTests
```

Expected: 8 passed.

- [ ] **Step 6: Stage**

```bash
git add src/Umbraco.Community.AdvancedPermissions.Core/Concurrency/PermissionStamp.cs tests/Umbraco.Community.AdvancedPermissions.Core.Tests/Concurrency/PermissionStampTests.cs
```

---

### Task 2: Return the stamp from the read endpoints

**Files:**
- Modify: `src/Umbraco.Community.AdvancedPermissions/Controllers/Models/TreeNodeResponseModel.cs`
- Modify: `src/Umbraco.Community.AdvancedPermissions/Controllers/AdvancedPermissionsTreeController.cs`
- Modify: `src/Umbraco.Community.AdvancedPermissions/Controllers/AdvancedPermissionsPermissionController.cs`
- Test: `tests/Umbraco.Community.AdvancedPermissions.Tests/PermissionStampEndpointTests.cs`

The editor populates its grid from the tree endpoints, so that is where the stamp has to arrive.
The server computes it from the same entries it is already returning — no extra query.

- [ ] **Step 1: Write the failing test**

Create `tests/Umbraco.Community.AdvancedPermissions.Tests/PermissionStampEndpointTests.cs`:

```csharp
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Concurrency;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests that the stamp a read endpoint hands out is the stamp a save will be checked against.
/// </summary>
public sealed class PermissionStampEndpointTests
{
    /// <summary>
    /// A tree node's stamp must be computed from exactly the entries it reports, so that a client
    /// that saves back what it was given, unchanged, is never told it conflicts.
    /// </summary>
    [Fact]
    public void TreeNodeResponseModel_CarriesStampOfItsOwnEntries()
    {
        var nodeKey = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var entries = new List<PermissionEntryResponseModel>
        {
            new(Guid.NewGuid(), nodeKey, "editors", "Umb.Document.Read", "Allow", "ThisNodeOnly", false),
        };

        var stamp = entries.ComputeFromResponse();
        var model = new TreeNodeResponseModel(nodeKey, "Products", "icon-document", false, entries, stamp);

        Assert.Equal(stamp, model.Stamp);
        Assert.NotEqual(PermissionStamp.Compute([]), model.Stamp);
    }

    /// <summary>A node with no entries still carries the well-known empty stamp, never null.</summary>
    [Fact]
    public void TreeNodeResponseModel_NoEntries_CarriesEmptyStamp()
    {
        var nodeKey = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var stamp = Array.Empty<PermissionEntryResponseModel>().ComputeFromResponse();
        var model = new TreeNodeResponseModel(nodeKey, "Empty", null, false, [], stamp);

        Assert.Equal(PermissionStamp.Compute([]), model.Stamp);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

```bash
dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests/ --filter PermissionStampEndpointTests
```

Expected: build failure — `ComputeFromResponse` does not exist and `TreeNodeResponseModel` has no `Stamp`.

- [ ] **Step 3: Add the response overload to `PermissionStamp`**

The controllers hold `PermissionEntryResponseModel`, not `AdvancedPermissionEntry`. Rather than
map back, add an overload that canonicalises the same way. Append to
`src/Umbraco.Community.AdvancedPermissions.Core/Concurrency/PermissionStamp.cs` — but note that
`Core` cannot see the controller model, so the overload takes the four fields as a tuple and the
controller projects into it.

Replace the `Compute` method with a shared private core plus two public entry points:

```csharp
    /// <summary>
    /// Computes the stamp for a set of entries.
    /// </summary>
    /// <param name="entries">The stored entries for one node and user group, in any order.</param>
    /// <returns>The stamp, as lowercase hex.</returns>
    public static string Compute(IEnumerable<AdvancedPermissionEntry> entries) =>
        ComputeCore(entries.Select(e => (e.Verb, (int)e.State, (int)e.Scope, e.IsPriorityOverride)));

    /// <summary>
    /// Computes the stamp from entries whose state and scope are already strings, as the API
    /// response models carry them.
    /// </summary>
    /// <param name="entries">Verb, state name, scope name and priority-override flag per entry.</param>
    /// <returns>The stamp, as lowercase hex.</returns>
    /// <exception cref="ArgumentException">A state or scope name is not a known value.</exception>
    public static string ComputeFromNames(IEnumerable<(string Verb, string State, string Scope, bool IsPriorityOverride)> entries) =>
        ComputeCore(entries.Select(e =>
        {
            if (!Enum.TryParse<PermissionState>(e.State, ignoreCase: true, out var state))
            {
                throw new ArgumentException($"'{e.State}' is not a valid permission state.", nameof(entries));
            }

            if (!Enum.TryParse<PermissionScope>(e.Scope, ignoreCase: true, out var scope))
            {
                throw new ArgumentException($"'{e.Scope}' is not a valid permission scope.", nameof(entries));
            }

            return (e.Verb, (int)state, (int)scope, e.IsPriorityOverride);
        }));

    /// <summary>
    /// The one canonicalisation, shared by both entry points so the two can never disagree.
    /// </summary>
    /// <param name="entries">Verb, state ordinal, scope ordinal and priority-override flag per entry.</param>
    /// <returns>The stamp, as lowercase hex.</returns>
    private static string ComputeCore(IEnumerable<(string Verb, int State, int Scope, bool IsPriorityOverride)> entries)
    {
        var canonical = new StringBuilder();

        var ordered = entries
            .OrderBy(e => e.Verb, StringComparer.Ordinal)
            .ThenBy(e => e.State)
            .ThenBy(e => e.Scope)
            .ThenBy(e => e.IsPriorityOverride);

        foreach (var entry in ordered)
        {
            // The verb is length-prefixed, and that is not decoration. It is the only
            // variable-length field, and without the prefix a verb containing a separator
            // character could be read as structure: a single entry whose verb embedded the
            // right bytes would canonicalise identically to two unrelated entries, and two
            // different permission pictures would hash the same — silently defeating the
            // concurrency check this whole function exists to provide.
            canonical
                .Append(entry.Verb.Length).Append(FieldSeparator)
                .Append(entry.Verb).Append(FieldSeparator)
                .Append(entry.State).Append(FieldSeparator)
                .Append(entry.Scope).Append(FieldSeparator)
                .Append(entry.IsPriorityOverride ? '1' : '0').Append(EntrySeparator);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return Convert.ToHexStringLower(hash);
    }
```

**Do not change the canonical form while refactoring.** `ComputeCore` must produce byte-identical
output to the `Compute` written in Task 1, including the verb's length prefix (`string.Length`, the
UTF-16 code-unit count). The golden-vector test is what catches a slip here; if it fails after this
refactor, the refactor is wrong, not the test.

- [ ] **Step 4: Add a controller-side helper**

Create `src/Umbraco.Community.AdvancedPermissions/Controllers/PermissionStampExtensions.cs` —
under `Controllers/`, not `Controllers/Models/`, because that folder holds wire shapes and this is
behaviour:

```csharp
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Concurrency;

namespace Umbraco.Community.AdvancedPermissions.Controllers;

/// <summary>
/// Bridges the API response models to <see cref="PermissionStamp"/>, which lives in Core and
/// therefore cannot see them.
/// </summary>
public static class PermissionStampExtensions
{
    /// <summary>
    /// Computes the concurrency stamp for a set of response entries.
    /// </summary>
    /// <param name="entries">The entries being returned for one node and user group.</param>
    /// <returns>The stamp, as lowercase hex.</returns>
    public static string ComputeFromResponse(this IEnumerable<PermissionEntryResponseModel> entries) =>
        PermissionStamp.ComputeFromNames(
            entries.Select(e => (e.Verb, e.State, e.Scope, e.IsPriorityOverride)));
}
```

- [ ] **Step 5: Add `Stamp` to `TreeNodeResponseModel`**

Replace the record in `src/Umbraco.Community.AdvancedPermissions/Controllers/Models/TreeNodeResponseModel.cs`:

```csharp
/// <param name="Key">The unique key of the content node.</param>
/// <param name="Name">The display name of the content node.</param>
/// <param name="Icon">The content type icon, if available.</param>
/// <param name="HasChildren">Whether this node has child nodes.</param>
/// <param name="Entries">The stored permission entries for the requested user group on this node.</param>
/// <param name="Stamp">
/// The concurrency stamp of <paramref name="Entries"/>. A client sends this back when saving; the
/// server refuses the save if the stored entries have moved since. A node with no entries carries
/// the well-known empty-set stamp, never null.
/// </param>
public sealed record TreeNodeResponseModel(
    Guid Key,
    string Name,
    string? Icon,
    bool HasChildren,
    IReadOnlyList<PermissionEntryResponseModel> Entries,
    string Stamp);
```

- [ ] **Step 6: Populate it in the tree controller**

In `src/Umbraco.Community.AdvancedPermissions/Controllers/AdvancedPermissionsTreeController.cs`,
find every construction of `TreeNodeResponseModel` and add the stamp as the final argument,
computed from the same entries list already being passed:

```csharp
new TreeNodeResponseModel(key, name, icon, hasChildren, nodeEntries, nodeEntries.ComputeFromResponse())
```

Add `using Umbraco.Community.AdvancedPermissions.Controllers.Models;` if it is not already present.

- [ ] **Step 7: Add an ETag to the bare-array GET**

In `AdvancedPermissionsPermissionController.GetPermissions`, set the stamp as an `ETag` header —
the response body stays a bare array, so no existing consumer breaks:

```csharp
        var entries = await permissionService.GetEntriesAsync(nodeKey, roleAlias, cancellationToken);
        var models = entries.Select(MapEntry).ToList();
        Response.Headers.ETag = $"\"{models.ComputeFromResponse()}\"";
        return Ok(models);
```

- [ ] **Step 8: Run the tests**

```bash
dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests/ --filter PermissionStampEndpointTests
dotnet build
```

Expected: 2 passed, build clean. If the build fails on other `TreeNodeResponseModel` call sites,
add the stamp argument there too.

- [ ] **Step 9: Stage**

```bash
git add src/Umbraco.Community.AdvancedPermissions.Core/Concurrency/PermissionStamp.cs src/Umbraco.Community.AdvancedPermissions/Controllers/ tests/Umbraco.Community.AdvancedPermissions.Tests/PermissionStampEndpointTests.cs
```

---

## Phase 2 — the batch save

### Task 3: `SaveManyAsync` on the repository

**Files:**
- Modify: `src/Umbraco.Community.AdvancedPermissions.Abstractions/Interfaces/IAdvancedPermissionRepository.cs`
- Modify: `src/Umbraco.Community.AdvancedPermissions.Data/Repositories/AdvancedPermissionRepository.cs`
- Test: `tests/Umbraco.Community.AdvancedPermissions.Data.Tests/AdvancedPermissionRepositoryBatchTests.cs`

All-or-nothing is the whole point: the current per-node loop means a rejection partway through
leaves some nodes written and some not, which is worse than either outcome it sits between.

- [ ] **Step 1: Write the failing test**

Create `tests/Umbraco.Community.AdvancedPermissions.Data.Tests/AdvancedPermissionRepositoryBatchTests.cs`.
Follow the existing fixture pattern in that project for creating a SQLite-backed repository — open
`tests/Umbraco.Community.AdvancedPermissions.Data.Tests/` and copy the setup from the existing
repository test class rather than inventing a second way to build one.

```csharp
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Data.Tests;

/// <summary>
/// Tests for the all-or-nothing batch write used by the editors' save.
/// </summary>
public sealed class AdvancedPermissionRepositoryBatchTests
{
    /// <summary>A batch covering several nodes writes every one of them.</summary>
    [Fact]
    public async Task SaveManyAsync_MultipleNodes_WritesAll()
    {
        // Arrange: build the repository the same way the existing repository tests do.
        var nodeA = Guid.NewGuid();
        var nodeB = Guid.NewGuid();

        var batch = new[]
        {
            (NodeKey: nodeA, RoleAlias: "editors", Entries: (IEnumerable<(string, PermissionState, PermissionScope, bool)>)
                [("Umb.Document.Read", PermissionState.Allow, PermissionScope.ThisNodeOnly, false)]),
            (NodeKey: nodeB, RoleAlias: "editors", Entries: (IEnumerable<(string, PermissionState, PermissionScope, bool)>)
                [("Umb.Document.Publish", PermissionState.Deny, PermissionScope.ThisNodeOnly, false)]),
        };

        await repository.SaveManyAsync(batch, CancellationToken.None);

        Assert.Single(await repository.GetByNodeAndRoleAsync(nodeA, "editors", CancellationToken.None));
        Assert.Single(await repository.GetByNodeAndRoleAsync(nodeB, "editors", CancellationToken.None));
    }

    /// <summary>
    /// A batch that fails partway must leave the database exactly as it found it. Without this the
    /// concurrency check would be worse than useless: a rejected save could still have written half
    /// its nodes.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_FailureMidway_WritesNothing()
    {
        var nodeA = Guid.NewGuid();
        var nodeB = Guid.NewGuid();

        var batch = new[]
        {
            (NodeKey: nodeA, RoleAlias: "editors", Entries: (IEnumerable<(string, PermissionState, PermissionScope, bool)>)
                [("Umb.Document.Read", PermissionState.Allow, PermissionScope.ThisNodeOnly, false)]),
            // A null alias makes the second write fail at the database, after the first has been staged.
            (NodeKey: nodeB, RoleAlias: null!, Entries: (IEnumerable<(string, PermissionState, PermissionScope, bool)>)
                [("Umb.Document.Publish", PermissionState.Deny, PermissionScope.ThisNodeOnly, false)]),
        };

        await Assert.ThrowsAnyAsync<Exception>(() => repository.SaveManyAsync(batch, CancellationToken.None));

        Assert.Empty(await repository.GetByNodeAndRoleAsync(nodeA, "editors", CancellationToken.None));
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

```bash
dotnet test tests/Umbraco.Community.AdvancedPermissions.Data.Tests/ --filter AdvancedPermissionRepositoryBatchTests
```

Expected: build failure — `SaveManyAsync` does not exist.

- [ ] **Step 3: Add the interface method**

Append to `IAdvancedPermissionRepository` in
`src/Umbraco.Community.AdvancedPermissions.Abstractions/Interfaces/IAdvancedPermissionRepository.cs`:

```csharp
    /// <summary>
    /// Replaces the entries for several node-and-user-group pairs in a single transaction.
    /// </summary>
    /// <remarks>
    /// All or nothing. The editors save many nodes at once, and a partial write — some nodes
    /// updated, some not — is a worse outcome than either completing or refusing, because nothing
    /// afterwards can tell which half landed.
    /// </remarks>
    /// <param name="batch">The node key, user group alias and replacement entries for each pair.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    Task SaveManyAsync(
        IEnumerable<(Guid NodeKey, string RoleAlias, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries)> batch,
        CancellationToken cancellationToken = default);
```

- [ ] **Step 4: Implement it**

In `src/Umbraco.Community.AdvancedPermissions.Data/Repositories/AdvancedPermissionRepository.cs`,
read the existing `SaveAsync` first and reuse its delete-then-insert shape. The new method opens
one `DbContext`, wraps every pair in one explicit transaction, and saves once:

```csharp
    /// <inheritdoc />
    public async Task SaveManyAsync(
        IEnumerable<(Guid NodeKey, string RoleAlias, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries)> batch,
        CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.CreateDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        foreach (var (nodeKey, roleAlias, entries) in batch)
        {
            // Deleted with ExecuteDeleteAsync, the same mechanism SaveAsync uses, rather than by
            // tracking and removing entities. The unique index covers (NodeKey, RoleAlias, Verb,
            // Scope), and the ordinary save — flipping a verb from Allow to Deny — replaces a row
            // with one that has the same four values. Staging the delete and the insert into one
            // SaveChangesAsync leaves the order to EF's command batcher, which has varied across
            // versions; an immediate delete makes it structural. It is also what makes the
            // explicit transaction load-bearing rather than decorative: the deletes execute now
            // and the inserts are deferred, so only the transaction makes the pair atomic.
            await context.AdvancedPermissions
                .Where(e => e.NodeKey == nodeKey && e.RoleAlias == roleAlias)
                .ExecuteDeleteAsync(cancellationToken);

            foreach (var (verb, state, scope, isPriorityOverride) in entries)
            {
                context.AdvancedPermissions.Add(new AdvancedPermissionEntity
                {
                    Id = Guid.NewGuid(),
                    NodeKey = nodeKey,
                    RoleAlias = roleAlias,
                    Verb = verb,
                    State = (int)state,
                    Scope = (int)scope,
                    IsPriorityOverride = isPriorityOverride,
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
```

Match the entity type name, the `DbSet` property name and the column types to what `SaveAsync`
already uses in that file — the names above are the shape, not necessarily the spelling.

- [ ] **Step 5: Run the tests**

```bash
dotnet test tests/Umbraco.Community.AdvancedPermissions.Data.Tests/ --filter AdvancedPermissionRepositoryBatchTests
```

Expected: 2 passed.

- [ ] **Step 6: Stage**

```bash
git add src/Umbraco.Community.AdvancedPermissions.Abstractions/Interfaces/IAdvancedPermissionRepository.cs src/Umbraco.Community.AdvancedPermissions.Data/Repositories/AdvancedPermissionRepository.cs tests/Umbraco.Community.AdvancedPermissions.Data.Tests/AdvancedPermissionRepositoryBatchTests.cs
```

---

### Task 4: The notification types

**Files:**
- Create: `src/Umbraco.Community.AdvancedPermissions/Notifications/AdvancedPermissionsChangedNotification.cs`
- Create: `src/Umbraco.Community.AdvancedPermissions/Notifications/DocTypePermissionsChangedNotification.cs`

These live in the main package, not `Abstractions`: `Abstractions` is a pure contract package with
no Umbraco dependency, and `INotification` comes from `Umbraco.Cms.Core`. Nothing is lost by it —
you cannot register a notification handler without referencing Umbraco anyway.

No test of its own: a record with no behaviour cannot fail independently of the code that raises
it, which Task 5 tests.

- [ ] **Step 1: Create the permission notification**

```csharp
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Community.AdvancedPermissions.Notifications;

/// <summary>
/// Published after advanced permission entries for one node and user group have been written and
/// the caches for them invalidated.
/// </summary>
/// <remarks>
/// This is the package's public extensibility seam for permission writes: another package can
/// handle it to audit, mirror or react to a change without taking a dependency on how the write
/// happened. The package's own server-event handler is one such consumer, not a special case.
/// </remarks>
/// <param name="NodeKey">
/// The content node key. <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> indicates the
/// virtual root.
/// </param>
/// <param name="RoleAlias">The user group alias, or <c>$everyone</c> for the All Users Group.</param>
/// <param name="Verbs">The verbs whose entries were written. Empty when all entries were removed.</param>
public sealed record AdvancedPermissionsChangedNotification(
    Guid NodeKey,
    string RoleAlias,
    IReadOnlyList<string> Verbs) : INotification;
```

- [ ] **Step 2: Create the document-type notification**

```csharp
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Community.AdvancedPermissions.Notifications;

/// <summary>
/// Published after document-type create permissions for one node and user group have been written
/// and the caches for them invalidated.
/// </summary>
/// <remarks>The document-type counterpart of <see cref="AdvancedPermissionsChangedNotification"/>.</remarks>
/// <param name="NodeKey">The content node key the rule is anchored to.</param>
/// <param name="RoleAlias">The user group alias, or <c>$everyone</c> for the All Users Group.</param>
/// <param name="ContentTypeKeys">The document types whose entries were written.</param>
public sealed record DocTypePermissionsChangedNotification(
    Guid NodeKey,
    string RoleAlias,
    IReadOnlyList<Guid> ContentTypeKeys) : INotification;
```

- [ ] **Step 3: Build**

```bash
dotnet build
```

Expected: clean.

- [ ] **Step 4: Stage**

```bash
git add src/Umbraco.Community.AdvancedPermissions/Notifications/
```

---

### Task 5: Raise the notification from the service

**Files:**
- Modify: `src/Umbraco.Community.AdvancedPermissions.Abstractions/Interfaces/IAdvancedPermissionService.cs`
- Modify: `src/Umbraco.Community.AdvancedPermissions/Services/AdvancedPermissionService.cs`
- Test: `tests/Umbraco.Community.AdvancedPermissions.Tests/AdvancedPermissionServiceNotificationTests.cs`

**The ordering rule lives here.** The notification is published *after* `cache.InvalidateRoleEntries`
and `cache.InvalidateAllResolved`, never before, and never from a separate handler racing them. A
client that refetches on an event which overtook the invalidation reads the very snapshot the
change was meant to replace — and, having consumed its notification, never asks again.

- [ ] **Step 1: Write the failing test**

Create `tests/Umbraco.Community.AdvancedPermissions.Tests/AdvancedPermissionServiceNotificationTests.cs`:

```csharp
using NSubstitute;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Caching;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Notifications;
using Umbraco.Community.AdvancedPermissions.Services;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests that permission writes publish <see cref="AdvancedPermissionsChangedNotification"/>, and
/// that they do so only after the caches have been invalidated.
/// </summary>
public sealed class AdvancedPermissionServiceNotificationTests
{
    /// <summary>A save publishes the notification carrying the node, user group and verbs written.</summary>
    [Fact]
    public async Task SaveEntriesAsync_Publishes_ChangedNotification()
    {
        var repository = Substitute.For<IAdvancedPermissionRepository>();
        var resolver = Substitute.For<IPermissionResolver>();
        var userService = Substitute.For<IUserService>();
        var cache = new AdvancedPermissionCache(/* match the existing test helpers in this project */);
        var aggregator = Substitute.For<IEventAggregator>();

        var service = new AdvancedPermissionService(repository, resolver, userService, cache, aggregator);
        var nodeKey = Guid.Parse("44444444-4444-4444-4444-444444444444");

        await service.SaveEntriesAsync(
            nodeKey,
            "editors",
            [("Umb.Document.Read", PermissionState.Allow, PermissionScope.ThisNodeOnly, false)],
            CancellationToken.None);

        await aggregator.Received(1).PublishAsync(
            Arg.Is<AdvancedPermissionsChangedNotification>(n =>
                n.NodeKey == nodeKey &&
                n.RoleAlias == "editors" &&
                n.Verbs.Count == 1 &&
                n.Verbs[0] == "Umb.Document.Read"),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Removing every entry still publishes, with an empty verb list.</summary>
    [Fact]
    public async Task SaveEntriesAsync_EmptyEntries_PublishesWithNoVerbs()
    {
        var repository = Substitute.For<IAdvancedPermissionRepository>();
        var resolver = Substitute.For<IPermissionResolver>();
        var userService = Substitute.For<IUserService>();
        var cache = new AdvancedPermissionCache(/* match the existing test helpers in this project */);
        var aggregator = Substitute.For<IEventAggregator>();

        var service = new AdvancedPermissionService(repository, resolver, userService, cache, aggregator);

        await service.SaveEntriesAsync(Guid.NewGuid(), "editors", [], CancellationToken.None);

        await aggregator.Received(1).PublishAsync(
            Arg.Is<AdvancedPermissionsChangedNotification>(n => n.Verbs.Count == 0),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The write must reach the repository before the notification goes out, so a handler that
    /// refetches cannot read the value the write was replacing.
    /// </summary>
    [Fact]
    public async Task SaveEntriesAsync_PublishesAfterRepositoryWrite()
    {
        var repository = Substitute.For<IAdvancedPermissionRepository>();
        var resolver = Substitute.For<IPermissionResolver>();
        var userService = Substitute.For<IUserService>();
        var cache = new AdvancedPermissionCache(/* match the existing test helpers in this project */);
        var aggregator = Substitute.For<IEventAggregator>();

        var order = new List<string>();
        repository
            .When(r => r.SaveAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<IEnumerable<(string, PermissionState, PermissionScope, bool)>>(),
                Arg.Any<CancellationToken>()))
            .Do(_ => order.Add("write"));
        aggregator
            .When(a => a.PublishAsync(Arg.Any<AdvancedPermissionsChangedNotification>(), Arg.Any<CancellationToken>()))
            .Do(_ => order.Add("publish"));

        var service = new AdvancedPermissionService(repository, resolver, userService, cache, aggregator);
        await service.SaveEntriesAsync(Guid.NewGuid(), "editors", [], CancellationToken.None);

        Assert.Equal(["write", "publish"], order);
    }

    /// <summary>Deleting a single entry publishes too, naming just that verb.</summary>
    [Fact]
    public async Task DeleteEntryAsync_Publishes_ChangedNotification()
    {
        var repository = Substitute.For<IAdvancedPermissionRepository>();
        var resolver = Substitute.For<IPermissionResolver>();
        var userService = Substitute.For<IUserService>();
        var cache = new AdvancedPermissionCache(/* match the existing test helpers in this project */);
        var aggregator = Substitute.For<IEventAggregator>();

        var service = new AdvancedPermissionService(repository, resolver, userService, cache, aggregator);

        await service.DeleteEntryAsync(Guid.NewGuid(), "editors", "Umb.Document.Read", CancellationToken.None);

        await aggregator.Received(1).PublishAsync(
            Arg.Is<AdvancedPermissionsChangedNotification>(n =>
                n.Verbs.Count == 1 && n.Verbs[0] == "Umb.Document.Read"),
            Arg.Any<CancellationToken>());
    }
}
```

Before running, open `tests/Umbraco.Community.AdvancedPermissions.Tests/AdvancedPermissionServiceTests.cs`
and copy how it constructs `AdvancedPermissionCache`, replacing the `/* match ... */` comments. Do
not invent a second way to build one.

- [ ] **Step 2: Run it to verify it fails**

```bash
dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests/ --filter AdvancedPermissionServiceNotificationTests
```

Expected: build failure — the constructor takes four arguments, not five.

- [ ] **Step 3: Inject the aggregator and publish**

In `src/Umbraco.Community.AdvancedPermissions/Services/AdvancedPermissionService.cs`, add
`using Umbraco.Cms.Core.Events;` and `using Umbraco.Community.AdvancedPermissions.Notifications;`
at the top, then extend the primary constructor and the two write methods:

```csharp
public sealed class AdvancedPermissionService(
    IAdvancedPermissionRepository repository,
    IPermissionResolver resolver,
    IUserService userService,
    AdvancedPermissionCache cache,
    IEventAggregator eventAggregator)
    : IAdvancedPermissionService
```

```csharp
    /// <inheritdoc />
    public async Task SaveEntriesAsync(
        Guid nodeKey,
        string roleAlias,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries,
        CancellationToken cancellationToken = default)
    {
        // Materialised once: the sequence is enumerated by the repository and again for the
        // notification's verb list, and a caller is entitled to hand us a lazy one.
        var materialised = entries as IList<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)>
            ?? entries.ToList();

        await repository.SaveAsync(nodeKey, roleAlias, materialised, cancellationToken);

        // Invalidate L1 for this role (entries changed), and ALL L2 (any user's resolution may be stale)
        cache.InvalidateRoleEntries(roleAlias);
        cache.InvalidateAllResolved();

        // Strictly after invalidation. A handler that refetches on this notification while the
        // cache still holds the old value would read the very snapshot this write replaced — and,
        // having consumed its notification, would never ask again.
        await eventAggregator.PublishAsync(
            new AdvancedPermissionsChangedNotification(
                nodeKey,
                roleAlias,
                materialised.Select(e => e.Verb).Distinct(StringComparer.Ordinal).ToList()),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteEntryAsync(
        Guid nodeKey,
        string roleAlias,
        string verb,
        CancellationToken cancellationToken = default)
    {
        await repository.DeleteAsync(nodeKey, roleAlias, verb, cancellationToken);

        // Invalidate L1 for this role (entries changed), and ALL L2 (any user's resolution may be stale)
        cache.InvalidateRoleEntries(roleAlias);
        cache.InvalidateAllResolved();

        await eventAggregator.PublishAsync(
            new AdvancedPermissionsChangedNotification(nodeKey, roleAlias, [verb]),
            cancellationToken);
    }
```

- [ ] **Step 4: Add `SaveManyAsync` to the service**

Append to `IAdvancedPermissionService`:

```csharp
    /// <summary>
    /// Replaces the entries for several node-and-user-group pairs in a single transaction,
    /// invalidating caches and publishing one notification per pair afterwards.
    /// </summary>
    /// <param name="batch">The node key, user group alias and replacement entries for each pair.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    Task SaveManyAsync(
        IReadOnlyList<(Guid NodeKey, string RoleAlias, IReadOnlyList<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries)> batch,
        CancellationToken cancellationToken = default);
```

And implement it in `AdvancedPermissionService`:

```csharp
    /// <inheritdoc />
    public async Task SaveManyAsync(
        IReadOnlyList<(Guid NodeKey, string RoleAlias, IReadOnlyList<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries)> batch,
        CancellationToken cancellationToken = default)
    {
        await repository.SaveManyAsync(
            batch.Select(b => (b.NodeKey, b.RoleAlias,
                (IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)>)b.Entries)),
            cancellationToken);

        // Invalidated once for the whole batch rather than per pair: L2 is dropped wholesale
        // anyway, and doing it per pair would drop it N times for no further effect.
        foreach (var roleAlias in batch.Select(b => b.RoleAlias).Distinct(StringComparer.Ordinal))
        {
            cache.InvalidateRoleEntries(roleAlias);
        }

        cache.InvalidateAllResolved();

        // Again strictly after invalidation, and one notification per pair so a handler sees the
        // same granularity it would from a single save.
        foreach (var (nodeKey, roleAlias, entries) in batch)
        {
            await eventAggregator.PublishAsync(
                new AdvancedPermissionsChangedNotification(
                    nodeKey,
                    roleAlias,
                    entries.Select(e => e.Verb).Distinct(StringComparer.Ordinal).ToList()),
                cancellationToken);
        }
    }
```

**Every publish is wrapped in a try/catch that logs and continues.** Inject
`ILogger<AdvancedPermissionService>` for it. By the time the publish runs, the write has committed
and the cache has been invalidated — the change is already visible to everyone else. Letting a
handler's exception propagate would return a 500 to the editor for a save that actually succeeded,
and they would retry or reload and lose work over nothing. A failed notification means the live
update silently does not go out, which is degraded but true; a false-negative save result is not.
In `SaveManyAsync` the catch goes **inside** the loop, so one bad pair does not silence the rest.
`OperationCanceledException` is deliberately not caught: a cancelled request is not a handler
failure, and swallowing it would hide a genuine cancellation.

`SaveManyAsync` returns early on an empty batch, before touching the repository or the cache.
Otherwise it opens a connection, commits a no-op transaction, and flushes the whole
resolved-permission cache on behalf of a call that changed nothing.


- [ ] **Step 5: Run the tests**

```bash
dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests/ --filter AdvancedPermissionServiceNotificationTests
dotnet build
```

Expected: 4 passed, build clean.

- [ ] **Step 6: Stage**

```bash
git add src/Umbraco.Community.AdvancedPermissions.Abstractions/Interfaces/IAdvancedPermissionService.cs src/Umbraco.Community.AdvancedPermissions/Services/AdvancedPermissionService.cs tests/Umbraco.Community.AdvancedPermissions.Tests/AdvancedPermissionServiceNotificationTests.cs
```

---

### Task 6: The batch endpoint with the stamp check

**Files:**
- Create: `src/Umbraco.Community.AdvancedPermissions/Controllers/Models/BatchSavePermissionsModels.cs`
- Modify: `src/Umbraco.Community.AdvancedPermissions/Controllers/AdvancedPermissionsPermissionController.cs`
- Test: `tests/Umbraco.Community.AdvancedPermissions.Tests/BatchSavePermissionsTests.cs`

- [ ] **Step 1: Create the models**

```csharp
namespace Umbraco.Community.AdvancedPermissions.Controllers.Models;

/// <summary>
/// A request to replace permission entries for several node-and-user-group pairs at once.
/// </summary>
/// <remarks>
/// Applied all or nothing. The editors change several nodes before saving, and writing some of
/// them while refusing others would leave a state nothing afterwards could interpret.
/// </remarks>
/// <param name="Nodes">The pairs to write.</param>
/// <param name="Force">
/// Whether to skip the concurrency check entirely. Sent only after a user has been shown what
/// they would overwrite and has confirmed.
/// </param>
public sealed record BatchSavePermissionsRequestModel(
    IReadOnlyList<BatchSavePermissionsNode> Nodes,
    bool Force = false);

/// <summary>
/// One node-and-user-group pair within a batch save.
/// </summary>
/// <param name="NodeKey">
/// The content node key. Use <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> for
/// virtual-root entries.
/// </param>
/// <param name="RoleAlias">The user group alias, or <c>$everyone</c> for the All Users Group.</param>
/// <param name="Entries">The replacement entries. An empty list removes all entries for the pair.</param>
/// <param name="ExpectedStamp">
/// The stamp this client was given when it read these entries. When null the concurrency check is
/// skipped for this pair, which is how an older client keeps working.
/// </param>
public sealed record BatchSavePermissionsNode(
    Guid NodeKey,
    string RoleAlias,
    IReadOnlyList<SavePermissionEntryItem> Entries,
    string? ExpectedStamp = null);

/// <summary>
/// The body of the <c>409 Conflict</c> a batch save is refused with.
/// </summary>
/// <param name="Conflicts">
/// Only the pairs whose stored entries moved. Pairs that would have written cleanly are not
/// listed, because nothing was written and there is nothing to say about them.
/// </param>
public sealed record BatchSaveConflictResponseModel(
    IReadOnlyList<BatchSaveConflict> Conflicts);

/// <summary>
/// One pair whose stored entries no longer match what the client loaded.
/// </summary>
/// <param name="NodeKey">The content node key.</param>
/// <param name="RoleAlias">The user group alias.</param>
/// <param name="CurrentEntries">
/// What is stored right now. The client renders "stored X, yours Y" from this rather than from
/// what it guessed when the change notification arrived.
/// </param>
/// <param name="CurrentStamp">
/// The stamp of <paramref name="CurrentEntries"/>, so a client that resolves the conflict can
/// retry without a further read.
/// </param>
public sealed record BatchSaveConflict(
    Guid NodeKey,
    string RoleAlias,
    IReadOnlyList<PermissionEntryResponseModel> CurrentEntries,
    string CurrentStamp);

/// <summary>
/// One pair's new stamp after a successful batch save.
/// </summary>
/// <remarks>
/// Its own type rather than a <see cref="BatchSaveConflict"/> with an empty entry list: a success
/// response shaped like a conflict reads as one at every call site that handles it, and the client
/// would be pulling a field named "current" off an object named "conflict" on the happy path.
/// </remarks>
/// <param name="NodeKey">The content node key.</param>
/// <param name="RoleAlias">The user group alias.</param>
/// <param name="Stamp">
/// The stamp of what was just written, so the client can keep editing without re-reading.
/// </param>
public sealed record BatchSavedStamp(
    Guid NodeKey,
    string RoleAlias,
    string Stamp);
```

- [ ] **Step 2: Write the failing tests**

Create `tests/Umbraco.Community.AdvancedPermissions.Tests/BatchSavePermissionsTests.cs`. Follow the
construction pattern in the existing `AdvancedPermissionsEffectiveControllerTests.cs` for building
the controller with substitutes.

```csharp
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Umbraco.Community.AdvancedPermissions.Controllers;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Concurrency;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests the batch save endpoint: the concurrency check, the shape of the refusal, the force
/// bypass, and that a refused batch writes nothing at all.
/// </summary>
public sealed class BatchSavePermissionsTests
{
    private static readonly Guid NodeA = Guid.Parse("55555555-5555-5555-5555-555555555555");

    /// <summary>Builds a stored entry for the given verb.</summary>
    /// <param name="verb">The verb.</param>
    /// <param name="state">The state.</param>
    /// <returns>The entry.</returns>
    private static AdvancedPermissionEntry Stored(string verb, PermissionState state = PermissionState.Allow) =>
        new()
        {
            Id = Guid.NewGuid(),
            NodeKey = NodeA,
            RoleAlias = "editors",
            Verb = verb,
            State = state,
            Scope = PermissionScope.ThisNodeOnly,
            IsPriorityOverride = false,
        };

    /// <summary>A matching stamp saves, and the write reaches the service as one batch.</summary>
    [Fact]
    public async Task BatchSave_MatchingStamp_Saves()
    {
        var service = Substitute.For<IAdvancedPermissionService>();
        var stored = new[] { Stored("Umb.Document.Read") };
        service.GetEntriesAsync(NodeA, "editors", Arg.Any<CancellationToken>()).Returns(stored);

        var controller = BuildController(service);
        var request = new BatchSavePermissionsRequestModel(
        [
            new BatchSavePermissionsNode(
                NodeA,
                "editors",
                [new SavePermissionEntryItem("Umb.Document.Publish", "Allow", "ThisNodeOnly")],
                PermissionStamp.Compute(stored)),
        ]);

        var result = await controller.BatchSavePermissions(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        await service.Received(1).SaveManyAsync(
            Arg.Any<IReadOnlyList<(Guid, string, IReadOnlyList<(string, PermissionState, PermissionScope, bool)>)>>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>A stale stamp is refused with 409, and nothing is written.</summary>
    [Fact]
    public async Task BatchSave_StaleStamp_Returns409AndWritesNothing()
    {
        var service = Substitute.For<IAdvancedPermissionService>();
        service.GetEntriesAsync(NodeA, "editors", Arg.Any<CancellationToken>())
            .Returns(new[] { Stored("Umb.Document.Read", PermissionState.Deny) });

        var controller = BuildController(service);
        var request = new BatchSavePermissionsRequestModel(
        [
            new BatchSavePermissionsNode(
                NodeA,
                "editors",
                [new SavePermissionEntryItem("Umb.Document.Publish", "Allow", "ThisNodeOnly")],
                PermissionStamp.Compute([Stored("Umb.Document.Read")])),
        ]);

        var result = await controller.BatchSavePermissions(request, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var body = Assert.IsType<BatchSaveConflictResponseModel>(conflict.Value);
        Assert.Single(body.Conflicts);
        Assert.Equal(NodeA, body.Conflicts[0].NodeKey);
        Assert.Single(body.Conflicts[0].CurrentEntries);
        Assert.Equal("Deny", body.Conflicts[0].CurrentEntries[0].State);

        await service.DidNotReceive().SaveManyAsync(
            Arg.Any<IReadOnlyList<(Guid, string, IReadOnlyList<(string, PermissionState, PermissionScope, bool)>)>>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>One stale pair in a batch refuses the whole batch, not just that pair.</summary>
    [Fact]
    public async Task BatchSave_OneStalePair_RefusesWholeBatch()
    {
        var nodeB = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var service = Substitute.For<IAdvancedPermissionService>();
        var freshA = new[] { Stored("Umb.Document.Read") };
        service.GetEntriesAsync(NodeA, "editors", Arg.Any<CancellationToken>()).Returns(freshA);
        service.GetEntriesAsync(nodeB, "editors", Arg.Any<CancellationToken>())
            .Returns(new[] { Stored("Umb.Document.Read", PermissionState.Deny) });

        var controller = BuildController(service);
        var request = new BatchSavePermissionsRequestModel(
        [
            new BatchSavePermissionsNode(NodeA, "editors", [], PermissionStamp.Compute(freshA)),
            new BatchSavePermissionsNode(nodeB, "editors", [], PermissionStamp.Compute([])),
        ]);

        var result = await controller.BatchSavePermissions(request, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var body = Assert.IsType<BatchSaveConflictResponseModel>(conflict.Value);
        Assert.Single(body.Conflicts);
        Assert.Equal(nodeB, body.Conflicts[0].NodeKey);

        await service.DidNotReceive().SaveManyAsync(
            Arg.Any<IReadOnlyList<(Guid, string, IReadOnlyList<(string, PermissionState, PermissionScope, bool)>)>>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Force writes through a stale stamp without asking.</summary>
    [Fact]
    public async Task BatchSave_Force_SavesDespiteStaleStamp()
    {
        var service = Substitute.For<IAdvancedPermissionService>();
        service.GetEntriesAsync(NodeA, "editors", Arg.Any<CancellationToken>())
            .Returns(new[] { Stored("Umb.Document.Read", PermissionState.Deny) });

        var controller = BuildController(service);
        var request = new BatchSavePermissionsRequestModel(
            [new BatchSavePermissionsNode(NodeA, "editors", [], "a-stamp-that-does-not-match")],
            Force: true);

        var result = await controller.BatchSavePermissions(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        await service.Received(1).SaveManyAsync(
            Arg.Any<IReadOnlyList<(Guid, string, IReadOnlyList<(string, PermissionState, PermissionScope, bool)>)>>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>A null stamp means an older client, which keeps its previous behaviour.</summary>
    [Fact]
    public async Task BatchSave_NullStamp_SkipsCheck()
    {
        var service = Substitute.For<IAdvancedPermissionService>();
        service.GetEntriesAsync(NodeA, "editors", Arg.Any<CancellationToken>())
            .Returns(new[] { Stored("Umb.Document.Read", PermissionState.Deny) });

        var controller = BuildController(service);
        var request = new BatchSavePermissionsRequestModel(
            [new BatchSavePermissionsNode(NodeA, "editors", [], ExpectedStamp: null)]);

        var result = await controller.BatchSavePermissions(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    /// <summary>An unrecognised verb is still rejected as a bad request, before any stamp work.</summary>
    [Fact]
    public async Task BatchSave_InvalidVerb_ReturnsBadRequest()
    {
        var service = Substitute.For<IAdvancedPermissionService>();
        var controller = BuildController(service);
        var request = new BatchSavePermissionsRequestModel(
        [
            new BatchSavePermissionsNode(
                NodeA,
                "editors",
                [new SavePermissionEntryItem("Not.A.Real.Verb", "Allow", "ThisNodeOnly")]),
        ]);

        var result = await controller.BatchSavePermissions(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>
    /// Builds the controller under test. Mirror the substitute set-up used by
    /// <c>AdvancedPermissionsEffectiveControllerTests</c> for the repository and entity service.
    /// </summary>
    /// <param name="service">The permission service substitute.</param>
    /// <returns>The controller.</returns>
    private static AdvancedPermissionsPermissionController BuildController(IAdvancedPermissionService service) =>
        new(service,
            Substitute.For<IAdvancedPermissionRepository>(),
            Substitute.For<global::Umbraco.Cms.Core.Services.IEntityService>());
}
```

- [ ] **Step 3: Run to verify it fails**

```bash
dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests/ --filter BatchSavePermissionsTests
```

Expected: build failure — `BatchSavePermissions` does not exist.

- [ ] **Step 4: Extract the entry validation**

`SavePermissions` already validates state, scope and verb inline. The batch endpoint needs the same
rules, and duplicating them is how the two drift apart. Add this private helper to
`AdvancedPermissionsPermissionController` and rewrite `SavePermissions`'s loop to call it:

```csharp
    /// <summary>
    /// Validates and maps the entries of one save, or produces the problem describing why it
    /// cannot be mapped.
    /// </summary>
    /// <param name="items">The raw entries from the request.</param>
    /// <param name="mapped">The mapped entries, when validation succeeds.</param>
    /// <param name="problem">The problem to return, when it does not.</param>
    /// <returns><see langword="true"/> when every entry is valid.</returns>
    private static bool TryMapEntries(
        IReadOnlyList<SavePermissionEntryItem> items,
        out List<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> mapped,
        out ProblemDetails? problem)
    {
        mapped = [];
        problem = null;

        foreach (var entry in items)
        {
            if (!Enum.TryParse<PermissionState>(entry.State, ignoreCase: true, out var state))
            {
                problem = new ProblemDetails
                {
                    Title = "Invalid state",
                    Detail = $"'{entry.State}' is not a valid permission state. Use 'Allow' or 'Deny'.",
                    Status = StatusCodes.Status400BadRequest,
                };
                return false;
            }

            if (!Enum.TryParse<PermissionScope>(entry.Scope, ignoreCase: true, out var scope))
            {
                problem = new ProblemDetails
                {
                    Title = "Invalid scope",
                    Detail = $"'{entry.Scope}' is not a valid permission scope. Use 'ThisNodeOnly', 'ThisNodeAndDescendants', or 'DescendantsOnly'.",
                    Status = StatusCodes.Status400BadRequest,
                };
                return false;
            }

            if (!AdvancedPermissionsConstants.AllVerbs.Contains(entry.Verb, StringComparer.Ordinal))
            {
                problem = new ProblemDetails
                {
                    Title = "Invalid verb",
                    Detail = $"'{entry.Verb}' is not a recognized permission verb.",
                    Status = StatusCodes.Status400BadRequest,
                };
                return false;
            }

            mapped.Add((entry.Verb, state, scope, entry.IsPriorityOverride));
        }

        return true;
    }
```

- [ ] **Step 5: Add the endpoint**

```csharp
    /// <summary>
    /// Replaces permission entries for several nodes and user groups at once, refusing the whole
    /// batch if any pair has changed since the client read it.
    /// </summary>
    /// <remarks>
    /// All or nothing, deliberately. The editors change several nodes before saving, and a partial
    /// write would leave a state nothing afterwards could interpret — not the client's, not the
    /// server's, and not the next person's.
    /// </remarks>
    /// <param name="request">The pairs to write, each with the stamp the client read.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>
    /// <see cref="StatusCodes.Status200OK"/> with the new stamp per pair, or
    /// <see cref="StatusCodes.Status409Conflict"/> naming the pairs that moved.
    /// </returns>
    [HttpPut("permissions/batch")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<IReadOnlyList<BatchSavedStamp>>(StatusCodes.Status200OK)]
    [ProducesResponseType<BatchSaveConflictResponseModel>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [EndpointSummary("Saves permission entries for several nodes at once, all or nothing.")]
    public async Task<IActionResult> BatchSavePermissions(
        [FromBody] BatchSavePermissionsRequestModel request,
        CancellationToken cancellationToken)
    {
        // Validate everything first. A batch that cannot be mapped is a client bug, not a
        // conflict, and reporting it as one would send the user to a dialog about somebody
        // else's changes when nobody else has changed anything.
        var pending = new List<(Guid NodeKey, string RoleAlias, IReadOnlyList<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries)>();

        foreach (var node in request.Nodes)
        {
            if (!TryMapEntries(node.Entries, out var mapped, out var problem))
            {
                return BadRequest(problem);
            }

            pending.Add((node.NodeKey, node.RoleAlias, mapped));
        }

        if (!request.Force)
        {
            var conflicts = new List<BatchSaveConflict>();

            foreach (var node in request.Nodes)
            {
                if (node.ExpectedStamp is null)
                {
                    continue;
                }

                var stored = await permissionService.GetEntriesAsync(node.NodeKey, node.RoleAlias, cancellationToken);
                var currentStamp = PermissionStamp.Compute(stored);

                if (string.Equals(currentStamp, node.ExpectedStamp, StringComparison.Ordinal))
                {
                    continue;
                }

                conflicts.Add(new BatchSaveConflict(
                    node.NodeKey,
                    node.RoleAlias,
                    stored.Select(MapEntry).ToList(),
                    currentStamp));
            }

            if (conflicts.Count > 0)
            {
                return Conflict(new BatchSaveConflictResponseModel(conflicts));
            }
        }

        await permissionService.SaveManyAsync(pending, cancellationToken);

        // The new stamps, so the client can keep editing without a further read. Computed from
        // what was written rather than re-read, because the write just made them equal and a
        // second round trip per node would buy nothing.
        var saved = request.Nodes
            .Select(n => new BatchSavedStamp(
                n.NodeKey,
                n.RoleAlias,
                PermissionStamp.ComputeFromNames(
                    n.Entries.Select(e => (e.Verb, e.State, e.Scope, e.IsPriorityOverride)))))
            .ToList();

        return Ok(saved);
    }
```

Add `using Umbraco.Community.AdvancedPermissions.Core.Concurrency;` to the file.

- [ ] **Step 6: Add the optional stamp to the single PUT**

In `SavePermissionsRequestModel`, append two parameters so existing callers are unaffected:

```csharp
/// <param name="ExpectedStamp">
/// The stamp this client was given when it read these entries. When null the concurrency check is
/// skipped, which is how a client written before stamps existed keeps working.
/// </param>
/// <param name="Force">Whether to write through a stale stamp. Only sent after a user confirms.</param>
public sealed record SavePermissionsRequestModel(
    Guid NodeKey,
    string RoleAlias,
    IReadOnlyList<SavePermissionEntryItem> Entries,
    string? ExpectedStamp = null,
    bool Force = false);
```

And in `SavePermissions`, after `TryMapEntries` succeeds and before `SaveEntriesAsync`:

```csharp
        if (!request.Force && request.ExpectedStamp is not null)
        {
            var stored = await permissionService.GetEntriesAsync(request.NodeKey, request.RoleAlias, cancellationToken);
            var currentStamp = PermissionStamp.Compute(stored);

            if (!string.Equals(currentStamp, request.ExpectedStamp, StringComparison.Ordinal))
            {
                return Conflict(new BatchSaveConflictResponseModel(
                [
                    new BatchSaveConflict(
                        request.NodeKey,
                        request.RoleAlias,
                        stored.Select(MapEntry).ToList(),
                        currentStamp),
                ]));
            }
        }
```

- [ ] **Step 7: Run the tests**

```bash
dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests/ --filter BatchSavePermissionsTests
dotnet test
```

Expected: 6 passed in the filter, and the full suite still green.

- [ ] **Step 8: Stage**

```bash
git add src/Umbraco.Community.AdvancedPermissions/Controllers/ tests/Umbraco.Community.AdvancedPermissions.Tests/BatchSavePermissionsTests.cs
```

---

## Phase 3 — server events

### Task 7: Event-source constants and the authorizer

**Files:**
- Create: `src/Umbraco.Community.AdvancedPermissions/ServerEvents/AdvancedPermissionsServerEvents.cs`
- Create: `src/Umbraco.Community.AdvancedPermissions/ServerEvents/AdvancedPermissionsEventAuthorizer.cs`
- Test: `tests/Umbraco.Community.AdvancedPermissions.Tests/AdvancedPermissionsEventAuthorizerTests.cs`

- [ ] **Step 1: Create the constants**

```csharp
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
```

- [ ] **Step 2: Write the failing test**

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Community.AdvancedPermissions.ServerEvents;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests that this package's server-event sources are delivered only to users who may already see
/// the permission data they describe.
/// </summary>
public sealed class AdvancedPermissionsEventAuthorizerTests
{
    /// <summary>The authorizer must claim every source the package publishes.</summary>
    /// <remarks>
    /// A source no authorizer claims is never delivered by core, so a source missing from this
    /// list disables its feature completely and silently.
    /// </remarks>
    [Fact]
    public void AuthorizableEventSources_CoversEverySourceThePackagePublishes()
    {
        var authorizer = new AdvancedPermissionsEventAuthorizer(Substitute.For<IAuthorizationService>());

        Assert.Equal(
            AdvancedPermissionsServerEvents.AllSources.OrderBy(s => s, StringComparer.Ordinal),
            authorizer.AuthorizableEventSources.OrderBy(s => s, StringComparer.Ordinal));
    }

    /// <summary>A principal that satisfies the Users-section policy receives the source.</summary>
    [Fact]
    public async Task AuthorizeAsync_UsersSectionPrincipal_IsAuthorized()
    {
        var authorizationService = Substitute.For<IAuthorizationService>();
        authorizationService
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), AuthorizationPolicies.SectionAccessUsers)
            .Returns(AuthorizationResult.Success());

        var authorizer = new AdvancedPermissionsEventAuthorizer(authorizationService);

        Assert.True(await authorizer.AuthorizeAsync(
            new ClaimsPrincipal(),
            AdvancedPermissionsServerEvents.NodePermissionsSource));
    }

    /// <summary>A principal without Users-section access receives nothing.</summary>
    [Fact]
    public async Task AuthorizeAsync_WithoutUsersSection_IsRefused()
    {
        var authorizationService = Substitute.For<IAuthorizationService>();
        authorizationService
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), AuthorizationPolicies.SectionAccessUsers)
            .Returns(AuthorizationResult.Failed());

        var authorizer = new AdvancedPermissionsEventAuthorizer(authorizationService);

        Assert.False(await authorizer.AuthorizeAsync(
            new ClaimsPrincipal(),
            AdvancedPermissionsServerEvents.AccessSource));
    }
}
```

- [ ] **Step 3: Run it to verify it fails**

```bash
dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests/ --filter AdvancedPermissionsEventAuthorizerTests
```

Expected: build failure — `AdvancedPermissionsEventAuthorizer` does not exist.

- [ ] **Step 4: Implement the authorizer**

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Umbraco.Cms.Core.ServerEvents;
using Umbraco.Cms.Web.Common.Authorization;

namespace Umbraco.Community.AdvancedPermissions.ServerEvents;

/// <summary>
/// Decides who receives this package's server events.
/// </summary>
/// <remarks>
/// <para>
/// Gated on the same <c>SectionAccessUsers</c> policy the mutating controllers already require, so
/// the events tell a user nothing they could not already read. There is no second place where this
/// can be got wrong: core delivers no source that no authorizer claims, so this class is the whole
/// access story for the feature.
/// </para>
/// <para>
/// Content editors without Users-section access therefore receive nothing at all. Pushing
/// permission changes into an ordinary editor's own session is a larger question about what an
/// editor may learn of the permission tree, and belongs with the client-side effective-permission
/// cache work rather than here.
/// </para>
/// </remarks>
/// <param name="authorizationService">Used to evaluate the Users-section policy.</param>
public sealed class AdvancedPermissionsEventAuthorizer(IAuthorizationService authorizationService)
    : IEventSourceAuthorizer
{
    /// <inheritdoc />
    public IEnumerable<string> AuthorizableEventSources => AdvancedPermissionsServerEvents.AllSources;

    /// <inheritdoc />
    public async Task<bool> AuthorizeAsync(ClaimsPrincipal principal, string eventSource)
    {
        var result = await authorizationService.AuthorizeAsync(principal, AuthorizationPolicies.SectionAccessUsers);
        return result.Succeeded;
    }
}
```

- [ ] **Step 5: Run the tests**

```bash
dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests/ --filter AdvancedPermissionsEventAuthorizerTests
```

Expected: 3 passed.

- [ ] **Step 6: Stage**

```bash
git add src/Umbraco.Community.AdvancedPermissions/ServerEvents/ tests/Umbraco.Community.AdvancedPermissions.Tests/AdvancedPermissionsEventAuthorizerTests.cs
```

---

### Task 8: The event handlers

**Files:**
- Create: `src/Umbraco.Community.AdvancedPermissions/ServerEvents/AdvancedPermissionsServerEventHandler.cs`
- Create: `src/Umbraco.Community.AdvancedPermissions/ServerEvents/AccessServerEventHandler.cs`
- Test: `tests/Umbraco.Community.AdvancedPermissions.Tests/ServerEventHandlerTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using NSubstitute;
using Umbraco.Cms.Core.Models.ServerEvents;
using Umbraco.Cms.Core.ServerEvents;
using Umbraco.Community.AdvancedPermissions.Notifications;
using Umbraco.Community.AdvancedPermissions.ServerEvents;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests that this package's notifications reach the server-event hub under the right source and key.
/// </summary>
public sealed class ServerEventHandlerTests
{
    /// <summary>A permission change is routed under the node-permissions source, keyed by node.</summary>
    [Fact]
    public async Task PermissionChanged_RoutesNodePermissionsEvent()
    {
        var router = Substitute.For<IServerEventRouter>();
        var handler = new AdvancedPermissionsServerEventHandler(router);
        var nodeKey = Guid.Parse("77777777-7777-7777-7777-777777777777");

        await handler.HandleAsync(
            new AdvancedPermissionsChangedNotification(nodeKey, "editors", ["Umb.Document.Read"]),
            CancellationToken.None);

        await router.Received(1).RouteEventAsync(Arg.Is<ServerEvent>(e =>
            e.EventSource == AdvancedPermissionsServerEvents.NodePermissionsSource &&
            e.EventType == AdvancedPermissionsServerEvents.EventType.Updated &&
            e.Key == nodeKey));
    }

    /// <summary>A document-type change is routed under its own source.</summary>
    [Fact]
    public async Task DocTypePermissionChanged_RoutesDocTypePermissionsEvent()
    {
        var router = Substitute.For<IServerEventRouter>();
        var handler = new AdvancedPermissionsServerEventHandler(router);
        var nodeKey = Guid.Parse("88888888-8888-8888-8888-888888888888");

        await handler.HandleAsync(
            new DocTypePermissionsChangedNotification(nodeKey, "editors", [Guid.NewGuid()]),
            CancellationToken.None);

        await router.Received(1).RouteEventAsync(Arg.Is<ServerEvent>(e =>
            e.EventSource == AdvancedPermissionsServerEvents.DocTypePermissionsSource &&
            e.Key == nodeKey));
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

```bash
dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests/ --filter ServerEventHandlerTests
```

Expected: build failure.

- [ ] **Step 3: Implement the package's own handler**

```csharp
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.ServerEvents;
using Umbraco.Cms.Core.ServerEvents;
using Umbraco.Community.AdvancedPermissions.Notifications;

namespace Umbraco.Community.AdvancedPermissions.ServerEvents;

/// <summary>
/// Puts this package's own permission changes onto Umbraco's built-in server-events hub, so open
/// editors and viewers hear about a write made from another window, by another user.
/// </summary>
/// <remarks>
/// <para>
/// Subscribes to this package's notifications rather than reaching into the services that raise
/// them, which keeps the transport a consumer of the same public seam anybody else would use.
/// </para>
/// <para>
/// The notifications are raised after the caches have been invalidated, and that ordering is what
/// makes this safe. A client refetching on an event that overtook the invalidation would read the
/// snapshot the write replaced and — having consumed its one notification — never ask again.
/// </para>
/// </remarks>
/// <param name="router">Umbraco's server-event router, registered by the management API.</param>
public sealed class AdvancedPermissionsServerEventHandler(IServerEventRouter router) :
    INotificationAsyncHandler<AdvancedPermissionsChangedNotification>,
    INotificationAsyncHandler<DocTypePermissionsChangedNotification>
{
    /// <inheritdoc />
    public Task HandleAsync(AdvancedPermissionsChangedNotification notification, CancellationToken cancellationToken) =>
        router.RouteEventAsync(new ServerEvent
        {
            EventType = AdvancedPermissionsServerEvents.EventType.Updated,
            EventSource = AdvancedPermissionsServerEvents.NodePermissionsSource,
            Key = notification.NodeKey,
        });

    /// <inheritdoc />
    public Task HandleAsync(DocTypePermissionsChangedNotification notification, CancellationToken cancellationToken) =>
        router.RouteEventAsync(new ServerEvent
        {
            EventType = AdvancedPermissionsServerEvents.EventType.Updated,
            EventSource = AdvancedPermissionsServerEvents.DocTypePermissionsSource,
            Key = notification.NodeKey,
        });
}
```

- [ ] **Step 4: Implement the Access handler**

```csharp
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.ServerEvents;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.ServerEvents;

namespace Umbraco.Community.AdvancedPermissions.ServerEvents;

/// <summary>
/// Puts the Umbraco-side changes that shift effective permissions onto the server-events hub.
/// </summary>
/// <remarks>
/// <para>
/// Effective permissions move without this package's own store being touched at all: a user
/// joining a group, a group being deleted, a node moving to a new parent and inheriting a
/// different branch. A viewer showing a user whose group membership just changed is exactly as
/// wrong as one showing a node whose entries changed, and until now nothing told it so.
/// </para>
/// <para>
/// <b>Ordering.</b> These notifications also drive <c>AdvancedPermissionCacheInvalidator</c>. This
/// handler must run after it, so a client refetching on the event cannot read the cached value the
/// change invalidated. Umbraco runs handlers for one notification in registration order, so
/// <c>AdvancedPermissionsComposer</c> registers the invalidator first — see the comment there.
/// </para>
/// </remarks>
/// <param name="router">Umbraco's server-event router, registered by the management API.</param>
public sealed class AccessServerEventHandler(IServerEventRouter router) :
    INotificationAsyncHandler<UserGroupSavedNotification>,
    INotificationAsyncHandler<UserGroupDeletedNotification>,
    INotificationAsyncHandler<UserSavedNotification>,
    INotificationAsyncHandler<ContentMovedNotification>,
    INotificationAsyncHandler<ContentMovedToRecycleBinNotification>,
    INotificationAsyncHandler<ContentDeletedNotification>
{
    /// <inheritdoc />
    public Task HandleAsync(UserGroupSavedNotification notification, CancellationToken cancellationToken) =>
        RouteAllAsync(notification.SavedEntities.Select(e => e.Key));

    /// <inheritdoc />
    public Task HandleAsync(UserGroupDeletedNotification notification, CancellationToken cancellationToken) =>
        RouteAllAsync(notification.DeletedEntities.Select(e => e.Key));

    /// <inheritdoc />
    public Task HandleAsync(UserSavedNotification notification, CancellationToken cancellationToken) =>
        RouteAllAsync(notification.SavedEntities.Select(e => e.Key));

    /// <inheritdoc />
    public Task HandleAsync(ContentMovedNotification notification, CancellationToken cancellationToken) =>
        RouteAllAsync(notification.MoveInfoCollection.Select(m => m.Entity.Key));

    /// <inheritdoc />
    public Task HandleAsync(ContentMovedToRecycleBinNotification notification, CancellationToken cancellationToken) =>
        RouteAllAsync(notification.MoveInfoCollection.Select(m => m.Entity.Key));

    /// <inheritdoc />
    public Task HandleAsync(ContentDeletedNotification notification, CancellationToken cancellationToken) =>
        RouteAllAsync(notification.DeletedEntities.Select(e => e.Key));

    /// <summary>
    /// Routes one <c>Access</c> event per key.
    /// </summary>
    /// <remarks>
    /// One event per entity rather than one per operation, because a client matches on the key and
    /// a single summary event would match nothing. Bulk operations therefore produce bursts, which
    /// is why every client-side consumer coalesces.
    /// </remarks>
    /// <param name="keys">The entity keys that changed.</param>
    /// <returns>A task that completes when every event has been routed.</returns>
    private async Task RouteAllAsync(IEnumerable<Guid> keys)
    {
        foreach (var key in keys)
        {
            await router.RouteEventAsync(new ServerEvent
            {
                EventType = AdvancedPermissionsServerEvents.EventType.Updated,
                EventSource = AdvancedPermissionsServerEvents.AccessSource,
                Key = key,
            });
        }
    }
}
```

Check each notification's collection property name against the v17 source at
`D:/github/UmbracoVersions/v17/Umbraco-CMS/src/Umbraco.Core/Notifications/` before building —
`SavedEntities`, `DeletedEntities` and `MoveInfoCollection` are the expected names but verify
rather than assume.

- [ ] **Step 5: Run the tests**

```bash
dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests/ --filter ServerEventHandlerTests
dotnet build
```

Expected: 2 passed, build clean.

- [ ] **Step 6: Stage**

```bash
git add src/Umbraco.Community.AdvancedPermissions/ServerEvents/ tests/Umbraco.Community.AdvancedPermissions.Tests/ServerEventHandlerTests.cs
```

---

### Task 9: Register everything

**Files:**
- Modify: `src/Umbraco.Community.AdvancedPermissions/Composing/AdvancedPermissionsComposer.cs`

- [ ] **Step 1: Register the authorizer**

In `RegisterServices`, after the doc-type registrations:

```csharp
        // Server events: publish this package's changes on Umbraco's built-in hub. The authorizer
        // is what makes the sources reachable at all — core delivers no source that no authorizer
        // claims — so this line and AdvancedPermissionsEventAuthorizer are the whole access story.
        builder.EventSourceAuthorizers().Append<AdvancedPermissionsEventAuthorizer>();
```

- [ ] **Step 2: Register the handlers, after the invalidators**

In `RegisterNotificationHandlers`, append at the very end of the method:

```csharp
        // Server-event handlers. Registered LAST, and that position is load-bearing: Umbraco runs
        // the handlers for one notification in registration order, and every notification below is
        // also handled by a cache invalidator above. A client that refetched on an event which
        // overtook its invalidation would read the snapshot the change replaced — and, having
        // consumed its one notification, would never ask again. Do not move these up.
        builder.AddNotificationAsyncHandler<AdvancedPermissionsChangedNotification, AdvancedPermissionsServerEventHandler>();
        builder.AddNotificationAsyncHandler<DocTypePermissionsChangedNotification, AdvancedPermissionsServerEventHandler>();
        builder.AddNotificationAsyncHandler<UserGroupSavedNotification, AccessServerEventHandler>();
        builder.AddNotificationAsyncHandler<UserGroupDeletedNotification, AccessServerEventHandler>();
        builder.AddNotificationAsyncHandler<UserSavedNotification, AccessServerEventHandler>();
        builder.AddNotificationAsyncHandler<ContentMovedNotification, AccessServerEventHandler>();
        builder.AddNotificationAsyncHandler<ContentMovedToRecycleBinNotification, AccessServerEventHandler>();
        builder.AddNotificationAsyncHandler<ContentDeletedNotification, AccessServerEventHandler>();
```

Add `using Umbraco.Community.AdvancedPermissions.ServerEvents;` to the file.

- [ ] **Step 3: Verify the whole suite**

```bash
dotnet build
dotnet test
```

Expected: build clean, all tests pass.

- [ ] **Step 4: Verify the hub end-to-end by hand**

```bash
cd tests/Umbraco.Community.AdvancedPermissions.TestSite && dotnet run
```

Sign in to the backoffice, open dev tools, and confirm the WebSocket to `/umbraco/serverEventHub`
is open. Change a permission in another browser and confirm a frame arrives carrying
`AdvancedPermissions:NodePermissions`. Nothing in the UI reacts yet — that is Phase 4 — but if no
frame arrives, the source name, the authorizer or the registration is wrong, and every client task
that follows would be debugging the wrong layer.

- [ ] **Step 5: Stage**

```bash
git add src/Umbraco.Community.AdvancedPermissions/Composing/AdvancedPermissionsComposer.cs
```

---

## Phase 4 — client foundations

### Task 10: vitest

**Files:**
- Create: `src/Umbraco.Community.AdvancedPermissions.Client/vitest.config.ts`
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/package.json`

Only the pure modules are tested — the classifier and the coalescer. They are where a mistake
silently loses somebody's work; the Lit rendering around them is not, and a DOM harness would cost
more to keep working than the bugs it would catch.

- [ ] **Step 1: Add the dependency and script**

```bash
cd src/Umbraco.Community.AdvancedPermissions.Client && npm install --save-dev vitest@^3
```

Then add to the `scripts` block in `package.json`, after `typecheck`:

```json
    "test": "vitest run",
    "test:watch": "vitest"
```

- [ ] **Step 2: Create the config**

`src/Umbraco.Community.AdvancedPermissions.Client/vitest.config.ts`:

```typescript
import { defineConfig } from 'vitest/config';

// Only the pure modules under src/live are tested. Everything else in this package is a Lit
// element whose behaviour is the DOM, and a browser harness for those would cost more to keep
// working than the bugs it would catch. These two are different: they decide whether somebody's
// unsaved work survives.
export default defineConfig({
  test: {
    include: ['src/live/**/*.test.ts'],
    environment: 'node',
  },
});
```

- [ ] **Step 3: Verify the runner starts**

```bash
npm test
```

Expected: "No test files found" — the runner works, there is nothing to run yet.

- [ ] **Step 4: Stage**

```bash
git add src/Umbraco.Community.AdvancedPermissions.Client/package.json src/Umbraco.Community.AdvancedPermissions.Client/package-lock.json src/Umbraco.Community.AdvancedPermissions.Client/vitest.config.ts
```

---

### Task 11: The cell classifier

**Files:**
- Create: `src/Umbraco.Community.AdvancedPermissions.Client/src/live/conflict.ts`
- Create: `src/Umbraco.Community.AdvancedPermissions.Client/src/live/conflict.test.ts`

- [ ] **Step 1: Write the failing tests**

`src/live/conflict.test.ts`:

```typescript
import { describe, expect, it } from 'vitest';
import { classifyCell, type CellEntry } from './conflict.js';

/**
 * Builds one cell entry.
 * @param state Allow or Deny.
 * @param scope How far the entry reaches.
 * @returns The entry.
 */
function entry(state: 'Allow' | 'Deny', scope: 'ThisNodeOnly' | 'ThisNodeAndDescendants' = 'ThisNodeOnly'): CellEntry {
  return { state, scope, isPriorityOverride: false };
}

describe('classifyCell', () => {
  it('reports own-write when the server already holds what the editor holds', () => {
    // Asked first, and that order is load-bearing: while a save is landing all three comparisons
    // are true at once, and only this answer avoids reloading over a save still settling.
    expect(classifyCell({ base: [entry('Allow')], mine: [entry('Deny')], theirs: [entry('Deny')] }))
      .toBe('own-write');
  });

  it('reports no-change when the server still holds what was loaded', () => {
    expect(classifyCell({ base: [entry('Allow')], mine: [entry('Deny')], theirs: [entry('Allow')] }))
      .toBe('no-change');
  });

  it('reports refresh when the editor has not touched this cell', () => {
    expect(classifyCell({ base: [entry('Allow')], mine: [entry('Allow')], theirs: [entry('Deny')] }))
      .toBe('refresh');
  });

  it('reports conflict when both sides moved differently', () => {
    expect(classifyCell({ base: [entry('Allow')], mine: [entry('Deny')], theirs: [] }))
      .toBe('conflict');
  });

  it('ignores entry order', () => {
    const a = entry('Allow', 'ThisNodeOnly');
    const b = entry('Deny', 'ThisNodeAndDescendants');
    expect(classifyCell({ base: [a, b], mine: [a, b], theirs: [b, a] })).toBe('own-write');
  });

  it('treats an empty cell and a populated one as different', () => {
    expect(classifyCell({ base: [], mine: [], theirs: [entry('Allow')] })).toBe('refresh');
  });

  it('does nothing when a side has not loaded', () => {
    // A grid mid-load reaches here with undefined. Doing nothing is the only safe answer to a
    // question that cannot be asked.
    expect(classifyCell({ base: undefined, mine: [entry('Allow')], theirs: [] })).toBe('no-change');
  });

  it('distinguishes a priority override from an otherwise identical entry', () => {
    const plain: CellEntry = { state: 'Allow', scope: 'ThisNodeOnly', isPriorityOverride: false };
    const override: CellEntry = { state: 'Allow', scope: 'ThisNodeOnly', isPriorityOverride: true };
    expect(classifyCell({ base: [plain], mine: [plain], theirs: [override] })).toBe('refresh');
  });
});
```

- [ ] **Step 2: Run to verify it fails**

```bash
cd src/Umbraco.Community.AdvancedPermissions.Client && npm test
```

Expected: failure — cannot resolve `./conflict.js`.

- [ ] **Step 3: Implement the classifier**

`src/live/conflict.ts`:

```typescript
import type { PermissionScope, PermissionState } from '../models/permission.models.js';

/**
 * The verdict for one cell — one node and one verb — when the server reports that something
 * changed.
 *
 * The decision is made from three snapshots rather than from anything the event says about
 * itself, because the event says almost nothing: `ServerEvent` carries a source, a type and one
 * key, with no user and no client identity, so this editor's own save is indistinguishable from a
 * colleague's by inspection. Asking the data instead answers for every write path there is —
 * including the same person saving from another browser tab — because it never tries to enumerate
 * them.
 */
export type CellVerdict =
  /** The server already holds what the editor holds. This editor wrote it. Do nothing. */
  | 'own-write'
  /** Nothing moved relative to what was loaded. A duplicate or stale event. Do nothing. */
  | 'no-change'
  /** The editor holds nothing of its own here, so take the server's value in place. */
  | 'refresh'
  /** Both sides moved, differently. A person has to decide. */
  | 'conflict';

/** One stored permission entry, reduced to the fields that decide whether two cells differ. */
export interface CellEntry {
  /** Allow or Deny. */
  state: PermissionState;
  /** How far the entry reaches. */
  scope: PermissionScope;
  /** Whether the entry is a priority override. */
  isPriorityOverride: boolean;
}

/** The three versions a verdict is decided from. A side that has not loaded yet is undefined. */
export interface CellComparison {
  /** What the server said when the editor loaded. */
  base: ReadonlyArray<CellEntry> | undefined;
  /** What the editor is holding: `base` with its pending change applied. */
  mine: ReadonlyArray<CellEntry> | undefined;
  /** What the server holds now. */
  theirs: ReadonlyArray<CellEntry> | undefined;
}

/**
 * Reduces a cell to a string equal for two cells that mean the same thing.
 *
 * Sorted, because nothing promises the order entries come back in, and two cells differing only
 * in that are the same cell.
 * @param entries The cell's entries.
 * @returns A comparable string.
 */
function signature(entries: ReadonlyArray<CellEntry>): string {
  return entries
    .map((e) => `${e.state}|${e.scope}|${e.isPriorityOverride ? '1' : '0'}`)
    .sort()
    .join('\u001e');
}

/**
 * Decides what a change on the server means for one cell of an editor holding unsaved work.
 *
 * The order of the checks is load-bearing. `own-write` is asked first because when a save is
 * landing all three comparisons are true at once, and only that answer is safe: the server matches
 * what the editor holds, and a moment later the loaded baseline will match it too. Asking
 * `refresh` first would reload over a save that is still settling.
 * @param comparison The three versions; see {@link CellComparison}.
 * @returns The verdict.
 */
export function classifyCell({ base, mine, theirs }: CellComparison): CellVerdict {
  // A side that has not arrived makes every comparison unknowable, and doing nothing is the only
  // safe answer to a question that cannot be asked.
  if (base === undefined || mine === undefined || theirs === undefined) return 'no-change';

  const theirSignature = signature(theirs);
  const mySignature = signature(mine);

  if (theirSignature === mySignature) return 'own-write';
  if (theirSignature === signature(base)) return 'no-change';
  if (mySignature === signature(base)) return 'refresh';
  return 'conflict';
}
```

- [ ] **Step 4: Run the tests**

```bash
npm test
```

Expected: 8 passed.

- [ ] **Step 5: Stage**

```bash
git add src/Umbraco.Community.AdvancedPermissions.Client/src/live/conflict.ts src/Umbraco.Community.AdvancedPermissions.Client/src/live/conflict.test.ts
```

---

### Task 12: The coalescer

**Files:**
- Create: `src/Umbraco.Community.AdvancedPermissions.Client/src/live/coalesce.ts`
- Create: `src/Umbraco.Community.AdvancedPermissions.Client/src/live/coalesce.test.ts`

- [ ] **Step 1: Write the failing tests**

`src/live/coalesce.test.ts`:

```typescript
import { describe, expect, it } from 'vitest';
import { createCoalescer } from './coalesce.js';

/**
 * A promise plus the function that settles it, so a test controls when work finishes.
 * @returns The promise and its resolver.
 */
function deferred(): { promise: Promise<void>; resolve: () => void } {
  let resolve!: () => void;
  const promise = new Promise<void>((r) => { resolve = r; });
  return { promise, resolve };
}

describe('createCoalescer', () => {
  it('runs immediately when nothing is in flight', async () => {
    let runs = 0;
    const run = createCoalescer(async () => { runs += 1; });
    await run();
    expect(runs).toBe(1);
  });

  it('collapses a burst arriving during one run into exactly one re-run', async () => {
    // Publishing with descendants or emptying the recycle bin delivers one event per affected
    // node. Without this, a surface refetches once per event, each time against data already
    // stale by the time the first answer came back.
    let runs = 0;
    const gate = deferred();
    const run = createCoalescer(async () => {
      runs += 1;
      if (runs === 1) await gate.promise;
    });

    const first = run();
    void run();
    void run();
    void run();
    gate.resolve();
    await first;

    expect(runs).toBe(2);
  });

  it('runs again normally once the queue has drained', async () => {
    let runs = 0;
    const run = createCoalescer(async () => { runs += 1; });
    await run();
    await run();
    expect(runs).toBe(2);
  });

  it('clears the in-flight flag when the work throws', async () => {
    // A rejected refetch that left the flag set would wedge the surface permanently: every later
    // event would queue behind a run that had already finished.
    let runs = 0;
    const run = createCoalescer(async () => {
      runs += 1;
      throw new Error('network');
    });

    await expect(run()).rejects.toThrow('network');
    await expect(run()).rejects.toThrow('network');
    expect(runs).toBe(2);
  });
});
```

- [ ] **Step 2: Run to verify it fails**

```bash
npm test
```

Expected: failure — cannot resolve `./coalesce.js`.

- [ ] **Step 3: Implement**

`src/live/coalesce.ts`:

```typescript
/**
 * Wraps an async job so at most one run is in flight and at most one more is queued behind it, no
 * matter how many times it is asked for.
 *
 * Bulk operations on the server deliver one event per affected entity — publishing with
 * descendants, emptying the recycle bin, deleting a user group that touched a hundred nodes — and
 * a surface that refetched per event would spend one request each, every one of them answering a
 * question that was already out of date when it was asked. Collapsing them to "run now, then once
 * more if anything else arrived" costs two requests for a burst of any size and still ends on
 * fresh data.
 * @param job The work to run.
 * @returns A function that requests a run.
 */
export function createCoalescer(job: () => Promise<void>): () => Promise<void> {
  let running: Promise<void> | undefined;
  let queued = false;

  /**
   * Runs the job, then once more if anything arrived while it ran.
   * @returns A promise settling when the run, and any follow-up, is done.
   */
  const start = async (): Promise<void> => {
    try {
      await job();
      // Checked after the job rather than before, so anything that arrived while it ran is
      // answered by a further pass against data newer than that pass saw.
      //
      // A loop, not a single `if`. `running` stays truthy for the whole cycle, so a request
      // arriving during the re-run sets `queued` again — and with a single check that flag is
      // cleared by `finally` with nothing scheduled to act on it. The request is then silently
      // dropped: its key sits in the caller's pending set, no refetch ever happens, the screen
      // stays wrong, and nothing anywhere says so. That is the exact failure this module exists
      // to rule out.
      while (queued) {
        queued = false;
        await job();
      }
    } finally {
      // In `finally`, because a rejected job that left these set would wedge the surface: every
      // later request would queue behind a run that had already finished.
      running = undefined;
      queued = false;
    }
  };

  return () => {
    if (running) {
      queued = true;
      return running;
    }

    running = start();
    return running;
  };
}
```

- [ ] **Step 4: Run the tests**

```bash
npm test
```

Expected: 12 passed across both files.

- [ ] **Step 5: Stage**

```bash
git add src/Umbraco.Community.AdvancedPermissions.Client/src/live/coalesce.ts src/Umbraco.Community.AdvancedPermissions.Client/src/live/coalesce.test.ts
```

---

### Task 13: The shared events controller

**Files:**
- Create: `src/Umbraco.Community.AdvancedPermissions.Client/src/live/security-events.controller.ts`

No test of its own: everything in it that can be wrong independently of the Umbraco context lives
in `conflict.ts` and `coalesce.ts`, which are tested. This file is the twenty lines that connect
them — the same split UmbraDesktop uses between its router and its controller.

- [ ] **Step 1: Create the controller**

```typescript
import { UmbControllerBase } from '@umbraco-cms/backoffice/class-api';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UMB_MANAGEMENT_API_SERVER_EVENT_CONTEXT } from '@umbraco-cms/backoffice/management-api';
import { createCoalescer } from './coalesce.js';

/**
 * The event sources this package publishes. Must match `AdvancedPermissionsServerEvents` on the
 * server exactly: the hub routes to a SignalR group named by the source string, so a mismatch
 * delivers nothing and nothing about that failure looks like a failure.
 */
export const UAP_EVENT_SOURCES = {
  /** Permission entries for a content node changed. */
  nodePermissions: 'AdvancedPermissions:NodePermissions',
  /** Document-type create permissions changed. */
  docTypePermissions: 'AdvancedPermissions:DocTypePermissions',
  /** Something changed that shifts effective permissions without this package's store being touched. */
  access: 'AdvancedPermissions:Access',
} as const;

/** One event as Umbraco's hub delivers it. */
interface UapServerEvent {
  /** The source string, e.g. `AdvancedPermissions:NodePermissions`. */
  eventSource: string;
  /** The event type; this package only ever sends `Updated`. */
  eventType: string;
  /** The entity's key. */
  key: string;
}

/**
 * Connects Umbraco's live change notifications to one Advanced Permissions surface.
 *
 * Consumes the built-in management-API server-event context rather than opening a connection of
 * this package's own. The hub is already there, already authenticated and already held open by the
 * backoffice for its own cache invalidation; a second socket would buy nothing and would need its
 * own authorization, reconnect and backoff to get wrong.
 *
 * What arrives is a hint, never a statement: `ServerEvent` carries a source, a type and one key,
 * with no payload, so the host always answers by refetching rather than by believing. The keys are
 * passed on anyway, because a surface can use them to decide whether it is showing anything
 * affected at all.
 *
 * Bursts are coalesced before the host hears about them — see {@link createCoalescer}.
 */
export class UapSecurityEventsController extends UmbControllerBase {
  /** Keys seen since the host was last called, so a burst arrives as one set. */
  #pending = new Set<string>();

  /** Requests a run, collapsing anything that arrives while one is in flight. */
  #request: () => Promise<void>;

  /**
   * @param host The element this controller belongs to.
   * @param onChanged Called with the keys that changed. Its promise is awaited, which is what lets
   * the coalescer know a refetch is still running.
   */
  constructor(host: UmbControllerHost, onChanged: (keys: ReadonlySet<string>) => Promise<void>) {
    super(host);

    this.#request = createCoalescer(async () => {
      // Taken and cleared before awaiting, so keys arriving during the refetch belong to the next
      // pass rather than being dropped as already handled.
      const keys = this.#pending;
      this.#pending = new Set<string>();
      await onChanged(keys);
    });

    this.consumeContext(UMB_MANAGEMENT_API_SERVER_EVENT_CONTEXT, (context) => {
      if (!context) return;
      this.observe(
        context.events,
        (event) => {
          if (!event) return;
          const typed = event as unknown as UapServerEvent;
          const sources: readonly string[] = Object.values(UAP_EVENT_SOURCES);
          if (!sources.includes(typed.eventSource)) return;
          this.#pending.add(typed.key);
          void this.#request();
        },
        'uapObserveSecurityEvents',
      );
    });
  }
}
```

- [ ] **Step 2: Type-check**

```bash
cd src/Umbraco.Community.AdvancedPermissions.Client && npx tsc --noEmit
```

Expected: clean. If `UMB_MANAGEMENT_API_SERVER_EVENT_CONTEXT` will not resolve, check the export in
the reference source at `D:/github/UmbracoVersions/v17/Umbraco-CMS/src/Umbraco.Web.UI.Client` — do
not read `node_modules`.

- [ ] **Step 3: Stage**

```bash
git add src/Umbraco.Community.AdvancedPermissions.Client/src/live/security-events.controller.ts
```

---

### Task 14: Localization strings

**Files:**
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/localization/en.ts` and `en-us.ts` first
- Modify: the other 26 files in `src/Umbraco.Community.AdvancedPermissions.Client/src/localization/`

The package covers all 28 backoffice languages and a missing key renders as the raw key, so this is
not optional polish.

- [ ] **Step 1: Add the keys to `en.ts`**

Add to the existing `uap_` block, matching the file's formatting:

```typescript
  uap_liveUpdated: 'Updated just now',
  uap_liveConflictTitle: 'Someone changed these permissions while you were editing',
  uap_liveConflictBody: '%0% of your unsaved changes collide with what is stored now. Nothing of yours has been changed.',
  uap_liveConflictBodyOne: 'One of your unsaved changes collides with what is stored now. Nothing of yours has been changed.',
  uap_liveLoadStored: 'Load stored values',
  uap_liveKeepMine: 'Keep my changes',
  uap_liveFlagLegend: 'Changed by someone else since you loaded',
  uap_conflictDialogTitle: 'Overwrite the stored changes?',
  uap_conflictDialogBody: 'Saving replaces what is stored now. %0% changes made by someone else will be lost, and this cannot be undone.',
  uap_conflictDialogBodyOne: 'Saving replaces what is stored now. One change made by someone else will be lost, and this cannot be undone.',
  uap_conflictStoredLabel: 'stored',
  uap_conflictYoursLabel: 'yours',
  uap_conflictOverwrite: 'Overwrite anyway',
  uap_conflictCancel: 'Cancel',
```

Note the separate singular keys. A count reads as a counting error in the wrong branch, and
"1 changes" is exactly the thing that makes a warning look careless at the moment it most needs to
be believed.

- [ ] **Step 2: Copy to `en-us.ts`**

Same keys. Check how `en-us.ts` currently relates to `en.ts` in this repo and follow it rather than
introducing a second convention.

- [ ] **Step 3: Translate into the other 26 files**

For each file in `src/localization/` other than `en.ts` and `en-us.ts`, add the same keys with a
translation. Match the register and terminology the file already uses — in particular whatever that
language's file already uses for "user group", since these strings sit beside those.

- [ ] **Step 4: Verify none were missed**

```bash
cd src/Umbraco.Community.AdvancedPermissions.Client && for f in src/localization/*.ts; do n=$(grep -c "uap_liveUpdated\|uap_conflictOverwrite" "$f"); echo "$n $f"; done
```

Expected: every line reports `2`. Any file reporting `0` was missed.

- [ ] **Step 5: Type-check and stage**

```bash
npx tsc --noEmit
git add src/Umbraco.Community.AdvancedPermissions.Client/src/localization/
```

---

## Phase 5 — client surfaces

### Task 15: The API layer

**Files:**
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/models/permission.models.ts`
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/api/advanced-permissions.api.ts`

- [ ] **Step 1: Regenerate the typed client**

The server gained endpoints and response fields, so the generated client is stale. Start the test
site:

```bash
cd tests/Umbraco.Community.AdvancedPermissions.TestSite && dotnet run --urls http://localhost:5266
```

In a second shell:

```bash
cd src/Umbraco.Community.AdvancedPermissions.Client && npm run generate-client
```

- [ ] **Step 2: Add the model types**

Append to `src/models/permission.models.ts`:

```typescript
/** One node-and-user-group pair in a batch save, with the stamp the client read. */
export interface BatchSaveNode {
  /** The content node key, or VIRTUAL_ROOT_NODE_KEY for the virtual root. */
  nodeKey: string;
  /** The user group alias. */
  roleAlias: string;
  /** The replacement entries. An empty list removes all entries for the pair. */
  entries: Array<{ verb: string; state: PermissionState; scope: PermissionScope; isPriorityOverride: boolean }>;
  /** The stamp this client was given when it read these entries. */
  expectedStamp: string | undefined;
}

/** One pair's new stamp after a successful batch save. */
export interface BatchSavedStamp {
  /** The content node key. */
  nodeKey: string;
  /** The user group alias. */
  roleAlias: string;
  /** The stamp of what was just written. */
  stamp: string;
}

/** One pair the server refused because its stored entries moved. */
export interface BatchSaveConflict {
  /** The content node key. */
  nodeKey: string;
  /** The user group alias. */
  roleAlias: string;
  /** What is stored right now — what the dialog shows as "stored". */
  currentEntries: PermissionEntry[];
  /** The stamp of `currentEntries`, so a resolved conflict can retry without a further read. */
  currentStamp: string;
}

/** The result of a batch save: either it wrote, or it named what moved. */
export type BatchSaveResult =
  | { ok: true; stamps: ReadonlyMap<string, string> }
  | { ok: false; conflicts: BatchSaveConflict[] };
```

And add to the existing `TreeNode` interface:

```typescript
  /** The concurrency stamp of this node's entries, sent back on save. */
  stamp: string;
```

- [ ] **Step 3: Add the API wrapper**

Append to `src/api/advanced-permissions.api.ts`:

```typescript
/**
 * Saves several node-and-user-group pairs at once, all or nothing.
 *
 * Resolves with `ok: false` and the conflicting pairs when the server refuses, rather than
 * throwing: a conflict is an expected answer this feature exists to produce, not a failure, and
 * routing it through the error path would mean recovering the detail out of an exception.
 * @param nodes The pairs to write.
 * @param force Whether to write through a stale stamp. Only true after the user has confirmed.
 * @returns The new stamps, or the conflicts.
 */
export async function savePermissionsBatch(
  nodes: BatchSaveNode[],
  force = false,
): Promise<BatchSaveResult> {
  const { data, error, response } = await AdvancedPermissionsService.batchSavePermissions({
    body: {
      nodes: nodes.map((n) => ({
        nodeKey: n.nodeKey,
        roleAlias: n.roleAlias,
        entries: n.entries,
        ...(n.expectedStamp !== undefined ? { expectedStamp: n.expectedStamp } : {}),
      })),
      force,
    },
  });

  if (response.status === 409) {
    const body = error as { conflicts: BatchSaveConflict[] };
    return { ok: false, conflicts: body.conflicts };
  }

  if (error) throw error;

  const stamps = new Map<string, string>();
  for (const saved of (data ?? []) as BatchSavedStamp[]) {
    stamps.set(`${saved.nodeKey}|${saved.roleAlias}`, saved.stamp);
  }

  return { ok: true, stamps };
}
```

Note the spread guard on `expectedStamp`: `exactOptionalPropertyTypes` is on, so assigning an
explicit `undefined` to an optional property is a type error (CLAUDE.md #5).

- [ ] **Step 4: Type-check and stage**

```bash
npx tsc --noEmit
git add src/Umbraco.Community.AdvancedPermissions.Client/src/models/ src/Umbraco.Community.AdvancedPermissions.Client/src/api/
```

---

### Task 16: The viewers

**Files:**
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/access-viewer/uap-access-viewer-root.element.ts`
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/doc-type-permissions/uap-doc-type-create-audit-root.element.ts`

The viewers first, because they are the simplest complete proof that the transport works: no dirty
state, no conflict, no dialog. If a viewer refreshes itself, everything upstream of the client is
correct, and every later task can assume it.

- [ ] **Step 1: Wire the controller into the Access Viewer**

Add the import:

```typescript
import { UapSecurityEventsController } from '../live/security-events.controller.js';
```

Add beside the existing `@state()` declarations:

```typescript
  /** Set after a live refresh, to show the "updated" pill. */
  @state() private _liveRefreshedAt: number | null = null;
```

And in the constructor, after the existing set-up:

```typescript
    // A viewer holds nothing of the user's, so it never asks and never flags — it just becomes
    // correct again. The keys are ignored deliberately: a permission written on an ancestor moves
    // what every descendant on screen resolves to, so there is no subset worth refetching.
    new UapSecurityEventsController(this, async () => {
      if (!this._activeSubject) return;
      clearEffectivePermissionCache();
      await this.#reloadEffective();
      this._liveRefreshedAt = Date.now();
    });
```

- [ ] **Step 2: Render the pill**

In `render()`, inside the `uap-selection-panel`'s `actions` slot — or immediately above the table
if that slot is not used by this element:

```typescript
          ${this._liveRefreshedAt
            ? html`<span class="live-pill"><uui-icon name="icon-sync"></uui-icon>${this.#localize.term('uap_liveUpdated')}</span>`
            : nothing}
```

And add to `static override styles`:

```css
    .live-pill {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 3px 9px;
      border-radius: 4px;
      font-size: 12px;
      background: var(--uui-color-surface-alt);
      color: var(--uui-color-text-alt);
    }
```

- [ ] **Step 3: Do the same for the Create Audit viewer**

Repeat steps 1 and 2 in `uap-doc-type-create-audit-root.element.ts`, calling that element's own
reload method in place of `#reloadEffective()`. Read the file first to find its name — do not
assume it matches the Access Viewer's.

- [ ] **Step 4: Build and verify by hand**

```bash
cd src/Umbraco.Community.AdvancedPermissions.Client && npm run build
cd ../../tests/Umbraco.Community.AdvancedPermissions.TestSite && dotnet run
```

Open the Access Viewer for a user in one browser. In a second browser, signed in as a different
user, change a permission that affects them. The first browser's grid must update within a second
and show the pill, with no interaction.

- [ ] **Step 5: Stage**

```bash
git add src/Umbraco.Community.AdvancedPermissions.Client/src/access-viewer/ src/Umbraco.Community.AdvancedPermissions.Client/src/doc-type-permissions/uap-doc-type-create-audit-root.element.ts
```

---

### Task 17: The banner and dialog elements

**Files:**
- Create: `src/Umbraco.Community.AdvancedPermissions.Client/src/live/uap-live-banner.element.ts`
- Create: `src/Umbraco.Community.AdvancedPermissions.Client/src/live/uap-conflict-dialog.element.ts`

Their own elements so both editors share one copy, and so the editor files — already 700 lines —
do not grow another two panels of markup.

- [ ] **Step 1: Create the banner**

```typescript
import { LitElement, css, customElement, html, nothing, property } from '@umbraco-cms/backoffice/external/lit';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';

/**
 * The notice an editor shows when somebody else changed the permissions it is holding unsaved work
 * against.
 *
 * Dismissible and non-blocking on purpose. A modal here would interrupt whatever the person was in
 * the middle of typing to tell them about a collision they may not care about yet, and the
 * save-time dialog already stops anything irreversible. This only has to be noticeable.
 */
@customElement('uap-live-banner')
export class UapLiveBannerElement extends LitElement {
  /** How many cells collide with the stored values. */
  @property({ type: Number }) conflictCount = 0;

  /** The localization controller for this element. */
  #localize = new UmbLocalizationController(this);

  override render() {
    if (this.conflictCount < 1) return nothing;

    const body = this.conflictCount === 1
      ? this.#localize.term('uap_liveConflictBodyOne')
      : this.#localize.term('uap_liveConflictBody', this.conflictCount);

    return html`
      <div class="banner" role="status">
        <uui-icon name="icon-alert"></uui-icon>
        <div class="text">
          <strong>${this.#localize.term('uap_liveConflictTitle')}</strong>
          <p>${body}</p>
          <div class="actions">
            <uui-button
              look="outline"
              label=${this.#localize.term('uap_liveLoadStored')}
              @click=${() => this.dispatchEvent(new CustomEvent('uap-live-load-stored'))}>
              ${this.#localize.term('uap_liveLoadStored')}
            </uui-button>
            <uui-button
              look="outline"
              label=${this.#localize.term('uap_liveKeepMine')}
              @click=${() => this.dispatchEvent(new CustomEvent('uap-live-keep-mine'))}>
              ${this.#localize.term('uap_liveKeepMine')}
            </uui-button>
          </div>
        </div>
      </div>
    `;
  }

  static override styles = css`
    .banner {
      display: flex;
      gap: 10px;
      padding: 11px 13px;
      background: var(--uui-color-warning);
      color: var(--uui-color-warning-contrast);
      border-bottom: 1px solid var(--uui-color-warning-standalone);
    }

    .text p {
      margin: 2px 0 0;
      font-size: 13px;
    }

    .actions {
      display: flex;
      gap: 8px;
      margin-top: 9px;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    /** The live-change banner. */
    'uap-live-banner': UapLiveBannerElement;
  }
}
```

Check the `lit` import line against how the other elements in this package import `customElement`
and `property` and follow that — this repo already has one answer and should not gain a second.

- [ ] **Step 2: Create the dialog**

```typescript
import { LitElement, css, customElement, html, property } from '@umbraco-cms/backoffice/external/lit';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';

/** One line of the dialog's "stored versus yours" list. */
export interface UapConflictLine {
  /** The node's display name. */
  nodeName: string;
  /** The verb's display name. */
  verbName: string;
  /** What is stored now, already localized. */
  stored: string;
  /** What this editor holds, already localized. */
  mine: string;
}

/**
 * The confirmation shown when a save is refused because the stored values moved.
 *
 * It exists because the banner can be missed — a tab left in the background, a hub connection that
 * dropped, a save that beat its own event — and this is the last point at which somebody else's
 * work can still be kept. So it is reached from the server's refusal rather than from the client's
 * own knowledge, and it names what would be lost rather than warning in the abstract.
 */
@customElement('uap-conflict-dialog')
export class UapConflictDialogElement extends LitElement {
  /** The conflicting cells, as the server reported them. */
  @property({ type: Array }) lines: UapConflictLine[] = [];

  /** The localization controller for this element. */
  #localize = new UmbLocalizationController(this);

  /** Shows the dialog. */
  open(): void {
    this.removeAttribute('hidden');
  }

  /** Hides the dialog. */
  close(): void {
    this.setAttribute('hidden', '');
  }

  override render() {
    const body = this.lines.length === 1
      ? this.#localize.term('uap_conflictDialogBodyOne')
      : this.#localize.term('uap_conflictDialogBody', this.lines.length);

    return html`
      <uui-modal-container>
        <uui-modal-dialog>
          <uui-dialog-layout headline=${this.#localize.term('uap_conflictDialogTitle')}>
            <p>${body}</p>
            <ul class="lines">
              ${this.lines.map((l) => html`
                <li>
                  <strong>${l.nodeName} · ${l.verbName}</strong>
                  ${this.#localize.term('uap_conflictStoredLabel')}: ${l.stored} ·
                  ${this.#localize.term('uap_conflictYoursLabel')}: ${l.mine}
                </li>
              `)}
            </ul>
            <uui-button
              slot="actions"
              label=${this.#localize.term('uap_conflictCancel')}
              @click=${() => this.dispatchEvent(new CustomEvent('uap-conflict-cancel'))}>
              ${this.#localize.term('uap_conflictCancel')}
            </uui-button>
            <uui-button
              slot="actions"
              look="outline"
              label=${this.#localize.term('uap_liveLoadStored')}
              @click=${() => this.dispatchEvent(new CustomEvent('uap-conflict-load-stored'))}>
              ${this.#localize.term('uap_liveLoadStored')}
            </uui-button>
            <uui-button
              slot="actions"
              look="primary"
              color="danger"
              label=${this.#localize.term('uap_conflictOverwrite')}
              @click=${() => this.dispatchEvent(new CustomEvent('uap-conflict-overwrite'))}>
              ${this.#localize.term('uap_conflictOverwrite')}
            </uui-button>
          </uui-dialog-layout>
        </uui-modal-dialog>
      </uui-modal-container>
    `;
  }

  static override styles = css`
    :host([hidden]) { display: none; }

    .lines {
      list-style: none;
      margin: 12px 0 0;
      padding: 9px 11px;
      background: var(--uui-color-surface-alt);
      border-radius: 4px;
      font-size: 13px;
    }

    .lines li { padding: 3px 0; }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    /** The save-time conflict confirmation. */
    'uap-conflict-dialog': UapConflictDialogElement;
  }
}
```

Before building, compare the modal markup above against the existing
`uap-permission-scope-dialog` element and follow that element's approach to opening and closing.

- [ ] **Step 3: Build**

```bash
cd src/Umbraco.Community.AdvancedPermissions.Client && npx tsc --noEmit && npm run build
```

Expected: clean.

- [ ] **Step 4: Stage**

```bash
git add src/Umbraco.Community.AdvancedPermissions.Client/src/live/
```

---

### Task 18: The Permissions Editor

**Files:**
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/permissions-editor/uap-permissions-editor-root.element.ts`

The largest single change in this plan. Take it in the steps below rather than as one edit.

- [ ] **Step 1: Add the state and the controller**

Add the imports:

```typescript
import { UapSecurityEventsController } from '../live/security-events.controller.js';
import { classifyCell, type CellEntry } from '../live/conflict.js';
import { savePermissionsBatch } from '../api/advanced-permissions.api.js';
import type { BatchSaveConflict, BatchSaveNode } from '../models/permission.models.js';
import '../live/uap-live-banner.element.js';
import '../live/uap-conflict-dialog.element.js';
```

Add beside the existing `@state()` declarations:

```typescript
  /** Cells the server changed under an unsaved edit, keyed `nodeKey|verb`. */
  @state() private _conflicts: Set<string> = new Set();

  /** Whether the banner has been dismissed for the current set of conflicts. */
  @state() private _bannerDismissed = false;

  /** Set after a silent live refresh, to show the "updated" pill. */
  @state() private _liveRefreshedAt: number | null = null;
```

And in the constructor:

```typescript
    // A clean editor takes the server's version in place; a dirty one is classified cell by cell
    // and nothing of the user's is touched. The keys are ignored deliberately — a permission
    // written on an ancestor moves what every descendant on screen resolves to, so there is no
    // subset worth refetching.
    new UapSecurityEventsController(this, async () => {
      if (!this._selectedRole) return;
      if (this._pendingChanges.size === 0) {
        await this.#reloadPermissions();
        this._liveRefreshedAt = Date.now();
        return;
      }
      await this.#reconcileWithServer();
    });
```

- [ ] **Step 2: Add the reconciliation**

Add these private methods next to `#saveChanges`:

```typescript
  /**
   * Refetches the stored values and works out, cell by cell, what the change means for an editor
   * holding unsaved work.
   *
   * Cells the user has not touched take the server's value silently — there is nothing of theirs
   * to lose, and leaving them stale would show two vintages of data in one grid. Cells where both
   * sides moved are flagged, and nothing else happens to them. The user's pending changes are
   * never written over here; that only ever follows an explicit choice.
   */
  async #reconcileWithServer(): Promise<void> {
    if (!this._selectedRole) return;

    const controller = new AbortController();
    const conflicts = new Set<string>();

    for (const node of this.#flattenNodes(this._treeNodes)) {
      const apiKey = node.key === 'virtual-root' ? VIRTUAL_ROOT_NODE_KEY : node.key;
      const theirs = await getPermissions(apiKey, this._selectedRole.alias, controller.signal);
      const pendingForNode = this._pendingChanges.get(node.key);

      for (const verbInfo of this._verbs) {
        const verb = verbInfo.verb;
        const base = this.#cellOf(node.entries, verb);
        const theirCell = this.#cellOf(theirs, verb);
        const pending = pendingForNode?.get(verb);
        const mine: CellEntry[] = pending
          ? pending.map((p) => ({ state: p.state, scope: p.scope, isPriorityOverride: p.isPriorityOverride }))
          : base;

        if (classifyCell({ base, mine, theirs: theirCell }) === 'conflict') {
          conflicts.add(`${node.key}|${verb}`);
        }
      }

      // The baseline advances PER VERB, never wholesale. A verb that conflicted keeps its old
      // base; a verb that did not adopts the server's.
      //
      // This is subtle and getting it wrong reopens the exact hole the feature closes. The
      // baseline is what the next pass compares against, so advancing a conflicted verb's base to
      // the server's value makes the following pass see `theirs == base` and return `no-change` —
      // the conflict silently clears, the withheld stamp is adopted, and the user's save then
      // passes the concurrency check and overwrites the other person's work with no 409 and no
      // dialog. Any unrelated event anywhere in the tree triggers that pass, because the events
      // controller deliberately ignores event keys.
      //
      // Re-flagging an unresolved conflict on every later event is the correct behaviour, not a
      // defect to design away. Forgetting it is the defect.
      this.#updateNode(node.key, { entries: mergedPerVerb });
    }

    if (conflicts.size > 0) {
      this._bannerDismissed = false;
    }

    this._conflicts = conflicts;
  }

  /**
   * Reduces a node's stored entries for one verb to the fields a comparison cares about.
   * @param entries The node's entries.
   * @param verb The verb to extract.
   * @returns The cell's entries.
   */
  #cellOf(entries: PermissionEntry[], verb: string): CellEntry[] {
    return entries
      .filter((e) => e.verb === verb)
      .map((e) => ({ state: e.state, scope: e.scope, isPriorityOverride: e.isPriorityOverride }));
  }

  /**
   * Walks the loaded tree depth-first.
   * @param nodes The nodes to walk.
   * @returns Every loaded node, parents before children.
   */
  #flattenNodes(nodes: TreeNodeState[]): TreeNodeState[] {
    const out: TreeNodeState[] = [];
    for (const node of nodes) {
      out.push(node);
      if (node.children?.length) out.push(...this.#flattenNodes(node.children));
    }
    return out;
  }

  /**
   * Resolves the flagged cells to the stored values, keeping every other pending change.
   *
   * Scoped to the flagged cells on purpose. Discarding everything would be simpler and would throw
   * away edits elsewhere in the tree that nobody is contesting — which is the outcome this whole
   * feature exists to avoid.
   */
  #loadStoredForConflicts(): void {
    const next = new Map(this._pendingChanges);

    for (const cell of this._conflicts) {
      const [nodeKey, verb] = cell.split('|');
      const forNode = next.get(nodeKey);
      if (!forNode) continue;
      forNode.delete(verb);
      if (forNode.size === 0) next.delete(nodeKey);
    }

    this._pendingChanges = next;
    this._conflicts = new Set();
    this._bannerDismissed = false;
  }
```

Both recursive private methods carry explicit return types — without them TypeScript infers `any`
(CLAUDE.md #4).

- [ ] **Step 3: Switch the save to the batch endpoint**

Replace `#saveChanges` with a version that sends one request and handles the refusal:

```typescript
  /**
   * Writes every pending change in one all-or-nothing request, and stops to ask if the stored
   * values moved since this editor read them.
   *
   * One request rather than the previous loop of one per node: a rejection partway through a loop
   * would leave some nodes written and some not, and nothing afterwards could tell which.
   * @param force Whether to write through a stale stamp. Only true after the user has confirmed.
   */
  async #saveChanges(force = false): Promise<void> {
    if (!this._pendingChanges.size || !this._selectedRole || this._saving) return;
    this._saving = true;

    try {
      const nodes: BatchSaveNode[] = [];

      for (const [nodeKey, verbChanges] of this._pendingChanges) {
        const node = this.#findNode(nodeKey);
        if (!node) continue;

        // Start from stored, apply the pending verb changes — unchanged from the previous
        // implementation, only the destination differs.
        const byVerb = new Map<string, Array<{ verb: string; state: PermissionState; scope: PermissionScope; isPriorityOverride: boolean }>>();
        for (const e of node.entries) {
          const list = byVerb.get(e.verb) ?? [];
          list.push({ verb: e.verb, state: e.state, scope: e.scope, isPriorityOverride: e.isPriorityOverride });
          byVerb.set(e.verb, list);
        }
        for (const [verb, pending] of verbChanges) {
          if (pending.length === 0) {
            byVerb.delete(verb);
          } else {
            byVerb.set(verb, pending.map((pe) => ({
              verb,
              state: pe.state,
              scope: pe.scope,
              isPriorityOverride: pe.isPriorityOverride,
            })));
          }
        }

        nodes.push({
          nodeKey: nodeKey === 'virtual-root' ? VIRTUAL_ROOT_NODE_KEY : nodeKey,
          roleAlias: this._selectedRole.alias,
          entries: [...byVerb.values()].flat(),
          expectedStamp: node.stamp,
        });
      }

      const result = await savePermissionsBatch(nodes, force);

      if (!result.ok) {
        // Not an error path. A conflict is the answer this feature exists to produce, and what
        // happens next is the user's decision, not this method's.
        this.#openConflictDialog(result.conflicts);
        return;
      }

      await this.#reloadPermissions();
      this._pendingChanges = new Map();
      this._conflicts = new Set();
      clearEffectivePermissionCache();
      this.#notificationContext?.peek('positive', { data: { message: this.#localize.term('uap_permissionsSaved') } });
    } catch (err) {
      this.#notificationContext?.peek('danger', { data: { message: this.#localize.term('uap_saveFailed', String(err)) } });
    } finally {
      this._saving = false;
    }
  }
```

**The virtual root needs a real stamp.** The editors synthesise that node client-side rather than
receiving it from the tree endpoint, so it has no `stamp` of its own. A placeholder is not an
option: an empty string never matches the stored stamp, so every save touching the virtual root
would come back `409` — a conflict dialog about a change nobody made — while omitting the stamp
would leave the virtual root as the one node with no concurrency protection at all. Fetch its real
stamp with `getPermissionsWithStamp(VIRTUAL_ROOT_NODE_KEY, roleAlias)` when the tree loads, and
refresh it whenever the node's entries are reloaded.

Then add `#openConflictDialog(conflicts: BatchSaveConflict[]): void`. It maps each conflicted cell
into a `UapConflictLine` — node name from `#findNode`, verb display name from `this._verbs`,
`stored` rendered from the conflict's `currentEntries` and `mine` from `_pendingChanges` — sets
`lines` on the `uap-conflict-dialog` element, and calls its `open()`. Wire its three events on the
element in `render()`:

- `uap-conflict-cancel` — close the dialog, change nothing.
- `uap-conflict-load-stored` — close, call `this.#loadStoredForConflicts()`, and do **not** save.
- `uap-conflict-overwrite` — close, then `void this.#saveChanges(true)`.

- [ ] **Step 4: Render the banner, the flags, the dialog and the pill**

In `render()`, immediately above the table wrapper:

```typescript
          ${this._conflicts.size > 0 && !this._bannerDismissed
            ? html`<uap-live-banner
                .conflictCount=${this._conflicts.size}
                @uap-live-load-stored=${() => this.#loadStoredForConflicts()}
                @uap-live-keep-mine=${() => { this._bannerDismissed = true; }}>
              </uap-live-banner>`
            : nothing}
```

In the cell renderer, add the flag class to the `<td>`:

```typescript
      <td
        class="perm-td ${this._conflicts.has(`${node.key}|${verb}`) ? 'conflicted' : ''}"
        title=${verb}
        @click=${() => this.#openPicker(node, verb)}>
```

And the style:

```css
    .perm-td.conflicted uap-perm-block {
      outline: 2px solid var(--uui-color-danger);
      outline-offset: 2px;
    }
```

Add the dialog beside the existing scope dialog at the end of `render()`:

```typescript
      <uap-conflict-dialog
        hidden
        @uap-conflict-cancel=${() => this.#closeConflictDialog()}
        @uap-conflict-load-stored=${() => { this.#closeConflictDialog(); this.#loadStoredForConflicts(); }}
        @uap-conflict-overwrite=${() => { this.#closeConflictDialog(); void this.#saveChanges(true); }}>
      </uap-conflict-dialog>
```

Add the same `live-pill` markup and style used in Task 16, shown when `_liveRefreshedAt` is set.

- [ ] **Step 5: Build and type-check**

```bash
cd src/Umbraco.Community.AdvancedPermissions.Client && npx tsc --noEmit && npm run build
```

Expected: clean.

- [ ] **Step 6: Verify by hand, two browsers**

```bash
cd tests/Umbraco.Community.AdvancedPermissions.TestSite && dotnet run
```

Run all five cases and confirm each one:

1. **Clean editor.** Open the editor, change nothing. Change a permission from the other browser.
   The grid updates silently and the pill appears. No banner.
2. **Dirty, no collision.** Change verb A on node 1, do not save. From the other browser change
   verb B on node 2. Node 2 updates, your unsaved change to node 1 is untouched, no banner.
3. **Dirty, collision.** Change verb A on node 1, do not save. From the other browser change verb A
   on node 1 to something else. Exactly that cell is outlined, the banner names one conflict, and
   your value is still on screen.
4. **Load stored values.** From case 3, having also changed verb B on node 2 first, click "Load
   stored values". The flagged cell reverts to the stored value; your node 2 change survives.
5. **Overwrite.** From case 3, click "Keep my changes", then Save. The dialog appears naming what
   would be lost. Cancel leaves everything as it was. Save again and choose "Overwrite anyway" — it
   writes, and the other browser's editor flags the same cell within a second.

- [ ] **Step 7: Stage**

```bash
git add src/Umbraco.Community.AdvancedPermissions.Client/src/permissions-editor/
```

---

### Task 19: The Document Type Permissions Editor

**Files:**
- Modify: `src/Umbraco.Community.AdvancedPermissions/Controllers/DocTypePermissionsController.cs`
- Modify: `src/Umbraco.Community.AdvancedPermissions/Services/DocTypePermissionService.cs`
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/doc-type-permissions/uap-doc-type-permissions-editor-root.element.ts`
- Test: `tests/Umbraco.Community.AdvancedPermissions.Tests/DocTypeBatchSaveTests.cs`

**The one structural difference from Task 18.** A document-type permission is keyed by *three*
things, not two: `DocTypePermissionService.SaveEditorEntriesAsync` takes
`(nodeKey, roleAlias, contentTypeKey, entries)`. So the unit a stamp describes — and the unit a
conflict is reported about — is node **plus user group plus document type**. Everywhere Task 18
writes `nodeKey|verb` or `${nodeKey}|${roleAlias}`, this task carries the content type as well.
Getting that wrong produces a check that passes when it should fail, which is the one failure mode
worse than no check at all.

- [ ] **Step 1: Write the failing tests**

Create `tests/Umbraco.Community.AdvancedPermissions.Tests/DocTypeBatchSaveTests.cs` with the same
six cases as `BatchSavePermissionsTests`, each keyed on all three parts:

1. `BatchSave_MatchingStamp_Saves` — the stamp matches, the write reaches the service once.
2. `BatchSave_StaleStamp_Returns409AndWritesNothing` — asserts `ConflictObjectResult`, that the
   conflict names the node, the user group **and** the content type, and that the service received
   no save.
3. `BatchSave_OneStalePair_RefusesWholeBatch` — two pairs differing only by content type, one
   stale; the whole batch is refused and only the stale one is listed.
4. `BatchSave_Force_SavesDespiteStaleStamp`.
5. `BatchSave_NullStamp_SkipsCheck`.
6. `BatchSave_InvalidVerb_ReturnsBadRequest`.

Case 3 is the one that catches a two-part key used where a three-part key belongs: with the content
type dropped from the comparison, the two pairs collapse into one and the test fails.

- [ ] **Step 2: Run to verify they fail**

```bash
dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests/ --filter DocTypeBatchSaveTests
```

Expected: build failure — the endpoint does not exist.

- [ ] **Step 3: Publish the notification**

In `src/Umbraco.Community.AdvancedPermissions/Services/DocTypePermissionService.cs`, add
`IEventAggregator eventAggregator` to the primary constructor and publish at the end of
`SaveEditorEntriesAsync`, **after** the two existing `cache.Invalidate…` calls:

```csharp
        // Strictly after invalidation, for the reason set out on AdvancedPermissionService: a
        // handler that refetched on this while the cache still held the old value would read the
        // snapshot this write replaced, and would never be told again.
        await eventAggregator.PublishAsync(
            new DocTypePermissionsChangedNotification(nodeKey, roleAlias, [contentTypeKey]),
            cancellationToken);
```

Add `using Umbraco.Cms.Core.Events;` and `using Umbraco.Community.AdvancedPermissions.Notifications;`.

- [ ] **Step 4: Add the three-part `SaveManyAsync`**

On `IDocTypePermissionRepository`, `IDocTypePermissionService` and their implementations, mirroring
Task 3's transaction but with the content type in the key:

```csharp
    /// <summary>
    /// Replaces the entries for several node, user group and document type triples in a single
    /// transaction.
    /// </summary>
    /// <remarks>
    /// All or nothing, for the same reason as the permission repository's equivalent: the editor
    /// changes several at once, and a partial write leaves a state nothing afterwards can read.
    /// </remarks>
    /// <param name="batch">The triple and replacement entries for each.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    Task SaveManyAsync(
        IEnumerable<(Guid NodeKey, string RoleAlias, Guid ContentTypeKey, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries)> batch,
        CancellationToken cancellationToken = default);
```

The service implementation invalidates once per distinct user group, then publishes one
`DocTypePermissionsChangedNotification` per triple — the same two-stage shape as
`AdvancedPermissionService.SaveManyAsync` in Task 5.

- [ ] **Step 5: Add the batch endpoint**

In `src/Umbraco.Community.AdvancedPermissions/Controllers/Models/DocTypePermissionResponseModels.cs`,
add the batch models alongside the existing records:

```csharp
/// <summary>
/// A request to replace document-type create permissions for several node, user group and document
/// type triples at once.
/// </summary>
/// <param name="Nodes">The triples to write.</param>
/// <param name="Force">Whether to skip the concurrency check. Only sent after a user confirms.</param>
public sealed record BatchSaveDocTypePermissionsRequestModel(
    IReadOnlyList<BatchSaveDocTypePermissionsNode> Nodes,
    bool Force = false);

/// <summary>
/// One node, user group and document type triple within a batch save.
/// </summary>
/// <param name="NodeKey">The content node key the rule is anchored to.</param>
/// <param name="RoleAlias">The user group alias.</param>
/// <param name="ContentTypeKey">The document type the rule is about.</param>
/// <param name="Entries">The replacement entries. An empty list removes all entries for the triple.</param>
/// <param name="ExpectedStamp">
/// The stamp this client read. Null skips the check for this triple, which is how an older client
/// keeps working.
/// </param>
public sealed record BatchSaveDocTypePermissionsNode(
    Guid NodeKey,
    string RoleAlias,
    Guid ContentTypeKey,
    IReadOnlyList<SavePermissionEntryItem> Entries,
    string? ExpectedStamp = null);

/// <summary>The body of the <c>409 Conflict</c> a document-type batch save is refused with.</summary>
/// <param name="Conflicts">Only the triples whose stored entries moved.</param>
public sealed record BatchSaveDocTypeConflictResponseModel(
    IReadOnlyList<BatchSaveDocTypeConflict> Conflicts);

/// <summary>One triple whose stored entries no longer match what the client loaded.</summary>
/// <param name="NodeKey">The content node key.</param>
/// <param name="RoleAlias">The user group alias.</param>
/// <param name="ContentTypeKey">The document type.</param>
/// <param name="CurrentEntries">What is stored right now.</param>
/// <param name="CurrentStamp">The stamp of <paramref name="CurrentEntries"/>.</param>
public sealed record BatchSaveDocTypeConflict(
    Guid NodeKey,
    string RoleAlias,
    Guid ContentTypeKey,
    IReadOnlyList<DocTypePermissionEntryResponseModel> CurrentEntries,
    string CurrentStamp);

/// <summary>One triple's new stamp after a successful batch save.</summary>
/// <param name="NodeKey">The content node key.</param>
/// <param name="RoleAlias">The user group alias.</param>
/// <param name="ContentTypeKey">The document type.</param>
/// <param name="Stamp">The stamp of what was just written.</param>
public sealed record BatchSavedDocTypeStamp(
    Guid NodeKey,
    string RoleAlias,
    Guid ContentTypeKey,
    string Stamp);
```

Then add `PUT doc-type-permissions/batch` to `DocTypePermissionsController`, following
`BatchSavePermissions` from Task 6 exactly: validate every triple's entries first and return
`BadRequest` on any failure; then, unless `Force`, read each triple's stored entries, compare
`PermissionStamp.Compute` against `ExpectedStamp`, and collect conflicts; refuse the whole batch
with `Conflict(...)` if there are any; otherwise call `SaveManyAsync` and return the new stamps.

Reuse the `TryMapEntries` helper added in Task 6 rather than writing a second copy of the
verb/state/scope validation — that duplication is exactly how the two endpoints drift apart.

Include Task 6's **duplicate-triple guard**: after mapping and before any stamp work, reject a
request containing the same `(NodeKey, RoleAlias, ContentTypeKey)` twice with a `400` naming it.
The repository throws on duplicates as a last-resort invariant guard, but letting that escape turns
a client mistake into a `500` while every other validation failure on the same endpoint returns a
clean `400`.

- [ ] **Step 6: Add `Stamp` to the editor's read model**

`GetForEditor` returns the entries the editor renders. Add a `Stamp` per triple to its response
model, computed with `ComputeFromResponse` over that triple's entries, exactly as Task 2 does for
`TreeNodeResponseModel`. Without it the client has no stamp to send and the check silently degrades
to the null-stamp path, which passes every time.

- [ ] **Step 7: Run the server tests**

```bash
dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests/ --filter DocTypeBatchSaveTests
dotnet test
```

Expected: 6 passed in the filter, full suite green.

- [ ] **Step 8: Wire the client editor**

In `uap-doc-type-permissions-editor-root.element.ts`, add the same four pieces Task 18 added to the
permissions editor, with the content type carried through every key:

- The imports, the three `@state()` fields (`_conflicts`, `_bannerDismissed`, `_liveRefreshedAt`)
  and the `UapSecurityEventsController` in the constructor — clean editor reloads and shows the
  pill, dirty editor reconciles.
- `#reconcileWithServer`, classifying each cell with `classifyCell` from `../live/conflict.js` and
  recording conflicts under `` `${nodeKey}|${contentTypeKey}|${verb}` ``.
- `#loadStoredForConflicts`, dropping only the flagged cells from the pending map.
- The batch save, sending `expectedStamp` per triple and opening the conflict dialog on
  `ok: false`.
- The `uap-live-banner` above the grid, the `conflicted` outline class on flagged cells, and the
  `uap-conflict-dialog` with its three handlers.

`classifyCell`, `createCoalescer`, `uap-live-banner` and `uap-conflict-dialog` are shared. Do not
write a second copy of any of them, and do not fork them "just for doc types" — a divergence there
means two different answers to the same question about somebody's unsaved work.

- [ ] **Step 9: Verify**

```bash
dotnet test
cd src/Umbraco.Community.AdvancedPermissions.Client && npx tsc --noEmit && npm run build
```

Then run all five manual cases from Task 18 Step 6 against the document-type editor: clean editor
refreshes silently; dirty editor with no collision keeps its edit; dirty editor with a collision
flags exactly that cell; "Load stored values" reverts only the flagged cell; and a forced save
writes and flags the other browser.

- [ ] **Step 10: Stage**

```bash
git add src/Umbraco.Community.AdvancedPermissions/ src/Umbraco.Community.AdvancedPermissions.Client/src/doc-type-permissions/ tests/
```

---

## Phase 6 — finishing

### Task 20: Documentation

**Files:**
- Create: `Memory/ADRs/ADR-011-server-events-over-umbraco-hub.md`
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/help/help-content.ts`
- Modify: `README.md`

- [ ] **Step 1: Write the ADR**

Follow the format of the existing ADRs in `Memory/ADRs/`. Record the decision to ride Umbraco's hub
rather than run one, and — more usefully — the two constraints that will otherwise be rediscovered
the hard way: events must be raised strictly after cache invalidation, and `ServerEvent` carries no
payload so every consumer refetches rather than believing the event.

- [ ] **Step 2: Add the in-app help**

Add a short section to `help-content.ts` for both editors, explaining what the banner means and what
the two choices actually do. This is the only place a user learns that "Keep my changes" does not
mean "my changes have won".

- [ ] **Step 3: Update the README**

Add real-time updates to the feature list, and state the load-balancing limitation plainly: without
a SignalR backplane only clients connected to the same server receive the events. Someone who meets
that in production without having been told will conclude the feature is broken.

- [ ] **Step 4: Stage**

```bash
git add README.md Memory/ADRs/ src/Umbraco.Community.AdvancedPermissions.Client/src/help/
```

---

### Task 21: Full verification

- [ ] **Step 1: Everything**

```bash
dotnet build
dotnet test
cd src/Umbraco.Community.AdvancedPermissions.Client && npm test && npx tsc --noEmit && npm run build
```

Expected: build clean, every .NET test passing, 12 client tests passing, type-check clean.

- [ ] **Step 2: Confirm nothing regressed for a lone editor**

With only one browser open, change several permissions across several nodes and save. It must still
save — in one request now — with no banner and no dialog. The concurrency check has to be invisible
when there is nothing to conflict with. If a solo editor ever sees the conflict dialog, the stamp
the client is sending is not the stamp the server handed it, and that is a bug in Task 2 or Task 15,
not in the dialog.

- [ ] **Step 3: Report**

Report what was done and what remains, and leave the work uncommitted for review.

---

## v18

Once v17 is merged, port to `C:\GitHub\UmbracoAdvancedSecurity_v18`. The server half should apply
unchanged. v18 has more surfaces than v17, but they all consume the same
`UapSecurityEventsController`, `conflict.ts` and `coalesce.ts`, so each extra surface is Task 16
(for a viewer) or Task 18 (for an editor) repeated — wiring, not design.
