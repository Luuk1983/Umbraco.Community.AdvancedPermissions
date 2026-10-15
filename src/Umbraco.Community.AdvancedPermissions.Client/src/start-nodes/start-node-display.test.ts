import { describe, expect, it } from 'vitest';
import type { StartNodeSourceItem, StartNodesResponse } from '../models/permission.models.js';
import {
  blockedBannerTerm,
  blockedNoteTerm,
  blockedPlaceholderPermission,
  isBlocked,
  rowBadge,
  startNodeLines,
  startNodeSourceTerm,
  stripIntroTerm,
} from './start-node-display.js';

const source = (over: Partial<StartNodeSourceItem>): StartNodeSourceItem => ({
  kind: 'UserGroup',
  userGroupAlias: 'editors',
  userGroupName: 'Editors',
  startNodeKey: 'aaa',
  startNodeName: 'Home',
  isRoot: false,
  status: 'InUse',
  relatedNodeKey: null,
  relatedNodeName: null,
  relatedIsRoot: false,
  ...over,
});

const restricted: StartNodesResponse = {
  state: 'Restricted',
  startNodes: [{ key: 'AAA', name: 'Home' }],
  sources: [],
};

describe('isBlocked', () => {
  it('blocks path-only and not-visible nodes', () => {
    expect(isBlocked('PathOnly')).toBe(true);
    expect(isBlocked('NotVisible')).toBe(true);
  });
  it('does not block accessible or not-yet-loaded nodes', () => {
    expect(isBlocked('Accessible')).toBe(false);
    expect(isBlocked(null)).toBe(false);
    expect(isBlocked(undefined)).toBe(false);
  });
});

describe('rowBadge', () => {
  it('marks blocked rows by their kind', () => {
    expect(rowBadge('x', 'PathOnly', restricted)).toBe('pathOnly');
    expect(rowBadge('x', 'NotVisible', restricted)).toBe('notVisible');
  });
  it('marks a start node regardless of key casing', () => {
    expect(rowBadge('aaa', 'Accessible', restricted)).toBe('startNode');
  });
  it('leaves ordinary accessible rows alone', () => {
    expect(rowBadge('bbb', 'Accessible', restricted)).toBeNull();
    expect(rowBadge('aaa', 'Accessible', null)).toBeNull();
  });
});

describe('stripIntroTerm', () => {
  it('says start nodes are in effect for a restricted subject', () => {
    expect(stripIntroTerm('Restricted', 'user')).toBe('uap_startNodes_introRestrictedUser');
    expect(stripIntroTerm('Restricted', 'role')).toBe('uap_startNodes_introRestrictedRole');
  });
  it('says start nodes do not limit a subject with root access', () => {
    expect(stripIntroTerm('RootAccess', 'user')).toBe('uap_startNodes_introRootUser');
    expect(stripIntroTerm('RootAccess', 'role')).toBe('uap_startNodes_introRootRole');
  });
  it('says a subject without start nodes reaches no content', () => {
    expect(stripIntroTerm('NoAccess', 'user')).toBe('uap_startNodes_introNoAccessUser');
    expect(stripIntroTerm('NoAccess', 'role')).toBe('uap_startNodes_introNoAccessRole');
  });
  it('says start nodes do not apply to the All Users group', () => {
    expect(stripIntroTerm('NotApplicable', 'role')).toBe('uap_startNodes_introNotApplicable');
  });
});

