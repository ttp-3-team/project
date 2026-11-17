import { createFileRoute, redirect, useRouter } from '@tanstack/react-router'
import { useState } from 'react'
import { Github, TerminalSquare } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '#/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '#/components/ui/card'
import { authClient } from '#/lib/auth-client'
import { devSignIn, getLoginOptions, getSessionUser } from '#/server/session'

export const Route = createFileRoute('/login')({
  beforeLoad: async () => {
    if (await getSessionUser()) throw redirect({ to: '/' })
  },
  loader: () => getLoginOptions(),
  component: LoginPage,
})

function LoginPage() {
  const options = Route.useLoaderData()
  const router = useRouter()
  const [busy, setBusy] = useState(false)

  return (
    <main className="grid min-h-screen place-items-center p-6">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle className="text-2xl">ArchLens</CardTitle>
          <CardDescription>Sign in to work with your team's architecture models.</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          {options.github && (
            <Button
              disabled={busy}
              onClick={async () => {
                setBusy(true)
                const result = await authClient.signIn.social({ provider: 'github', callbackURL: '/' })
                if (result.error) {
                  toast.error('Could not start GitHub sign-in.')
                  setBusy(false)
                }
              }}
            >
              <Github /> Sign in with GitHub
            </Button>
          )}
          {options.devLogin && (
            <Button
              variant={options.github ? 'outline' : 'default'}
              disabled={busy}
              onClick={async () => {
                setBusy(true)
                try {
                  const result = await devSignIn()
                  if (result.ok) {
                    await router.navigate({ to: '/' })
                    return
                  }
                  toast.error(result.message)
                } catch {
                  toast.error('Could not sign in. Try again.')
                }
                setBusy(false)
              }}
            >
              <TerminalSquare /> Sign in as developer
            </Button>
          )}
          {!options.github && (
            <p className="text-xs text-muted-foreground">
              GitHub sign-in is disabled: set GITHUB_CLIENT_ID / GITHUB_CLIENT_SECRET.
            </p>
          )}
          {!options.github && !options.devLogin && (
            <p className="text-xs text-destructive">No sign-in method is configured. See the README.</p>
          )}
        </CardContent>
      </Card>
    </main>
  )
}
