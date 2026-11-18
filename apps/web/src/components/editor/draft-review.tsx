import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { X } from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'
import { DriftPanel } from './drift-panel'
import { SaveVersionDialog } from './save-version-dialog'
import type { ComposeDraft } from './compose-import-dialog'
import type { ModelInfo, ModelVersion, Point, Snapshot } from '#/model/types'
import { CompareWorkspace } from '#/components/compare/compare-workspace'
import { Button } from '#/components/ui/button'
import { unwrap } from '#/lib/core-result'
import { totalChanges } from '#/model/types'
import { compareSnapshots } from '#/server/core-api'

type DraftReviewProps = {
  readonly model: ModelInfo
  readonly version: ModelVersion | null
  readonly base: Snapshot
  readonly draft: ComposeDraft
  readonly viewKey: string | undefined
  readonly onViewChange: (key: string) => void
  readonly onDiscard: () => void
}

/**
 * Review of a compose draft in the compare view (current version → draft) with the drift findings. Accepting saves
 * exactly the draft that was shown, with the positions shown for the reviewed views, as a new version.
 */
export function DraftReview(props: DraftReviewProps) {
  const viewKey = props.viewKey ?? `container:${props.draft.targetSystemId}`
  const [shown, setShown] = useState<Readonly<Record<string, Readonly<Record<string, Point>>>>>({})
  const compare = useQuery({
    queryKey: ['compare-draft', props.model.id, props.version?.id, props.draft, viewKey],
    queryFn: async () =>
      unwrap(await compareSnapshots({ data: { base: props.base, head: props.draft.draft, view: viewKey } })),
    placeholderData: keepPreviousData,
    staleTime: Infinity,
  })
  const onPositions = useCallback(
    (key: string, positions: Readonly<Record<string, Point>>) =>
      setShown((all) => (all[key] === positions ? all : { ...all, [key]: positions })),
    [],
  )
  const accepted = useMemo<Snapshot>(
    () => ({ ...props.draft.draft, layout: { ...props.draft.draft.layout, ...shown } }),
    [props.draft.draft, shown],
  )
  const elements = useMemo(
    () => [...props.base.elements, ...props.draft.draft.elements],
    [props.base, props.draft.draft],
  )

  return (
    <CompareWorkspace
      title={`${props.model.name}: draft from docker-compose (v${props.version?.number ?? '—'} → draft)`}
      graph={compare}
      diff={props.draft.diff}
      elements={elements}
      onViewChange={props.onViewChange}
      onPositions={onPositions}
      panelTop={<DriftPanel drift={props.draft.drift} />}
      actions={
        <>
          <Button size="sm" variant="ghost" onClick={props.onDiscard}>
            <X /> Discard
          </Button>
          {totalChanges(props.draft.diff.summary) === 0 ? (
            <span className="text-xs text-muted-foreground">The draft changes nothing</span>
          ) : (
            <SaveVersionDialog
              modelId={props.model.id}
              parentVersionId={props.version?.id ?? null}
              snapshot={accepted}
              label="Accept draft"
              defaultMessage="Accepted draft from docker-compose"
            />
          )}
        </>
      }
    />
  )
}
