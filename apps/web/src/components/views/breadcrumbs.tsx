import { ChevronRight } from 'lucide-react'
import type { GraphView } from '#/model/types'

/** C4 level trail from Core's view ancestors: Context › system › container. */
export function Breadcrumbs(props: { readonly view: GraphView; readonly onOpen: (viewKey: string) => void }) {
  const crumbs = [...props.view.ancestors, props.view]
  return (
    <nav aria-label="C4 levels" className="flex items-center gap-1 text-muted-foreground">
      {crumbs.map((crumb, i) => (
        <span key={crumb.key} className="flex items-center gap-1">
          {i > 0 && <ChevronRight className="size-3.5" />}
          {i === crumbs.length - 1 ? (
            <span className="text-foreground">{crumb.label}</span>
          ) : (
            <button type="button" className="hover:text-foreground" onClick={() => props.onOpen(crumb.key)}>
              {crumb.label}
            </button>
          )}
        </span>
      ))}
    </nav>
  )
}
