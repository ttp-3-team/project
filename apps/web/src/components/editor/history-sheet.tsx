import { useQuery } from '@tanstack/react-query'
import { History } from 'lucide-react'
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
}

/** "History": the immutable versions of the model. */
export function HistorySheet(props: HistorySheetProps) {
  const [open, setOpen] = useState(false)
  const versions = useQuery({ ...versionsQuery(props.modelId), enabled: open })
  const list = versions.data ?? []
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
          <SheetDescription>Saved versions of the model, newest first.</SheetDescription>
        </SheetHeader>
        <div className="flex-1 overflow-y-auto px-4">
          {list.map((v) => (
            <button
              key={v.id}
              type="button"
              className={cn(
                'mb-2 w-full rounded-md border p-3 text-left text-sm transition-colors hover:border-primary/60',
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
      </SheetContent>
    </Sheet>
  )
}
