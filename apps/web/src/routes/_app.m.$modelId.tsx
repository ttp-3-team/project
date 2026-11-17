import { createFileRoute } from '@tanstack/react-router'
import { useSuspenseQuery } from '@tanstack/react-query'
import { modelQuery } from '#/lib/model-queries'

export const Route = createFileRoute('/_app/m/$modelId')({
  loader: ({ context, params }) => context.queryClient.ensureQueryData(modelQuery(params.modelId)),
  component: ModelPage,
})

function ModelPage() {
  const { modelId } = Route.useParams()
  const { data: model } = useSuspenseQuery(modelQuery(modelId))
  return (
    <main className="p-8">
      <h1 className="text-2xl font-semibold">{model.name}</h1>
      <p className="text-muted-foreground">
        {model.latestVersionNumber ? `Version v${model.latestVersionNumber}` : 'No versions'}
      </p>
    </main>
  )
}
