import { createFileRoute } from '@tanstack/react-router'
import { useSuspenseQuery } from '@tanstack/react-query'
import { z } from 'zod'
import { Editor } from '#/components/editor/editor'
import { modelQuery, versionQuery } from '#/lib/model-queries'
import { emptySnapshot } from '#/model/types'
import type { ModelInfo } from '#/model/types'

const Search = z.object({ view: z.string().optional() })

export const Route = createFileRoute('/_app/m/$modelId')({
  validateSearch: Search,
  loader: async ({ context, params }) => {
    const model = await context.queryClient.ensureQueryData(modelQuery(params.modelId))
    if (model.latestVersionId) await context.queryClient.ensureQueryData(versionQuery(model.latestVersionId))
  },
  component: ModelPage,
})

function ModelPage() {
  const { modelId } = Route.useParams()
  const { view } = Route.useSearch()
  const { data: model } = useSuspenseQuery(modelQuery(modelId))
  return model.latestVersionId ? (
    <LoadedEditor key={model.latestVersionId} model={model} versionId={model.latestVersionId} viewKey={view} />
  ) : (
    <Editor model={model} version={null} snapshot={emptySnapshot} viewKey={view} />
  )
}

function LoadedEditor({
  model,
  versionId,
  viewKey,
}: {
  readonly model: ModelInfo
  readonly versionId: string
  readonly viewKey: string | undefined
}) {
  const { data: version } = useSuspenseQuery(versionQuery(versionId))
  return <Editor model={model} version={version} snapshot={version.snapshot} viewKey={viewKey} />
}
