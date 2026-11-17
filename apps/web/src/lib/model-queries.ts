import { queryOptions } from '@tanstack/react-query'
import { unwrap } from './core-result'
import { getModel, getVersion, listVersions } from '#/server/core-api'

/** Query for a model with its latest version number. */
export const modelQuery = (modelId: string) =>
  queryOptions({ queryKey: ['model', modelId], queryFn: async () => unwrap(await getModel({ data: { modelId } })) })

/** Query for one version with its snapshot. Versions are immutable, so the result never goes stale. */
export const versionQuery = (versionId: string) =>
  queryOptions({
    queryKey: ['version', versionId],
    staleTime: Infinity,
    queryFn: async () => unwrap(await getVersion({ data: { versionId } })),
  })

/** Query for the version list of a model (newest first). */
export const versionsQuery = (modelId: string) =>
  queryOptions({
    queryKey: ['model', modelId, 'versions'],
    queryFn: async () => unwrap(await listVersions({ data: { modelId } })),
  })
