import { Link, createFileRoute } from '@tanstack/react-router'
import { queryOptions, useMutation, useQuery, useQueryClient, useSuspenseQuery } from '@tanstack/react-query'
import { ChevronLeft, FilePlus2, FileUp, LayoutTemplate, LoaderCircle, Network } from 'lucide-react'
import { useRef } from 'react'
import { toast } from 'sonner'
import { NameDialog } from '#/components/name-dialog'
import { Button } from '#/components/ui/button'
import { Card, CardDescription, CardHeader, CardTitle } from '#/components/ui/card'
import { errorMessage, unwrap } from '#/lib/core-result'
import { emptySnapshot, snapshotFromFile } from '#/model/types'
import { createModel, getShopFlowTemplate, getWorkspace, listModels } from '#/server/core-api'
import type { Snapshot } from '#/model/types'

const workspaceQuery = (workspaceId: string) =>
  queryOptions({
    queryKey: ['workspace', workspaceId],
    queryFn: async () => unwrap(await getWorkspace({ data: { workspaceId } })),
  })

const modelsQuery = (workspaceId: string) =>
  queryOptions({
    queryKey: ['workspace', workspaceId, 'models'],
    queryFn: async () => unwrap(await listModels({ data: { workspaceId } })),
  })

const templateQuery = queryOptions({
  queryKey: ['template', 'shopflow'],
  staleTime: Infinity,
  queryFn: async () => unwrap(await getShopFlowTemplate()),
})

export const Route = createFileRoute('/_app/w/$workspaceId')({
  loader: ({ context, params }) =>
    Promise.all([
      context.queryClient.ensureQueryData(workspaceQuery(params.workspaceId)),
      context.queryClient.ensureQueryData(modelsQuery(params.workspaceId)),
    ]),
  component: WorkspacePage,
})

function templateSummary(template: Snapshot): string {
  const count = (kind: string) => template.elements.filter((e) => e.kind === kind).length
  return `${count('container')} containers, ${count('component')} components, ${template.relationships.length} relationships`
}

function WorkspacePage() {
  const { workspaceId } = Route.useParams()
  const { data: workspace } = useSuspenseQuery(workspaceQuery(workspaceId))
  const { data: models } = useSuspenseQuery(modelsQuery(workspaceId))
  const template = useQuery(templateQuery)
  const queryClient = useQueryClient()
  const navigate = Route.useNavigate()
  const fileInput = useRef<HTMLInputElement>(null)

  const create = useMutation({
    mutationFn: async (input: { name: string; snapshot: Snapshot | null; message: string }) =>
      unwrap(
        await createModel({
          data: { workspaceId, body: { name: input.name, message: input.message, snapshot: input.snapshot } },
        }),
      ),
    onSuccess: async (model) => {
      await queryClient.invalidateQueries({ queryKey: modelsQuery(workspaceId).queryKey })
      await navigate({ to: '/m/$modelId', params: { modelId: model.id } })
    },
  })
  const importFile = useMutation({
    mutationFn: async (file: File) => {
      let json: unknown
      try {
        json = JSON.parse(await file.text())
      } catch {
        throw new Error(`${file.name}: not a JSON file`)
      }
      const snapshot = snapshotFromFile(json)
      if (!snapshot) throw new Error(`${file.name}: not an ArchLens model`)
      await create.mutateAsync({
        name: file.name.replace(/\.json$/i, '').replace(/^archlens-/, ''),
        snapshot,
        message: `Imported from ${file.name}`,
      })
    },
    onError: (error) => toast.error(errorMessage(error)),
  })

  return (
    <main className="mx-auto max-w-4xl p-8">
      <Link to="/" className="flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
        <ChevronLeft className="size-4" /> Workspaces
      </Link>
      <div className="mt-2 flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold tracking-tight">{workspace.name}</h1>
        <div className="flex flex-wrap gap-2">
          <NameDialog
            title="Model from the ShopFlow template"
            description={
              template.data
                ? `Reference online shop model: ${templateSummary(template.data)}.`
                : template.isError
                  ? 'The template is unavailable right now.'
                  : 'Loading the template…'
            }
            initial="ShopFlow"
            submitLabel="Create"
            trigger={
              <Button>
                <LayoutTemplate /> From ShopFlow template
              </Button>
            }
            onSubmit={async (name) => {
              const snapshot = await queryClient.ensureQueryData(templateQuery)
              await create.mutateAsync({ name, snapshot, message: 'Created from the ShopFlow template' })
            }}
          />
          <NameDialog
            title="Empty model"
            submitLabel="Create"
            trigger={
              <Button variant="outline">
                <FilePlus2 /> Empty model
              </Button>
            }
            onSubmit={async (name) => {
              await create.mutateAsync({ name, snapshot: emptySnapshot, message: 'Empty model' })
            }}
          />
          <Button variant="outline" disabled={importFile.isPending} onClick={() => fileInput.current?.click()}>
            {importFile.isPending ? <LoaderCircle className="animate-spin" /> : <FileUp />} Import model
          </Button>
          <input
            ref={fileInput}
            type="file"
            accept="application/json,.json"
            className="hidden"
            onChange={(e) => {
              const file = e.target.files?.[0]
              e.target.value = ''
              if (file) importFile.mutate(file)
            }}
          />
        </div>
      </div>
      <div className="mt-6 grid gap-3 sm:grid-cols-2">
        {models.map((m) => (
          <Link key={m.id} to="/m/$modelId" params={{ modelId: m.id }}>
            <Card className="transition-colors hover:border-primary/60">
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <Network className="size-4 text-primary" /> {m.name}
                </CardTitle>
                <CardDescription>
                  {m.latestVersionNumber ? `Latest version: v${m.latestVersionNumber}` : 'No versions'}
                </CardDescription>
              </CardHeader>
            </Card>
          </Link>
        ))}
        {models.length === 0 && (
          <p className="text-sm text-muted-foreground">
            No models yet. Start from the ShopFlow template or an empty model.
          </p>
        )}
      </div>
    </main>
  )
}
