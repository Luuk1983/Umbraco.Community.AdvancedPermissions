import { describe, expect, it, vi } from 'vitest';
import type { StartNodesResponse } from '../models/permission.models.js';
import { StartNodesLoader } from './start-nodes-loader.js';

const response = (name: string): StartNodesResponse => ({
  state: 'Restricted',
  startNodes: [{ key: name.toLowerCase(), name }],
  sources: [],
});

/** One fetch call whose outcome the test controls, so in-flight states can be observed. */
interface Pending {
  resolve: (value: StartNodesResponse) => void;
  reject: (reason: unknown) => void;
}

/**
 * Builds a loader whose fetch hands out one controllable pending request per call, in order.
 * `pending[n]` is the n-th call's request.
 */
function setup() {
  const pending: Pending[] = [];
  const fetcher = vi.fn(
    () =>
      new Promise<StartNodesResponse>((resolve, reject) => {
        pending.push({ resolve, reject });
      }),
  );
  const host = { addController() {}, requestUpdate: vi.fn() };
  const loader = new StartNodesLoader(host, fetcher);
  return { host, loader, fetcher, pending };
}

const subject = { roleAlias: 'editors' };
const names = (loader: StartNodesLoader) => loader.data?.startNodes.map((n) => n.name);

describe('StartNodesLoader', () => {
  it('starts empty', () => {
    const { loader } = setup();
    expect(loader.data).toBeNull();
    expect(loader.loading).toBe(false);
    expect(loader.error).toBe(false);
  });

  it('foreground load sets loading, then data, and requests updates', async () => {
    const { loader, host, pending } = setup();

    const done = loader.load(subject, new AbortController().signal);
    expect(loader.loading).toBe(true);
    expect(loader.data).toBeNull();
    expect(host.requestUpdate).toHaveBeenCalled();

    pending[0]!.resolve(response('Home'));
    expect(await done).toBe(true);
    expect(loader.loading).toBe(false);
    expect(loader.error).toBe(false);
    expect(names(loader)).toEqual(['Home']);
  });

  it('passes the subject and signal to the fetch', async () => {
    const { loader, fetcher, pending } = setup();
    const signal = new AbortController().signal;
    const done = loader.load({ userKey: 'u1' }, signal);
    pending[0]!.resolve(response('Home'));
    await done;
    expect(fetcher).toHaveBeenCalledWith({ userKey: 'u1' }, signal);
  });

  it('a foreground load drops the previous data while it loads', async () => {
    const { loader, pending } = setup();
    const first = loader.load(subject, new AbortController().signal);
    pending[0]!.resolve(response('Old'));
    await first;
    expect(names(loader)).toEqual(['Old']);

    const second = loader.load(subject, new AbortController().signal);
    expect(loader.data).toBeNull();
    expect(loader.loading).toBe(true);
    pending[1]!.resolve(response('New'));
    await second;
    expect(names(loader)).toEqual(['New']);
  });

  it('a background load keeps the old data until the new data arrives, without loading', async () => {
    const { loader, pending } = setup();
    const first = loader.load(subject, new AbortController().signal);
    pending[0]!.resolve(response('Old'));
    await first;

    const second = loader.load(subject, new AbortController().signal, true);
    expect(loader.loading).toBe(false);
    expect(names(loader)).toEqual(['Old']);

    pending[1]!.resolve(response('New'));
    expect(await second).toBe(true);
    expect(names(loader)).toEqual(['New']);
    expect(loader.loading).toBe(false);
  });

  it('an aborted load discards its result and reports success', async () => {
    const { loader, pending } = setup();
    const controller = new AbortController();
    const done = loader.load(subject, controller.signal);
    controller.abort();
    pending[0]!.resolve(response('Stale'));
    expect(await done).toBe(true);
    expect(loader.data).toBeNull();
  });

  it('an aborted load that fails does not set the error', async () => {
    const { loader, pending } = setup();
    const controller = new AbortController();
    const done = loader.load(subject, controller.signal);
    controller.abort();
    pending[0]!.reject(new Error('aborted'));
    expect(await done).toBe(true);
    expect(loader.error).toBe(false);
  });

  it('a superseding background load does not leave loading stuck from the aborted foreground load', async () => {
    const { loader, pending } = setup();

    const c1 = new AbortController();
    const first = loader.load(subject, c1.signal);
    expect(loader.loading).toBe(true);

    c1.abort();
    const second = loader.load(subject, new AbortController().signal, true);

    pending[0]!.resolve(response('Stale'));
    await first;
    expect(loader.data).toBeNull();

    pending[1]!.resolve(response('Fresh'));
    expect(await second).toBe(true);
    expect(loader.loading).toBe(false);
    expect(names(loader)).toEqual(['Fresh']);
  });

  it('a failure sets error, reports false and does not stick loading', async () => {
    const { loader, pending } = setup();
    const done = loader.load(subject, new AbortController().signal);
    pending[0]!.reject(new Error('boom'));
    expect(await done).toBe(false);
    expect(loader.error).toBe(true);
    expect(loader.loading).toBe(false);
    expect(loader.data).toBeNull();
  });

  it('a background failure keeps the old data and sets error', async () => {
    const { loader, pending } = setup();
    const first = loader.load(subject, new AbortController().signal);
    pending[0]!.resolve(response('Old'));
    await first;

    const second = loader.load(subject, new AbortController().signal, true);
    pending[1]!.reject(new Error('boom'));
    expect(await second).toBe(false);
    expect(loader.error).toBe(true);
    expect(names(loader)).toEqual(['Old']);
  });

  it('a new load clears a previous error', async () => {
    const { loader, pending } = setup();
    const failing = loader.load(subject, new AbortController().signal);
    pending[0]!.reject(new Error('boom'));
    await failing;
    expect(loader.error).toBe(true);

    const retry = loader.load(subject, new AbortController().signal);
    expect(loader.error).toBe(false);
    pending[1]!.resolve(response('Home'));
    await retry;
    expect(names(loader)).toEqual(['Home']);
  });

  it('a load still in flight when the host disconnects is discarded', async () => {
    const { loader, pending } = setup();
    const done = loader.load(subject, new AbortController().signal);
    loader.hostDisconnected();
    pending[0]!.resolve(response('Late'));
    expect(await done).toBe(true);
    expect(loader.data).toBeNull();
  });

  it('clear resets data, loading and error and requests an update', async () => {
    const { loader, host, pending } = setup();
    const first = loader.load(subject, new AbortController().signal);
    pending[0]!.resolve(response('Old'));
    await first;
    const failing = loader.load(subject, new AbortController().signal, true);
    pending[1]!.reject(new Error('boom'));
    await failing;
    void loader.load(subject, new AbortController().signal);
    expect(loader.loading).toBe(true);
    host.requestUpdate.mockClear();

    loader.clear();
    expect(loader.data).toBeNull();
    expect(loader.loading).toBe(false);
    expect(loader.error).toBe(false);
    expect(host.requestUpdate).toHaveBeenCalled();
  });
});
