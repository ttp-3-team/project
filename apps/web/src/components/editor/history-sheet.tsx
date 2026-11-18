import { useQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { GitCompare, History } from 'lucide-react'
import { useState } from 'react'
import { Badge } from '#/components/ui/badge'
import { Button } from '#/components/ui/button'
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle, SheetTrigger } from '#/components/ui/sheet'
import { errorMessage } from '#/lib/core-result'
import { versionsQuery } from '#/lib/model-queries'
import { cn } from '#/lib/utils'

type HistorySheetProps = {
  readonly modelId: string
  readonly currentVersionId: string | null
  readonly viewKey: string
  readonly dirty: boolean
}

/** "History": the immutable versions of the model; pick two to compare them on the canvas. */
export function HistorySheet(props: HistorySheetProps) {
  const [open, setOpen] = useState(false)
  const versions = useQuery({ ...versionsQuery(props.modelId), enabled: open })
  const [picked, setPicked] = useState<ReadonlyArray<string>>([])
  const list = versions.data ?? []
  const toggle = (id: string) => setPicked((p) => (p.includes(id) ? p.filter((x) => x !== id) : [...p, id].slice(-2)))
  const number = (id: string) => list.find((v) => v.id === id)?.number ?? 0
  const [from, to] = [...picked].sort((a, b) => number(a) - number(b))
  return (
    <Sheet open={open} onOpenChange={setOpen}>
      <SheetTrigger asChild>
        <Button size="sm" variant="ghost">
          <History /> History
        </Button>
      </SheetTrigger>
      <SheetContent className="flex w-[420px] flex-col gap-0 sm:max-w-[420px]">
        <SheetHeader>
          <SheetTitle>Version history</SheetTitle>
          <SheetDescription>Pick two versions to compare them on the canvas.</SheetDescription>
        </SheetHeader>
        <div className="flex-1 overflow-y-auto px-4">
          {list.map((v) => (
            <button
              key={v.id}
              type="button"
              onClick={() => toggle(v.id)}
              className={cn(
                'mb-2 w-full rounded-md border p-3 text-left text-sm transition-colors hover:border-primary/60',
                picked.includes(v.id) && 'border-primary bg-primary/10',
              )}
            >
              <div className="flex items-center gap-2">
                <span className="font-semibold">v{v.number}</span>
                {v.id === props.currentVersionId && <Badge variant="secondary">current</Badge>}
                <span className="ml-auto text-xs text-muted-foreground">
                  {new Date(v.createdAt).toLocaleString('en-GB')}
                </span>
              </div>
              <div className="mt-1 text-muted-foreground">{v.message || '—'}</div>
            </button>
          ))}
          {versions.isLoading && <p className="text-sm text-muted-foreground">Loading…</p>}
          {versions.isError && (
            <div className="text-sm">
              <p className="text-destructive">Could not load the history: {errorMessage(versions.error)}</p>
              <Button size="sm" variant="outline" className="mt-2" onClick={() => void versions.refetch()}>
                Try again
              </Button>
            </div>
          )}
          {versions.isSuccess && list.length === 0 && (
            <p className="text-sm text-muted-foreground">No versions yet. Save the model to create the first one.</p>
          )}
          {versions.isSuccess && list.length === 1 && (
            <p className="text-sm text-muted-foreground">
              There is only one version. Save another one to have something to compare.
            </p>
          )}
        </div>
        <div className="border-t p-4">
          {props.dirty && <p className="mb-2 text-xs text-modified">Unsaved changes are not part of the comparison.</p>}
          {from && to ? (
            <Button asChild className="w-full">
              <Link
                to="/m/$modelId/compare"
                params={{ modelId: props.modelId }}
                search={{ from, to, view: props.viewKey }}
                onClick={() => setOpen(false)}
              >
                <GitCompare /> Compare v{number(from)} → v{number(to)}
              </Link>
            </Button>
          ) : (
            <Button className="w-full" disabled>
              <GitCompare /> Pick two versions
            </Button>
          )}
        </div>
      </SheetContent>
    </Sheet>
  )
}
