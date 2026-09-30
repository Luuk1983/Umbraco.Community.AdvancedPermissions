import { describe, expect, it } from 'vitest';
import { createMissedEvents } from './missed-events.js';

describe('createMissedEvents', () => {
  it('owes nothing until an event is deferred', () => {
    expect(createMissedEvents().take()).toBe(false);
  });

  it('owes a replay after an event was deferred', () => {
    // I1a: an event landing while a save is in flight must be answered when the save ends.
    const missed = createMissedEvents();

    missed.defer();

    expect(missed.take()).toBe(true);
  });

  it('owes one replay however many events were deferred, because a replay answers them all', () => {
    const missed = createMissedEvents();

    missed.defer();
    missed.defer();
    missed.defer();

    expect(missed.take()).toBe(true);
    expect(missed.take()).toBe(false);
  });

  it('owes a new replay for an event deferred after the previous one was taken', () => {
    const missed = createMissedEvents();
    missed.defer();
    missed.take();

    missed.defer();

    expect(missed.take()).toBe(true);
  });
});
