import { useMemo } from 'react'
import { resolvePositions } from '#/model/flow'
import { NODE_HEIGHT, NODE_WIDTH } from '#/model/node-size'
import type { CompareGraph, Point } from '#/model/types'

type PlacedPositions = {
  readonly positions: Readonly<Record<string, Point>> | null
  readonly placedByClient: boolean
}

const GAP = 60
const PER_COLUMN = 4

/** Positions for every node: the local layout, then Core's; unpositioned nodes go on a grid right of the others. */
export function usePlacedPositions(
  graph: CompareGraph | null,
  local: Readonly<Record<string, Point>> | undefined,
): PlacedPositions {
  return useMemo((): PlacedPositions => {
    if (!graph) return { positions: null, placedByClient: false }
    const resolved = resolvePositions(graph, local)
    const known: Record<string, Point> = Object.fromEntries(
      Object.entries(resolved).flatMap(([id, p]) => (p ? [[id, p]] : [])),
    )
    const missing = Object.keys(resolved).filter((id) => !known[id])
    const points = Object.values(known)
    const startX = points.length ? Math.max(...points.map((p) => p.x)) + NODE_WIDTH + GAP : 0
    const topY = points.length ? Math.min(...points.map((p) => p.y)) : 0
    missing.forEach((id, i) => {
      known[id] = {
        x: startX + Math.floor(i / PER_COLUMN) * (NODE_WIDTH + GAP),
        y: topY + (i % PER_COLUMN) * (NODE_HEIGHT + GAP),
      }
    })
    return { positions: known, placedByClient: missing.length > 0 }
  }, [graph, local])
}
