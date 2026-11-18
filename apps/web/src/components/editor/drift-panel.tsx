import { CircleAlert, CircleCheck } from 'lucide-react'
import type { DriftResult } from '#/model/types'

/** Findings of drift rule R1 between docker-compose and the current model. */
export function DriftPanel({ drift }: { readonly drift: DriftResult }) {
  return (
    <section
      className={
        drift.ok
          ? 'rounded-md border border-added/50 bg-added/10 p-3'
          : 'rounded-md border border-removed/50 bg-removed/10 p-3'
      }
    >
      <h2 className="flex items-center gap-2 font-medium">
        {drift.ok ? <CircleCheck className="size-4 text-added" /> : <CircleAlert className="size-4 text-removed" />}
        Deployment check (R1): {drift.ok ? 'no differences' : `differences: ${drift.messages.length}`}
      </h2>
      {!drift.ok && (
        <ul className="mt-2 list-disc pl-5 text-xs">
          {drift.messages.map((m) => (
            <li key={m}>{m}</li>
          ))}
        </ul>
      )}
      <p className="mt-2 text-xs text-muted-foreground">
        Compared with the current saved version. Accepting the draft adds the missing services as containers.
      </p>
    </section>
  )
}
