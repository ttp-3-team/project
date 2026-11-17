import { Link } from '@tanstack/react-router'
import { CircleAlert, LoaderCircle, SearchX } from 'lucide-react'
import type { ErrorComponentProps } from '@tanstack/react-router'
import type { ReactNode } from 'react'
import { Button } from '#/components/ui/button'
import { errorMessage } from '#/lib/core-result'

/** Centered message with an icon and an optional action. */
export function StateBlock(props: {
  readonly icon: ReactNode
  readonly title: string
  readonly children?: ReactNode
  readonly action?: ReactNode
}) {
  return (
    <div className="grid h-full min-h-60 place-items-center p-8 text-center">
      <div className="flex max-w-md flex-col items-center gap-3">
        <div className="text-muted-foreground">{props.icon}</div>
        <h2 className="text-lg font-medium">{props.title}</h2>
        {props.children && <div className="text-sm text-muted-foreground">{props.children}</div>}
        {props.action}
      </div>
    </div>
  )
}

/** Spinner with a message; the router shows it while a page loads. */
export function LoadingState({ title = 'Loading…' }: { readonly title?: string }) {
  return <StateBlock icon={<LoaderCircle className="size-6 animate-spin" />} title={title} />
}

/** Shown when a route or resource does not exist (or belongs to someone else). */
export function NotFoundState() {
  return (
    <StateBlock
      icon={<SearchX className="size-6" />}
      title="Not found"
      action={
        <Button asChild variant="outline">
          <Link to="/">Back to workspaces</Link>
        </Button>
      }
    >
      This page does not exist, or it belongs to another user.
    </StateBlock>
  )
}

/** Shown when a page fails to load. */
export function ErrorState({ error, reset }: ErrorComponentProps) {
  return (
    <StateBlock
      icon={<CircleAlert className="size-6 text-destructive" />}
      title="Something went wrong"
      action={
        <Button variant="outline" onClick={reset}>
          Try again
        </Button>
      }
    >
      {errorMessage(error)}
    </StateBlock>
  )
}
