import '@xyflow/react/dist/style.css'
import {
  Background,
  ConnectionMode,
  Controls,
  MarkerType,
  MiniMap,
  ReactFlow,
  ReactFlowProvider,
  applyNodeChanges,
  useReactFlow,
} from '@xyflow/react'
import { useEffect, useMemo, useState } from 'react'
import { C4Node } from './c4-node'
import { FloatingEdge } from './floating-edge'
import type { NodeChange } from '@xyflow/react'
import type { FloatingFlowEdge } from './floating-edge'
import type { C4FlowNode } from './c4-node'
import type { ChangeStatus, CompareGraph, Point } from '#/model/types'
import { edgeLabel } from '#/model/flow'

const nodeTypes = { c4: C4Node }
const edgeTypes = { floating: FloatingEdge }

const MIN_ZOOM = 0.3

const FIT_DELAY_MS = 30

const EDGE_COLOR: Readonly<Record<ChangeStatus, string>> = {
  added: 'var(--change-added)',
  removed: 'var(--change-removed)',
  modified: 'var(--change-modified)',
  unchanged: 'var(--muted-foreground)',
}

type ModelCanvasProps = {
  readonly graph: CompareGraph
  readonly positions: Readonly<Record<string, Point>>
  readonly selectedId: string | null
  readonly fitKey: string
  readonly readOnly?: boolean
  readonly onSelect?: (selection: { readonly kind: 'element' | 'edge'; readonly id: string } | null) => void
  readonly onOpen?: (childViewKey: string) => void
  readonly onMoved?: (positions: Readonly<Record<string, Point>>) => void
  readonly onConnect?: (sourceId: string, targetId: string) => void
  readonly onDeleteElements?: (ids: ReadonlyArray<string>) => void
}

function toNodes(
  graph: CompareGraph,
  positions: Readonly<Record<string, Point>>,
  selectedId: string | null,
): Array<C4FlowNode> {
  return graph.nodes.map((data) => ({
    id: data.elementId,
    type: 'c4',
    position: positions[data.elementId] ?? { x: 0, y: 0 },
    selected: data.elementId === selectedId,
    data,
  }))
}

function toEdges(graph: CompareGraph, selectedId: string | null): Array<FloatingFlowEdge> {
  return graph.edges.map((e) => {
    const color = EDGE_COLOR[e.status]
    return {
      id: e.id,
      source: e.sourceId,
      target: e.targetId,
      type: 'floating',
      data: { label: edgeLabel(e) },
      selected: e.id === selectedId,
      deletable: false,
      markerEnd: { type: MarkerType.ArrowClosed, color, width: 18, height: 18 },
      style: {
        stroke: color,
        strokeWidth: e.status === 'unchanged' ? 1.5 : 2.5,
        ...(e.implied || e.status === 'removed' ? { strokeDasharray: '6 4' } : {}),
      },
    }
  })
}

function CanvasInner(props: ModelCanvasProps) {
  const { graph, positions, selectedId, readOnly = false, onOpen } = props
  const [nodes, setNodes] = useState(() => toNodes(graph, positions, selectedId))
  const edges = useMemo(() => toEdges(graph, selectedId), [graph, selectedId])
  const flow = useReactFlow()

  useEffect(
    () =>
      setNodes((previous) => {
        const measured = new Map(previous.map((n) => [n.id, n.measured]))
        return toNodes(graph, positions, selectedId).map((n) => {
          const m = measured.get(n.id)
          return m ? { ...n, measured: m } : n
        })
      }),
    [graph, positions, selectedId],
  )
  useEffect(() => {
    const t = setTimeout(() => void flow.fitView({ padding: 0.15, duration: 200 }), FIT_DELAY_MS)
    return () => clearTimeout(t)
  }, [props.fitKey, flow])

  return (
    <ReactFlow<C4FlowNode, FloatingFlowEdge>
      nodes={nodes}
      edges={edges}
      nodeTypes={nodeTypes}
      edgeTypes={edgeTypes}
      colorMode="dark"
      className={readOnly ? 'archlens-read-only' : undefined}
      connectionMode={ConnectionMode.Loose}
      minZoom={MIN_ZOOM}
      proOptions={{ hideAttribution: true }}
      nodesDraggable={!readOnly}
      nodesConnectable={!readOnly}
      deleteKeyCode={readOnly ? null : ['Backspace', 'Delete']}
      onNodesChange={(changes: Array<NodeChange<C4FlowNode>>) => {
        const removed = changes.flatMap((c) => (c.type === 'remove' ? [c.id] : []))
        if (removed.length > 0) props.onDeleteElements?.(removed)
        setNodes((ns) =>
          applyNodeChanges(
            changes.filter((c) => c.type !== 'remove'),
            ns,
          ),
        )
      }}
      onNodeDragStop={(_, __, dragged) => {
        props.onMoved?.(
          Object.fromEntries(dragged.map((n) => [n.id, { x: Math.round(n.position.x), y: Math.round(n.position.y) }])),
        )
      }}
      onNodeClick={(_, node) => props.onSelect?.({ kind: 'element', id: node.id })}
      onNodeDoubleClick={(_, node) => {
        if (node.data.childViewKey) onOpen?.(node.data.childViewKey)
      }}
      onEdgeClick={(_, edge) => props.onSelect?.({ kind: 'edge', id: edge.id })}
      onPaneClick={() => props.onSelect?.(null)}
      zoomOnDoubleClick={false}
      onConnect={(c) => props.onConnect?.(c.source, c.target)}
    >
      <Background gap={24} />
      <Controls showInteractive={false} />
      <MiniMap pannable zoomable className="!bg-card" />
    </ReactFlow>
  )
}

/** Canvas that draws one C4 view from Core's graph. Positions are resolved by the caller. */
export function ModelCanvas(props: ModelCanvasProps) {
  return (
    <ReactFlowProvider>
      <CanvasInner {...props} />
    </ReactFlowProvider>
  )
}
