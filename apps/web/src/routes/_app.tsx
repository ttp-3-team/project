import { useQueryClient } from '@tanstack/react-query'
import { Link, Outlet, createFileRoute, redirect, useRouter } from '@tanstack/react-router'
import { LogOut, ScanSearch } from 'lucide-react'
import { Button } from '#/components/ui/button'
import { getSessionUser, signOut } from '#/server/session'

export const Route = createFileRoute('/_app')({
  beforeLoad: async () => {
    const user = await getSessionUser()
    if (!user) throw redirect({ to: '/login' })
    return { user }
  },
  component: AppShell,
})

function AppShell() {
  const { user } = Route.useRouteContext()
  const router = useRouter()
  const queryClient = useQueryClient()
  return (
    <div className="flex h-screen flex-col">
      <header className="flex h-12 shrink-0 items-center gap-4 border-b px-4">
        <Link to="/" className="flex items-center gap-2 font-semibold tracking-tight">
          <ScanSearch className="size-5 text-primary" /> ArchLens
        </Link>
        <div className="ml-auto flex items-center gap-3 text-sm text-muted-foreground">
          {user.image && <img src={user.image} alt="" className="size-6 rounded-full" />}
          <span>{user.name}</span>
          <Button
            variant="ghost"
            size="sm"
            onClick={async () => {
              await signOut()
              queryClient.clear()
              await router.navigate({ to: '/login' })
            }}
          >
            <LogOut /> Sign out
          </Button>
        </div>
      </header>
      <div className="min-h-0 flex-1">
        <Outlet />
      </div>
    </div>
  )
}
