import { describe, expect, it, vi } from 'vitest';
import { replaceExtensionApi } from './replace-extension-api.js';
import type { ReplaceableManifest, ReplaceableRegistry } from './replace-extension-api.js';

/** A manifest as the test registry stores it: the replaceable fields plus anything else. */
type TestManifest = ReplaceableManifest & { api?: unknown; [key: string]: unknown };

/**
 * A minimal in-memory registry recording the order of unregister/register calls.
 */
class FakeRegistry implements ReplaceableRegistry<TestManifest> {
  /** The registered manifests, keyed by alias. */
  readonly manifests = new Map<string, TestManifest>();
  /** The mutating calls received, in order. */
  readonly calls: Array<string> = [];

  /**
   * Seeds the registry with manifests.
   * @param manifests The manifests to start with.
   */
  constructor(...manifests: Array<TestManifest>) {
    for (const m of manifests) this.manifests.set(m.alias, m);
  }

  /** @inheritdoc */
  getByAlias(alias: string): TestManifest | undefined {
    return this.manifests.get(alias);
  }

  /** @inheritdoc */
  unregister(alias: string): void {
    this.calls.push(`unregister:${alias}`);
    this.manifests.delete(alias);
  }

  /** @inheritdoc */
  register(manifest: TestManifest): void {
    this.calls.push(`register:${manifest.alias}`);
    this.manifests.set(manifest.alias, manifest);
  }
}

const nativeApi = () => Promise.resolve({});
const packageApi = () => Promise.resolve({});

/** The native Save and Publish manifest as Umbraco 18 registers it. */
const nativeSaveAndPublish: TestManifest = {
  type: 'workspaceAction',
  kind: 'default',
  alias: 'Umb.WorkspaceAction.Document.SaveAndPublish',
  name: 'Save And Publish Document Workspace Action',
  weight: 70,
  api: nativeApi,
  meta: { label: '#buttons_saveAndPublish', look: 'primary', color: 'positive' },
  conditions: [
    { alias: 'Umb.Condition.WorkspaceAlias', match: 'Umb.Workspace.Document' },
    { alias: 'Umb.Condition.EntityIsNotTrashed' },
  ],
};

describe('replaceExtensionApi', () => {
  it('re-registers the extension under the same alias with the package api and name', () => {
    // The alias must survive: Publish with descendants, Schedule and Unpublish attach to the
    // Save and Publish button through forWorkspaceActions on exactly this alias.
    const registry = new FakeRegistry(nativeSaveAndPublish);

    const replaced = replaceExtensionApi(registry, 'Umb.WorkspaceAction.Document.SaveAndPublish', {
      name: 'Advanced Permissions Save And Publish Document Workspace Action',
      api: packageApi,
    });

    expect(replaced).toBe(true);
    const current = registry.getByAlias('Umb.WorkspaceAction.Document.SaveAndPublish');
    expect(current?.api).toBe(packageApi);
    expect(current?.name).toBe('Advanced Permissions Save And Publish Document Workspace Action');
  });

  it('keeps every other field of the native manifest, so placement, label and conditions follow Umbraco', () => {
    const registry = new FakeRegistry(nativeSaveAndPublish);

    replaceExtensionApi(registry, 'Umb.WorkspaceAction.Document.SaveAndPublish', {
      name: 'Advanced Permissions Save And Publish Document Workspace Action',
      api: packageApi,
    });

    const { api: _api, name: _name, ...rest } = registry.getByAlias('Umb.WorkspaceAction.Document.SaveAndPublish')!;
    const { api: _nativeApi, name: _nativeName, ...nativeRest } = nativeSaveAndPublish;
    expect(rest).toEqual(nativeRest);
  });

  it('unregisters the native extension before registering the replacement', () => {
    // The registry refuses a second registration under an alias that is still taken.
    const registry = new FakeRegistry(nativeSaveAndPublish);

    replaceExtensionApi(registry, 'Umb.WorkspaceAction.Document.SaveAndPublish', {
      name: 'Advanced Permissions Save And Publish Document Workspace Action',
      api: packageApi,
    });

    expect(registry.calls).toEqual([
      'unregister:Umb.WorkspaceAction.Document.SaveAndPublish',
      'register:Umb.WorkspaceAction.Document.SaveAndPublish',
    ]);
  });

  it('changes nothing and reports false when the native extension is not registered', () => {
    // Without the native manifest there is nothing to inherit placement and conditions from, and
    // registering a bare action would invent them. Leave the registry alone and say so.
    const registry = new FakeRegistry();
    const consoleWarn = vi.spyOn(console, 'warn').mockImplementation(() => {});

    try {
      const replaced = replaceExtensionApi(registry, 'Umb.WorkspaceAction.Document.SaveAndPublish', {
        name: 'Advanced Permissions Save And Publish Document Workspace Action',
        api: packageApi,
      });

      expect(replaced).toBe(false);
      expect(registry.calls).toEqual([]);
      expect(consoleWarn).toHaveBeenCalledOnce();
    } finally {
      consoleWarn.mockRestore();
    }
  });
});
