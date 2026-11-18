import { Link, createFileRoute } from '@tanstack/react-router'
import { keepPreviousData, queryOptions, useQuery, useSuspenseQuery } from '@tanstack/react-query'
import { ArrowLeft } from 'lucide-react'
import { useMemo } from 'react'
import { z } from 'zod'
import { CompareWorkspace } from '#/components/compare/compare-workspace'
import { Button } from '#/components/ui/button'
import { unwrap } from '#/lib/core-result'
import { modelQuery, versionQuery } from '#/lib/model-queries'
import { compareVersions, diffVersions } from '#/server/core-api'

const Search = z.object({ from: z.uuid(), to: z.uuid(), view: z.string().optional() })

const diffQuery = (modelId: string, from: string, to: string) =>
  queryOptions({
    queryKey: ['diff', modelId, from, to],
    staleTime: Infinity,
    queryFn: async () => unwrap(await diffVersions({ data: { modelId, from, to } })),
  })

const compareQuery = (modelId: string, from: string, to: string, view: string) =>
  queryOptions({
    queryKey: ['compare', modelId, from, to, view],
    staleTime: Infinity,
    queryFn: async () => unwrap(await compareVersions({ data: { modelId, from, to, view } })),
  })

export const Route = createFileRoute('/_app/m/$modelId_/compare')({
  validateSearch: Search,
  loaderDeps: ({ search }) => ({ from: search.from, to: search.to }),
  loader: ({ context, params, deps }) =>
    Promise.all([
      context.queryClient.ensureQueryData(modelQuery(params.modelId)),
      context.queryClient.ensureQueryData(versionQuery(deps.from)),
      context.queryClient.ensureQueryData(versionQuery(deps.to)),
      context.queryClient.ensureQueryData(diffQuery(params.modelId, deps.from, deps.to)),
    ]),
  component: ComparePage,
})

function ComparePage() {
  const { modelId } = Route.useParams()
  const search = Route.useSearch()
  const navigate = Route.useNavigate()
  const view = search.view ?? 'context'
  const { data: model } = useSuspenseQuery(modelQuery(modelId))
  const { data: from } = useSuspenseQuery(versionQuery(search.from))
  const { data: to } = useSuspenseQuery(versionQuery(search.to))
  const { data: diff } = useSuspenseQuery(diffQuery(modelId, search.from, search.to))
  const graph = useQuery({ ...compareQuery(modelId, search.from, search.to, view), placeholderData: keepPreviousData })
  const elements = useMemo(() => [...from.snapshot.elements, ...to.snapshot.elements], [from, to])
  return (
    <CompareWorkspace
      title={`${model.name}: compare v${from.number} → v${to.number}`}
      graph={graph}
      diff={diff}
      elements={elements}
      onViewChange={(key) => void navigate({ search: { ...search, view: key } })}
      actions={
        <Button size="sm" variant="ghost" asChild>
          <Link to="/m/$modelId" params={{ modelId }} search={{ view }}>
            <ArrowLeft /> Back to editor
          </Link>
        </Button>
      }
    />
  )
}
