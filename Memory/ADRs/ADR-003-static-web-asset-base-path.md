# ADR-003: Override StaticWebAssetBasePath to Serve at Root

**Status**: Accepted
**Date**: 2026-04-04

## Context

Razor Class Libraries (RCL) serve their static web assets under a default path of `/_content/{AssemblyName}/`. For our package this would be `/_content/Umbraco.Community.AdvancedPermissions/App_Plugins/...`, but Umbraco expects App_Plugins content to be accessible at `/App_Plugins/...`.

## Decision

Add `<StaticWebAssetBasePath>/</StaticWebAssetBasePath>` to the csproj:

```xml
<PropertyGroup>
  <StaticWebAssetBasePath>/</StaticWebAssetBasePath>
</PropertyGroup>
```

This overrides the default prefix so files are served directly at the root path, making `/App_Plugins/Umbraco.Community.AdvancedPermissions/uas.js` work as Umbraco expects.

## Consequences

- The Vite output path (`wwwroot/App_Plugins/Umbraco.Community.AdvancedPermissions/`) maps directly to the served URL `/App_Plugins/Umbraco.Community.AdvancedPermissions/`.
- The `umbraco-package.json` entry `"js": "/App_Plugins/Umbraco.Community.AdvancedPermissions/uas.js"` resolves correctly.
- If multiple packages use `<StaticWebAssetBasePath>/</StaticWebAssetBasePath>` there is a risk of path collisions — mitigated by using a unique `App_Plugins/{PackageName}/` subfolder.
