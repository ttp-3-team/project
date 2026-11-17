import { Link, createFileRoute } from '@tanstack/react-router'
import { queryOptions, useQueryClient, useSuspenseQuery } from '@tanstack/react-query'
import { FolderPlus, Folders } from 'lucide-react'
import { NameDialog } from '#/components/name-dialog'
import { Button } from '#/components/ui/button'
import { Card, CardDescription, CardHeader, CardTitle } from '#/components/ui/card'
import { unwrap } from '#/lib/core-result'
import { createWorkspace, listWorkspaces } from '#/server/core-api'

const workspacesQuery = queryOptions({
  queryKey: ['workspaces'],
  queryFn: async () => unwrap(await listWorkspaces()),
})

export const Route = createFileRoute('/_app/')({
  loader: ({ context }) => context.queryClient.ensureQueryData(workspacesQuery),
  component: WorkspacesPage,
})

function WorkspacesPage() {
  const { data: workspaces } = useSuspenseQuery(workspacesQuery)
  const queryClient = useQueryClient()
  const navigate = Route.useNavigate()

  return (
    <main className="mx-auto max-w-4xl p-8">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Workspaces</h1>
          <p className="text-sm text-muted-foreground">A team's workspace holds its architecture models.</p>
        </div>
        <NameDialog
          title="New workspace"
          submitLabel="Create"
          trigger={
            <Button>
              <FolderPlus /> Create workspace
            </Button>
          }
          onSubmit={async (name) => {
            const workspace = unwrap(await createWorkspace({ data: { name } }))
            await queryClient.invalidateQueries({ queryKey: workspacesQuery.queryKey })
            await navigate({ to: '/w/$workspaceId', params: { workspaceId: workspace.id } })
          }}
        />
      </div>
      <div className="mt-6 grid gap-3 sm:grid-cols-2">
        {workspaces.map((w) => (
          <Link key={w.id} to="/w/$workspaceId" params={{ workspaceId: w.id }}>
            <Card className="transition-colors hover:border-primary/60">
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <Folders className="size-4 text-primary" /> {w.name}
                </CardTitle>
                <CardDescription>Created {new Date(w.createdAt).toLocaleDateString('en-GB')}</CardDescription>
              </CardHeader>
            </Card>
          </Link>
        ))}
        {workspaces.length === 0 && (
          <p className="text-sm text-muted-foreground">No workspaces yet. Create the first one.</p>
        )}
      </div>
    </main>
  )
}
