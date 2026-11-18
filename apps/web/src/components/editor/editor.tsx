import { useNavigate } from '@tanstack/react-router'
import { LoaderCircle } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useCoreViews } from './use-core-views'
import type { ModelInfo, ModelVersion, Snapshot, ViewKind } from '#/model/types'
import { ModelCanvas } from '#/components/canvas/model-canvas'
import { LoadingState, StateBlock } from '#/components/states'
import { Breadcrumbs } from '#/components/views/breadcrumbs'
import { usePlacedPositions } from '#/components/views/use-placed-positions'
import { errorMessage } from '#/lib/core-result'
import { fromViewGraph } from '#/model/flow'

const EMPTY: Readonly<Record<ViewKind, readonly [string, string]>> = {
  context: ['The model is empty', 'Add the first software system or person in the panel on the right.'],
  container: ['Nothing on this level yet', 'Add the first container in the panel on the right.'],
  component: ['Nothing on this level yet', 'Add the first component in the panel on the right.'],
}

type EditorProps = {
  readonly model: ModelInfo
  readonly version: ModelVersion | null
  readonly snapshot: Snapshot
  readonly viewKey: string | undefined
}

/** Model editor: draws the current C4 view exactly as Core derived it. */
export function Editor(props: EditorProps) {
  const snapshot = props.snapshot
  const [selection, setSelection] = useState<string | null>(null)
  const navigate = useNavigate()

  const { views, pending } = useCoreViews(`${props.model.id}:${props.version?.id ?? 'new'}`, snapshot, 0)
  const viewKey = props.viewKey ?? 'context'
  const view = views.data?.find((v) => v.view.key === viewKey) ?? views.data?.find((v) => v.view.key === 'context')
  const graph = useMemo(() => (view ? fromViewGraph(view) : null), [view])
  const local = view ? snapshot.layout[view.view.key] : undefined
  const placed = usePlacedPositions(graph, local)

  const openView = (key: string) => {
    setSelection(null)
    void navigate({ to: '/m/$modelId', params: { modelId: props.model.id }, search: { view: key } })
  }

  return (
    <div className="flex h-full flex-col">
      <div className="flex h-11 shrink-0 items-center gap-3 border-b px-4 text-sm">
        <span className="font-medium">{props.model.name}</span>
        <span className="text-muted-foreground">{props.version ? `v${props.version.number}` : 'no versions'}</span>
        {view && (
          <div className="ml-4">
            <Breadcrumbs view={view.view} onOpen={openView} />
          </div>
        )}
        {pending && (
          <LoaderCircle className="size-3.5 animate-spin text-muted-foreground" aria-label="Updating views" />
        )}
      </div>
      <div className="flex min-h-0 flex-1">
        <div className="relative min-w-0 flex-1">
          {views.isError && (
            <StateBlock icon={null} title="Could not get the views from Core">
              {errorMessage(views.error)}
            </StateBlock>
          )}
          {graph && placed.positions && graph.nodes.length > 0 && (
            <ModelCanvas
              graph={graph}
              positions={placed.positions}
              fitKey={`${graph.view.key}:${placed.placedByClient ? 'client' : 'stored'}`}
              selectedId={selection}
              readOnly
              onSelect={(s) => setSelection(s?.id ?? null)}
              onOpen={openView}
            />
          )}
          {graph && !placed.positions && graph.nodes.length > 0 && <LoadingState title="Laying out elements…" />}
          {view && graph?.nodes.length === 0 && (
            <StateBlock icon={null} title={EMPTY[view.view.kind][0]}>
              {EMPTY[view.view.kind][1]}
            </StateBlock>
          )}
          {!graph && !views.isError && <LoadingState title="Loading views…" />}
        </div>
      </div>
    </div>
  )
}
