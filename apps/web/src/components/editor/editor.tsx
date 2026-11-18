import { useNavigate } from '@tanstack/react-router'
import { Download, LoaderCircle, Workflow } from 'lucide-react'
import { useMemo, useReducer, useState } from 'react'
import { toast } from 'sonner'
import { Inspector } from './inspector'
import { SaveVersionDialog } from './save-version-dialog'
import { useCoreViews } from './use-core-views'
import type { Selection } from './inspector'
import type { EditorAction } from '#/model/editor'
import type { ModelInfo, ModelVersion, Snapshot, ViewKind } from '#/model/types'
import { ModelCanvas } from '#/components/canvas/model-canvas'
import { LoadingState, StateBlock } from '#/components/states'
import { Button } from '#/components/ui/button'
import { Breadcrumbs } from '#/components/views/breadcrumbs'
import { usePlacedPositions } from '#/components/views/use-placed-positions'
import { downloadJson, fileNameOf } from '#/lib/download'
import { errorMessage } from '#/lib/core-result'
import { apply, changesModel, differs } from '#/model/editor'
import { fromViewGraph } from '#/model/flow'
import { layoutView } from '#/model/layout'

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

type State = { readonly snapshot: Snapshot; readonly revision: number }

function reduce(state: State, action: EditorAction): State {
  const snapshot = apply(state.snapshot, action)
  if (snapshot === state.snapshot) return state
  return { snapshot, revision: changesModel(action) ? state.revision + 1 : state.revision }
}

/**
 * Model editor. The reducer holds edit commands only; what to draw (views, breadcrumbs, levels) and the model's
 * problems come from Core.
 */
export function Editor(props: EditorProps) {
  const [state, dispatch] = useReducer(reduce, { snapshot: props.snapshot, revision: 0 })
  const { snapshot } = state
  const [selection, setSelection] = useState<Selection>(null)
  const [fitBump, setFitBump] = useState(0)
  const [layingOut, setLayingOut] = useState(false)
  const dirty = differs(props.snapshot, snapshot)
  const navigate = useNavigate()

  const { views, problems, pending } = useCoreViews(
    `${props.model.id}:${props.version?.id ?? 'new'}`,
    snapshot,
    state.revision,
  )
  const viewKey = props.viewKey ?? 'context'
  const view = views.data?.find((v) => v.view.key === viewKey) ?? views.data?.find((v) => v.view.key === 'context')
  const graph = useMemo(() => (view ? fromViewGraph(view) : null), [view])
  const local = view ? snapshot.layout[view.view.key] : undefined
  const placed = usePlacedPositions(graph, local)

  const openView = (key: string) => {
    setSelection(null)
    void navigate({ to: '/m/$modelId', params: { modelId: props.model.id }, search: { view: key } })
  }

  const saveBlocked = !dirty ? 'no changes' : problems.length > 0 ? 'the model has problems' : null

  return (
    <div className="flex h-full flex-col">
      <div className="flex h-11 shrink-0 items-center gap-3 border-b px-4 text-sm">
        <span className="font-medium">{props.model.name}</span>
        <span className="text-muted-foreground">{props.version ? `v${props.version.number}` : 'no versions'}</span>
        {dirty && <span className="rounded bg-modified/20 px-1.5 py-0.5 text-xs text-modified">unsaved changes</span>}
        {view && (
          <div className="ml-4">
            <Breadcrumbs view={view.view} onOpen={openView} />
          </div>
        )}
        {pending && (
          <LoaderCircle className="size-3.5 animate-spin text-muted-foreground" aria-label="Updating views" />
        )}
        <div className="ml-auto flex items-center gap-1">
          <Button
            size="sm"
            variant="ghost"
            disabled={layingOut || !graph}
            onClick={async () => {
              if (!graph) return
              setLayingOut(true)
              try {
                const positions = await layoutView(
                  graph.nodes.map((n) => n.elementId),
                  graph.edges,
                  {},
                )
                dispatch({ type: 'replaceLayout', viewKey: graph.view.key, positions })
                setFitBump((n) => n + 1)
              } catch (error) {
                toast.error(`Auto layout failed: ${errorMessage(error)}`)
              } finally {
                setLayingOut(false)
              }
            }}
          >
            <Workflow /> Auto layout
          </Button>
          <Button
            size="sm"
            variant="ghost"
            disabled={!props.version}
            title={
              props.version
                ? dirty
                  ? 'Exports the saved version, without unsaved changes'
                  : undefined
                : 'No saved version yet'
            }
            onClick={() => {
              if (!props.version) return
              downloadJson(`archlens-${fileNameOf(props.model.name)}-v${props.version.number}.json`, props.version)
            }}
          >
            <Download /> Export
          </Button>
          <SaveVersionDialog
            modelId={props.model.id}
            parentVersionId={props.version?.id ?? null}
            snapshot={snapshot}
            disabled={saveBlocked !== null}
          />
          {saveBlocked && <span className="text-xs text-muted-foreground">({saveBlocked})</span>}
        </div>
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
              fitKey={`${graph.view.key}:${placed.placedByClient ? 'client' : 'stored'}:${fitBump}`}
              selectedId={selection?.id ?? null}
              onSelect={setSelection}
              onOpen={openView}
              onMoved={(moved) => {
                const unstored = Object.fromEntries(
                  Object.entries(placed.positions ?? {}).filter(([id]) => !local?.[id]),
                )
                dispatch({
                  type: 'moveElements',
                  viewKey: graph.view.key,
                  positions: { ...unstored, ...moved },
                })
              }}
              onConnect={(sourceId, targetId) =>
                dispatch({
                  type: 'addRelationship',
                  id: crypto.randomUUID(),
                  sourceId,
                  targetId,
                  description: 'Uses',
                })
              }
              onDeleteElements={(ids) => {
                for (const id of ids) dispatch({ type: 'deleteElement', id })
                setSelection(null)
              }}
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
        {view && (
          <Inspector
            snapshot={snapshot}
            view={view}
            selection={selection}
            problems={problems}
            dispatch={dispatch}
            onOpen={openView}
            onSelect={setSelection}
          />
        )}
      </div>
    </div>
  )
}
