import { useDebouncedValue } from '@tanstack/react-pacer'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useMemo } from 'react'
import type { Snapshot } from '#/model/types'
import { unwrap } from '#/lib/core-result'
import { deriveViews, validateSnapshot } from '#/server/core-api'

const DEBOUNCE_MS = 150

/**
 * Derived views and validation of the unsaved snapshot, computed by Core. Requests are debounced and keyed by the
 * model revision (layout-only edits do not bump it); the previous result stays on screen while a request is in flight.
 */
export function useCoreViews(scope: string, snapshot: Snapshot, revision: number) {
  const current = useMemo(() => ({ snapshot, revision }), [revision])
  const [settled] = useDebouncedValue(current, { wait: DEBOUNCE_MS })

  const views = useQuery({
    queryKey: ['views', scope, settled.revision],
    queryFn: async () => unwrap(await deriveViews({ data: { snapshot: settled.snapshot } })),
    placeholderData: keepPreviousData,
    staleTime: Infinity,
  })
  const validation = useQuery({
    queryKey: ['validate', scope, settled.revision],
    queryFn: async () => unwrap(await validateSnapshot({ data: { snapshot: settled.snapshot } })),
    placeholderData: keepPreviousData,
    staleTime: Infinity,
  })
  return {
    views,
    problems: validation.data?.problems ?? [],
    pending: views.isFetching || revision !== settled.revision,
  }
}
