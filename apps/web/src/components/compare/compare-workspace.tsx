import { ArrowRight, Eye } from 'lucide-react'
import { useEffect, useMemo } from 'react'
import type { UseQueryResult } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import type { CompareGraph, DiffResult, Element, FieldChange, Point } from '#/model/types'
import { ModelCanvas } from '#/components/canvas/model-canvas'
import { LoadingState, StateBlock } from '#/components/states'
import { Badge } from '#/components/ui/badge'
import { Separator } from '#/components/ui/separator'
import { Breadcrumbs } from '#/components/views/breadcrumbs'
import { usePlacedPositions } from '#/components/views/use-placed-positions'
import { errorMessage } from '#/lib/core-result'
import { cn } from '#/lib/utils'
import { KIND_LABEL, fieldLabel, totalChanges } from '#/model/types'

type CompareWorkspaceProps = {
  readonly title: string
  readonly graph: UseQueryResult<CompareGraph>
  readonly diff: DiffResult
  readonly elements: ReadonlyArray<Element>
  readonly onViewChange: (viewKey: string) => void
  readonly onPositions?: (viewKey: string, positions: Readonly<Record<string, Point>>) => void
  readonly actions?: ReactNode
  readonly panelTop?: ReactNode
}

const STATUSES = ['added', 'removed', 'modified'] as const

const STATUS = {
  added: { dot: 'bg-added', word: 'Added' },
  removed: { dot: 'border border-dashed border-removed', word: 'Removed' },
  modified: { dot: 'bg-modified', word: 'Modified' },
} as const

/**
 * Visual comparison of two snapshots, drawn exactly as Core computed it: union graph with base positions,
 * colour-coded statuses and field changes; a grouped change list and the affected views with Core's reasons.
 */
