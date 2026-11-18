import { useQuery } from '@tanstack/react-query'
import { useMemo } from 'react'
import { resolvePositions } from '#/model/flow'
import { layoutView } from '#/model/layout'
import type { CompareGraph, Point } from '#/model/types'

type PlacedPositions = {
  readonly positions: Readonly<Record<string, Point>> | null
  readonly placedByClient: boolean
}

/** Positions for every node (local layout, then Core's, then ELK for the rest), or null while ELK is placing. */
export function usePlacedPositions(
  graph: CompareGraph | null,
  local: Readonly<Record<string, Point>> | undefined,
): PlacedPositions {
  const { known, missing } = useMemo(() => {
    const resolved = Object.entries(graph ? resolvePositions(graph, local) : {})
    return {
      known: Object.fromEntries(resolved.flatMap(([id, p]) => (p ? [[id, p]] : []))),
      missing: resolved.flatMap(([id, p]) => (p ? [] : [id])),
    }
  }, [graph, local])
  const placed = useQuery({
    queryKey: ['layout', graph?.view.key, graph?.edges.map((e) => e.id), known, missing],
    queryFn: () =>
      layoutView(
        graph!.nodes.map((n) => n.elementId),
        graph!.edges,
        known,
      ),
    enabled: graph !== null && missing.length > 0,
    staleTime: Infinity,
  })

  return useMemo(() => {
    if (!graph) return { positions: null, placedByClient: false }
    if (missing.length === 0) return { positions: known, placedByClient: false }
    return { positions: placed.data ? { ...placed.data, ...known } : null, placedByClient: true }
  }, [graph, known, missing, placed.data])
}
