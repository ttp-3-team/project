import { describe, expect, it } from 'vitest'
import { edgeLabel, fromViewGraph, resolvePositions } from './flow'
import type { ViewGraph } from './types'

const view: ViewGraph = {
  view: {
    key: 'container:sys',
    kind: 'container',
    title: 'Containers: Shop',
    label: 'Shop',
    scopeId: 'sys',
    ancestors: [{ key: 'context', title: 'System context', label: 'Context' }],
  },
  nodes: [
    {
      elementId: 'web',
      name: 'Web',
      kind: 'container',
      technology: 'React',
      description: '',
      external: false,
      outside: false,
      childViewKey: 'component:web',
      hasChildren: false,
      position: { x: 1, y: 2 },
    },
    {
      elementId: 'user',
      name: 'User',
      kind: 'person',
      technology: '',
      description: '',
      external: false,
      outside: true,
      childViewKey: null,
      hasChildren: false,
      position: null,
    },
  ],
  edges: [
    {
      id: 'user|web',
      sourceId: 'user',
      targetId: 'web',
      description: 'Uses',
      technology: 'HTTPS',
      implied: true,
      relationshipIds: ['a', 'b'],
    },
  ],
}

describe('canvas graph', () => {
  it('draws a Core view as a compare graph with nothing changed', () => {
    const graph = fromViewGraph(view)
    expect(graph.nodes.map((n) => [n.elementId, n.status, n.changes])).toEqual([
      ['web', 'unchanged', []],
      ['user', 'unchanged', []],
    ])
    expect(graph.edges.map((e) => e.status)).toEqual(['unchanged'])
  })

  it('labels edges with description, count, technology and the implied marker', () => {
    expect(edgeLabel(fromViewGraph(view).edges[0]!)).toBe('Uses (+1) [HTTPS] · implied')
  })

  it('prefers the local layout, then Core positions', () => {
    const graph = fromViewGraph(view)
    expect(resolvePositions(graph, { web: { x: 9, y: 9 } })).toEqual({ web: { x: 9, y: 9 }, user: null })
    expect(resolvePositions(graph, undefined)).toEqual({ web: { x: 1, y: 2 }, user: null })
  })
})
