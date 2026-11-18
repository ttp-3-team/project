import { BaseEdge, EdgeLabelRenderer, Position, getSmoothStepPath, useInternalNode } from '@xyflow/react'
import type { Edge, EdgeProps, InternalNode, Node } from '@xyflow/react'

type FloatingEdgeData = {
  readonly label: string
}

/** React Flow edge type for C4 relationships. */
export type FloatingFlowEdge = Edge<FloatingEdgeData, 'floating'>

const LABEL_MAX = 32

function center(node: InternalNode<Node>) {
  const { x, y } = node.internals.positionAbsolute
  const w = node.measured.width ?? 0
  const h = node.measured.height ?? 0
  return { x: x + w / 2, y: y + h / 2, w, h }
}

function attachment(node: InternalNode<Node>, other: InternalNode<Node>): { x: number; y: number; position: Position } {
  const a = center(node)
  const b = center(other)
  const dx = b.x - a.x
  const dy = b.y - a.y
  if (Math.abs(dx) * a.h > Math.abs(dy) * a.w) {
    const side = dx > 0 ? Position.Right : Position.Left
    return { x: a.x + Math.sign(dx) * (a.w / 2), y: a.y + (dy / Math.abs(dx || 1)) * (a.w / 2), position: side }
  }
  const side = dy > 0 ? Position.Bottom : Position.Top
  return { x: a.x + (dx / Math.abs(dy || 1)) * (a.h / 2), y: a.y + Math.sign(dy) * (a.h / 2), position: side }
}

/**
 * A relationship drawn between the nearest sides of its two nodes (React Flow's "floating edges"), as an orthogonal
 * smooth-step path with a one-line label that never sits on a box.
 */
export function FloatingEdge(props: EdgeProps<FloatingFlowEdge>) {
  const source = useInternalNode(props.source)
  const target = useInternalNode(props.target)
  if (!source || !target) return null
  const s = attachment(source, target)
  const t = attachment(target, source)
  const [path, labelX, labelY] = getSmoothStepPath({
    sourceX: s.x,
    sourceY: s.y,
    sourcePosition: s.position,
    targetX: t.x,
    targetY: t.y,
    targetPosition: t.position,
    borderRadius: 10,
  })
  const label = props.data?.label ?? ''
  const short = label.length > LABEL_MAX ? `${label.slice(0, LABEL_MAX - 1)}…` : label
  return (
    <>
      <BaseEdge id={props.id} path={path} markerEnd={props.markerEnd} style={props.style} interactionWidth={16} />
      {short && (
        <EdgeLabelRenderer>
          <div
            title={label}
            className="nodrag nopan pointer-events-auto absolute max-w-56 truncate rounded bg-card/95 px-1.5 py-0.5 text-[10px] text-foreground shadow-sm"
            style={{ transform: `translate(-50%, -50%) translate(${labelX}px, ${labelY}px)` }}
          >
            {short}
          </div>
        </EdgeLabelRenderer>
      )}
    </>
  )
}
