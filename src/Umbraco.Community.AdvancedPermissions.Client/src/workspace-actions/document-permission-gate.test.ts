import { describe, expect, it, vi } from 'vitest';
import { DOCUMENT_PERMISSION_CONDITION_ALIAS, gateOnDocumentPermission } from './document-permission-gate.js';
import type { CreateApiByAlias, DocumentPermissionConditionArgs } from './document-permission-gate.js';

/**
 * Records every enable/disable call so a test can assert the order the gate drove them in.
 */
class FakeAction {
  /** The calls received, in order. */
  calls: Array<'enable' | 'disable'> = [];

  /** Records an enable call. */
  enable(): void {
    this.calls.push('enable');
  }

  /** Records a disable call. */
  disable(): void {
    this.calls.push('disable');
  }
}

/**
 * A stand-in for Umbraco's createExtensionApiByAlias that remembers what it was asked for, so a
 * test can play the condition's part by calling the captured onChange.
 * @returns The fake and the requests it received.
 */
function fakeCreateApiByAlias(): {
  create: CreateApiByAlias;
  requests: Array<{ host: unknown; alias: string; args: DocumentPermissionConditionArgs }>;
} {
  const requests: Array<{ host: unknown; alias: string; args: DocumentPermissionConditionArgs }> = [];
  const create: CreateApiByAlias = async (host, alias, constructorArgs) => {
    requests.push({ host, alias, args: constructorArgs[0] });
    return {};
  };
  return { create, requests };
}

describe('gateOnDocumentPermission', () => {
  it('disables the action before the permission condition has answered', async () => {
    // Deny by default: the condition only reports changes, so until it first says "permitted"
    // the button must not be clickable.
    const action = new FakeAction();
    const { create } = fakeCreateApiByAlias();

    await gateOnDocumentPermission(create, {}, action, ['Umb.Document.Update']);

    expect(action.calls).toEqual(['disable']);
  });

  it('resolves the condition through the registry alias, not a hard-wired class', async () => {
    // The whole point of the fix: whatever is registered under the native alias decides — in this
    // package, the Advanced Permissions replacement condition.
    const action = new FakeAction();
    const host = {};
    const { create, requests } = fakeCreateApiByAlias();

    await gateOnDocumentPermission(create, host, action, ['Umb.Document.Update', 'Umb.Document.Publish']);

    expect(requests).toHaveLength(1);
    expect(requests[0]!.alias).toBe('Umb.Condition.UserPermission.Document');
    expect(DOCUMENT_PERMISSION_CONDITION_ALIAS).toBe('Umb.Condition.UserPermission.Document');
    expect(requests[0]!.host).toBe(host);
    expect(requests[0]!.args.config).toEqual({
      alias: 'Umb.Condition.UserPermission.Document',
      allOf: ['Umb.Document.Update', 'Umb.Document.Publish'],
    });
  });

  it('enables the action when the condition reports permitted, and disables it again when revoked', async () => {
    const action = new FakeAction();
    const { create, requests } = fakeCreateApiByAlias();

    await gateOnDocumentPermission(create, {}, action, ['Umb.Document.Update']);
    requests[0]!.args.onChange(true);
    requests[0]!.args.onChange(false);

    expect(action.calls).toEqual(['disable', 'enable', 'disable']);
  });

  it('leaves the action disabled, without throwing, when the condition cannot be created', async () => {
    // A missing or broken condition must fail closed: a disabled button, never an unhandled
    // rejection that takes the workspace action down with it.
    const action = new FakeAction();
    const failing: CreateApiByAlias = async () => {
      throw new Error('Failed to get manifest by alias');
    };
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});

    try {
      await expect(gateOnDocumentPermission(failing, {}, action, ['Umb.Document.Update'])).resolves.toBeUndefined();

      expect(action.calls).toEqual(['disable']);
      expect(consoleError).toHaveBeenCalledOnce();
    } finally {
      consoleError.mockRestore();
    }
  });
});