describe('startNodeLines', () => {
  it('lists only the start nodes in effect, with the sources that set them', () => {
    const data: StartNodesResponse = {
      state: 'Restricted',
      startNodes: [{ key: 'AAA', name: 'Example1' }],
      sources: [
        source({ userGroupName: 'Administrators', startNodeKey: null, startNodeName: null, isRoot: true, status: 'ReplacedByUserStartNode' }),
        source({ userGroupName: 'Sensitive data', startNodeKey: null, startNodeName: null, status: 'NoStartNode' }),
        source({ kind: 'User', userGroupAlias: null, userGroupName: null, startNodeKey: 'aaa', startNodeName: 'Example1' }),
      ],
    };
    expect(startNodeLines(data)).toEqual([{ key: 'AAA', name: 'Example1', fromUser: true, groups: [] }]);
  });
  it('names every user group that sets the same start node', () => {
    const data: StartNodesResponse = {
      state: 'Restricted',
      startNodes: [{ key: 'aaa', name: 'Home' }],
      sources: [source({}), source({ userGroupAlias: 'blog', userGroupName: 'Blog writers' })],
    };
    expect(startNodeLines(data)).toEqual([{ key: 'aaa', name: 'Home', fromUser: false, groups: ['Editors', 'Blog writers'] }]);
  });
  it('falls back to the alias when a group has no name', () => {
    const data: StartNodesResponse = {
      state: 'Restricted',
      startNodes: [{ key: 'aaa', name: 'Home' }],
      sources: [source({ userGroupName: null })],
    };
    expect(startNodeLines(data)[0]?.groups).toEqual(['editors']);
  });
  it('has no lines without effective start nodes', () => {
    expect(startNodeLines({ state: 'RootAccess', startNodes: [], sources: [source({ isRoot: true })] })).toEqual([]);
  });
});

describe('startNodeSourceTerm', () => {
  it('says who set the start node', () => {
    expect(startNodeSourceTerm({ key: 'a', name: 'A', fromUser: true, groups: [] }))
      .toEqual({ key: 'uap_startNodes_sourceUser', args: [] });
    expect(startNodeSourceTerm({ key: 'a', name: 'A', fromUser: false, groups: ['Editors', 'Blog writers'] }))
      .toEqual({ key: 'uap_startNodes_sourceGroups', args: ['Editors, Blog writers'] });
    expect(startNodeSourceTerm({ key: 'a', name: 'A', fromUser: true, groups: ['Editors'] }))
      .toEqual({ key: 'uap_startNodes_sourceUserAndGroups', args: ['Editors'] });
  });
  it('says nothing when no source could be matched', () => {
    expect(startNodeSourceTerm({ key: 'a', name: 'A', fromUser: false, groups: [] })).toBeNull();
  });
});

describe('blocked dialog terms', () => {
  it('picks the banner by kind', () => {
    expect(blockedBannerTerm('PathOnly')).toBe('uap_startNodes_blockedPathOnly');
    expect(blockedBannerTerm('NotVisible')).toBe('uap_startNodes_blockedNotVisible');
  });
  it('lists the start nodes, or says there are none', () => {
    expect(blockedNoteTerm(restricted)).toEqual({ key: 'uap_startNodes_blockedNote', args: ['Home'] });
    expect(blockedNoteTerm({ state: 'NoAccess', startNodes: [], sources: [] }))
      .toEqual({ key: 'uap_startNodes_blockedNoAccessNote', args: [] });
    expect(blockedNoteTerm(null)).toEqual({ key: 'uap_startNodes_blockedNoListNote', args: [] });
  });
  it('uses the plain note for an empty list that is not NoAccess', () => {
    expect(blockedNoteTerm({ state: 'RootAccess', startNodes: [], sources: [] }))
      .toEqual({ key: 'uap_startNodes_blockedNoListNote', args: [] });
    expect(blockedNoteTerm({ state: 'NotApplicable', startNodes: [], sources: [] }))
      .toEqual({ key: 'uap_startNodes_blockedNoListNote', args: [] });
  });
});

describe('blockedPlaceholderPermission', () => {
  it('is a deny with no reasoning, so the dialog can still draw its blocked banner', () => {
    expect(blockedPlaceholderPermission('Umb.Document.Read'))
      .toEqual({ verb: 'Umb.Document.Read', isAllowed: false, isExplicit: false, reasoning: [] });
  });
});
