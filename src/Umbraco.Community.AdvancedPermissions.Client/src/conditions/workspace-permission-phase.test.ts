import { describe, expect, it } from 'vitest';
import { isWorkspaceCheckOutOfPhase } from './workspace-permission-phase.js';

const CREATE = 'Umb.Document.Create';
const UPDATE = 'Umb.Document.Update';

describe('isWorkspaceCheckOutOfPhase', () => {
  it('retires the create check once the document has been saved', () => {
    // Issue #58: the workspace keeps its "may create" check alive after the first save. Asked about
    // the saved document itself, it denies when Create is not granted there (for example a
    // descendants-only deny), which made the freshly saved document read-only.
    expect(isWorkspaceCheckOutOfPhase({ allOf: [CREATE] }, false)).toBe(true);
  });

  it('keeps the create check while the document is being created', () => {
    expect(isWorkspaceCheckOutOfPhase({ allOf: [CREATE] }, true)).toBe(false);
  });

  it('retires the update check while a new document is being created', () => {
    // The workspace is reused when moving from editing one document to creating the next, so the
    // previous document's "may update" check lives on into the create.
    expect(isWorkspaceCheckOutOfPhase({ allOf: [UPDATE] }, true)).toBe(true);
  });

  it('keeps the update check for a saved document', () => {
    expect(isWorkspaceCheckOutOfPhase({ allOf: [UPDATE] }, false)).toBe(false);
  });

  it('keeps every check while it is not yet known whether the document is new', () => {
    expect(isWorkspaceCheckOutOfPhase({ allOf: [CREATE] }, undefined)).toBe(false);
    expect(isWorkspaceCheckOutOfPhase({ allOf: [UPDATE] }, undefined)).toBe(false);
  });

  it('keeps checks for any other verb', () => {
    expect(isWorkspaceCheckOutOfPhase({ allOf: ['Umb.Document.Publish'] }, false)).toBe(false);
    expect(isWorkspaceCheckOutOfPhase({ allOf: ['Umb.Document.Publish'] }, true)).toBe(false);
  });

  it('keeps checks that combine verbs', () => {
    expect(isWorkspaceCheckOutOfPhase({ allOf: [CREATE, UPDATE] }, false)).toBe(false);
    expect(isWorkspaceCheckOutOfPhase({ allOf: [CREATE], oneOf: [UPDATE] }, false)).toBe(false);
  });
});
