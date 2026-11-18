import { NODE_HEIGHT, NODE_WIDTH } from './node-size'
import type { ELK } from 'elkjs/lib/elk-api'
import type { Point } from './types'

let elkInstance: Promise<ELK> | undefined

function elk(): Promise<ELK> {
  elkInstance ??= Promise.all([import('elkjs/lib/elk-api.js'), import('elkjs/lib/elk-worker.min.js?url')]).then(
    ([api, worker]) => new api.default({ workerUrl: worker.default }),
  )
  return elkInstance
}

const GAP_X = 120
const GAP_Y = 50

/**
 * Lay out a view with ELK (layered, left to right) and return a position for every node. Nodes in `fixed`
 * keep their position; the others are placed in ELK's order in columns to the right of the fixed ones.
 * With nothing fixed, the whole view gets ELK's layout ("Auto layout").
 */
export async function layoutView(
  nodeIds: ReadonlyArray<string>,
  edges: ReadonlyArray<{ readonly id: string; readonly sourceId: string; readonly targetId: string }>,
  fixed: Readonly<Record<string, Point>>,
): Promise<Record<string, Point>> {
  const graph = await (
    await elk()
  ).layout({
    id: 'view',
    layoutOptions: {
      'elk.algorithm': 'layered',
      'elk.direction': 'RIGHT',
      'elk.spacing.nodeNode': String(GAP_Y),
      'elk.layered.spacing.nodeNodeBetweenLayers': String(GAP_X),
      'elk.layered.nodePlacement.strategy': 'BRANDES_KOEPF',
    },
    children: nodeIds.map((id) => ({ id, width: NODE_WIDTH, height: NODE_HEIGHT })),
    edges: edges.map((e) => ({ id: e.id, sources: [e.sourceId], targets: [e.targetId] })),
  })
  const elkPositions = Object.fromEntries(
    (graph.children ?? []).map((n) => [n.id, { x: Math.round(n.x ?? 0), y: Math.round(n.y ?? 0) }]),
  )
  const fixedPoints = Object.values(fixed)
  if (fixedPoints.length === 0) return elkPositions

  const result: Record<string, Point> = { ...fixed }
  const unplaced = nodeIds
    .filter((id) => !fixed[id])
    .sort((a, b) => (elkPositions[a]?.y ?? 0) - (elkPositions[b]?.y ?? 0))
  const startX = Math.max(...fixedPoints.map((p) => p.x)) + NODE_WIDTH + GAP_X
  const topY = Math.min(...fixedPoints.map((p) => p.y))
  const perColumn = 4
  unplaced.forEach((id, i) => {
    result[id] = {
      x: startX + Math.floor(i / perColumn) * (NODE_WIDTH + GAP_X),
      y: topY + (i % perColumn) * (NODE_HEIGHT + GAP_Y),
    }
  })
  return result
}
