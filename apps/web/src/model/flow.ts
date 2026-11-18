import type { CompareEdge, CompareGraph, Point, ViewGraph } from './types'

/** Core's editor view as a compare graph with nothing changed, so the canvas draws one shape. */
export function fromViewGraph(view: ViewGraph): CompareGraph {
  return {
    view: view.view,
    nodes: view.nodes.map((n) => ({ ...n, status: 'unchanged', changes: [] })),
    edges: view.edges.map((e) => ({ ...e, status: 'unchanged', changes: [] })),
  }
}

/** Edge label: description, number of further relationships, technology and the implied marker. */
export function edgeLabel(e: CompareEdge): string {
  const more = e.relationshipIds.length > 1 ? ` (+${e.relationshipIds.length - 1})` : ''
  const tech = e.technology ? ` [${e.technology}]` : ''
  return `${e.description}${more}${tech}${e.implied ? ' · implied' : ''}`
}

/** Positions to draw with: the editor's local layout wins (it moves while dragging), then Core's; null = not placed. */
export function resolvePositions(
  graph: CompareGraph,
  local: Readonly<Record<string, Point>> | undefined,
): Record<string, Point | null> {
  return Object.fromEntries(graph.nodes.map((n) => [n.elementId, local?.[n.elementId] ?? n.position]))
}