export function CompareWorkspace(props: CompareWorkspaceProps) {
  const { diff, onPositions } = props
  const graph = props.graph.data ?? null
  const placed = usePlacedPositions(graph, undefined)
  useEffect(() => {
    if (graph && placed.positions) onPositions?.(graph.view.key, placed.positions)
  }, [graph, placed.positions, onPositions])
  const names = useMemo(() => new Map(props.elements.map((e) => [e.id, e.name])), [props.elements])
  const nameOf = (id: string) => names.get(id) ?? id

  const affected = diff.views.filter((v) => v.affected)
  const s = diff.summary
  const total = totalChanges(s)

  return (
    <div className="flex h-full flex-col">
      <div className="flex h-11 shrink-0 items-center gap-3 border-b px-4 text-sm">
        <span className="font-medium">{props.title}</span>
        {graph && (
          <div className="ml-4">
            <Breadcrumbs view={graph.view} onOpen={props.onViewChange} />
          </div>
        )}
        <div className="ml-4 hidden items-center gap-3 text-xs text-muted-foreground lg:flex">
          {STATUSES.map((status) => (
            <span key={status} className="flex items-center gap-1">
              <span className={cn('size-2.5 rounded-full', STATUS[status].dot)} /> {STATUS[status].word}
            </span>
          ))}
        </div>
        <div className="ml-auto flex items-center gap-2">{props.actions}</div>
      </div>
      <div className="flex min-h-0 flex-1">
        <div className="relative min-w-0 flex-1">
          {props.graph.isError && (
            <StateBlock icon={null} title="Could not build the comparison">
              {errorMessage(props.graph.error)}
            </StateBlock>
          )}
          {graph && placed.positions && (
            <ModelCanvas
              graph={graph}
              positions={placed.positions}
              fitKey={`${graph.view.key}:${placed.placedByClient ? 'client' : 'stored'}`}
              selectedId={null}
              readOnly
              onOpen={props.onViewChange}
            />
          )}
          {!props.graph.isError && (!graph || !placed.positions) && <LoadingState title="Building the comparison…" />}
        </div>
        <aside className="flex w-96 shrink-0 flex-col gap-4 overflow-y-auto border-l p-4 text-sm">
          {props.panelTop}
          <section>
            <h2 className="font-medium">Changes ({total})</h2>
            <p className="text-xs text-muted-foreground">
              Elements: +{s.elementsAdded} −{s.elementsRemoved} ~{s.elementsModified} · Relationships: +
              {s.relationshipsAdded} −{s.relationshipsRemoved} ~{s.relationshipsModified}
            </p>
          </section>
          {total === 0 && <p className="text-muted-foreground">The models are the same (layout is ignored).</p>}
          {STATUSES.some((status) => diff.elements[status].length > 0) && (
            <section className="flex flex-col gap-1.5">
              <h3 className="text-xs uppercase tracking-wide text-muted-foreground">Elements</h3>
              {STATUSES.flatMap((status) =>
                diff.elements[status].map((e) => (
                  <ChangeRow
                    key={e.id}
                    status={status}
                    label={e.name}
                    hint={KIND_LABEL[e.kind].toLowerCase()}
                    changes={'changes' in e ? e.changes : []}
                  />
                )),
              )}
            </section>
          )}
          {STATUSES.some((status) => diff.relationships[status].length > 0) && (
            <section className="flex flex-col gap-1.5">
              <h3 className="text-xs uppercase tracking-wide text-muted-foreground">Relationships</h3>
              {STATUSES.flatMap((status) =>
                diff.relationships[status].map((r) => (
                  <ChangeRow
                    key={r.id}
                    status={status}
                    label={
                      <span className="inline-flex flex-wrap items-center gap-1">
                        {nameOf(r.sourceId)} <ArrowRight className="size-3" /> {nameOf(r.targetId)}
                      </span>
                    }
                    hint={status === 'modified' ? '' : r.description}
                    changes={'changes' in r ? r.changes : []}
                  />
                )),
              )}
            </section>
          )}
          <Separator />
          <section className="flex flex-col gap-2">
            <h3 className="text-xs uppercase tracking-wide text-muted-foreground">
              Affected views: {affected.length} / {diff.views.length}
            </h3>
            {affected.map((v) => (
              <button
                key={v.key}
                type="button"
                onClick={() => props.onViewChange(v.key)}
                className={cn(
                  'rounded-md border p-2.5 text-left transition-colors hover:border-primary/60',
                  v.key === graph?.view.key && 'border-primary bg-primary/10',
                )}
              >
                <div className="flex items-center gap-2 font-medium">
                  <Eye className="size-3.5" /> {v.title}
                  {v.status !== 'changed' && (
                    <Badge variant="secondary">{v.status === 'added' ? 'new' : 'removed'}</Badge>
                  )}
                </div>
                <ul className="mt-1 list-disc pl-5 text-xs text-muted-foreground">
                  {v.reasons.map((r) => (
                    <li key={r}>{r}</li>
                  ))}
                </ul>
              </button>
            ))}
            {affected.length === 0 && <p className="text-xs text-muted-foreground">No view changed.</p>}
          </section>
        </aside>
      </div>
    </div>
  )
}

function ChangeRow(props: {
  readonly status: (typeof STATUSES)[number]
  readonly label: ReactNode
  readonly hint: string
  readonly changes: ReadonlyArray<FieldChange>
}) {
  return (
    <div className="rounded-md border px-2.5 py-2">
      <div className="flex items-center gap-2">
        <span className={cn('size-2 shrink-0 rounded-full', STATUS[props.status].dot)} />
        <span className="font-medium">{props.label}</span>
        <span className="ml-auto shrink-0 text-xs text-muted-foreground">{STATUS[props.status].word}</span>
      </div>
      {props.hint && <div className="pl-4 text-xs text-muted-foreground">{props.hint}</div>}
      {props.changes.map((c) => (
        <div key={c.field} className="pl-4 text-xs text-muted-foreground">
          {fieldLabel(c.field)}: <span className="line-through">{c.from || '—'}</span> → {c.to || '—'}
        </div>
      ))}
    </div>
  )
}
