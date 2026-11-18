import { describe, expect, it } from 'vitest'
import { apply, changesModel } from './editor'
import type { Element, Relationship, Snapshot } from './types'

const el = (id: string, kind: Element['kind'], parentId: string | null = null, name = id): Element => ({
  id,
  kind,
  name,
  description: '',
  technology: '',
  tags: [],
  parentId,
  external: false,
})
const rel = (sourceId: string, targetId: string, description = 'Uses'): Relationship => ({
  id: `r-${sourceId}-${targetId}`,
  sourceId,
  targetId,
  description,
  technology: '',
})

const small: Snapshot = {
  schemaVersion: 1,
  elements: [
    el('user', 'person', null, 'User'),
    el('sys', 'softwareSystem', null, 'Shop'),
    el('web', 'container', 'sys', 'Web'),
    el('api', 'container', 'sys', 'API'),
    el('db', 'container', 'sys', 'Database'),
    el('ctrl', 'component', 'api', 'Controller'),
    el('repo', 'component', 'api', 'Repository'),
  ],
  relationships: [rel('user', 'web'), rel('web', 'api'), rel('api', 'db'), rel('ctrl', 'repo'), rel('repo', 'db')],
  layout: { 'component:api': { ctrl: { x: 0, y: 0 }, repo: { x: 300, y: 0 } } },
}

describe('editor command reducer', () => {
  it('adds an element with the id the caller generated', () => {
    const next = apply(small, {
      type: 'addElement',
      id: '0b6f…uuid',
      kind: 'container',
      name: ' Cache ',
      parentId: 'sys',
      technology: 'Redis',
    })
    expect(next.elements.at(-1)).toMatchObject({ id: '0b6f…uuid', name: 'Cache', parentId: 'sys', technology: 'Redis' })
  })

  it('places a new element when a position is given', () => {
    const next = apply(small, {
      type: 'addElement',
      id: 'n1',
      kind: 'container',
      name: 'Queue',
      parentId: 'sys',
      placement: { viewKey: 'container:sys', position: { x: 5, y: 6 } },
    })
    expect(next.layout['container:sys']).toEqual({ n1: { x: 5, y: 6 } })
  })

  it('renames without changing the id', () => {
    const next = apply(small, { type: 'updateElement', id: 'web', fields: { name: ' Storefront ', technology: 'Vue' } })
    expect(next.elements.find((e) => e.id === 'web')).toMatchObject({
      id: 'web',
      name: 'Storefront',
      technology: 'Vue',
    })
  })

  it('ignores an empty name', () => {
    expect(apply(small, { type: 'updateElement', id: 'web', fields: { name: '  ' } })).toBe(small)
  })

  it('deleting a container removes its components, their relationships and their positions', () => {
    const next = apply(small, { type: 'deleteElement', id: 'api' })
    expect(next.elements.map((e) => e.id)).toEqual(['user', 'sys', 'web', 'db'])
    expect(next.relationships.map((r) => r.id)).toEqual(['r-user-web'])
    expect(next.layout['component:api']).toEqual({})
  })

  it('adds a relationship but rejects self relationships and exact duplicates', () => {
    const next = apply(small, {
      type: 'addRelationship',
      id: 'r1',
      sourceId: 'web',
      targetId: 'db',
      description: 'Reads',
    })
    expect(next.relationships.at(-1)).toMatchObject({ id: 'r1', description: 'Reads' })
    expect(
      apply(next, { type: 'addRelationship', id: 'r2', sourceId: 'web', targetId: 'db', description: 'Reads' }),
    ).toBe(next)
    expect(
      apply(next, { type: 'addRelationship', id: 'r3', sourceId: 'web', targetId: 'db', description: 'Writes' })
        .relationships,
    ).toHaveLength(7)
    expect(
      apply(small, { type: 'addRelationship', id: 'r4', sourceId: 'web', targetId: 'web', description: 'x' }),
    ).toBe(small)
  })

  it('deletes several relationships at once (an implied edge)', () => {
    const next = apply(small, { type: 'deleteRelationships', ids: ['r-ctrl-repo', 'r-repo-db'] })
    expect(next.relationships.map((r) => r.id)).toEqual(['r-user-web', 'r-web-api', 'r-api-db'])
  })

  it('moves elements in one view without touching the model', () => {
    const next = apply(small, { type: 'moveElements', viewKey: 'component:api', positions: { repo: { x: 10, y: 20 } } })
    expect(next.layout['component:api']).toEqual({ ctrl: { x: 0, y: 0 }, repo: { x: 10, y: 20 } })
    expect(next.elements).toBe(small.elements)
    expect(changesModel({ type: 'moveElements', viewKey: 'x', positions: {} })).toBe(false)
  })
})
