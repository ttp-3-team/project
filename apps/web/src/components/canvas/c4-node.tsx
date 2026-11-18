import { Handle, Position } from '@xyflow/react'
import { memo } from 'react'
import type { Node, NodeProps } from '@xyflow/react'
import { cn } from '#/lib/utils'
import { NODE_HEIGHT, NODE_WIDTH } from '#/model/node-size'
import { KIND_LABEL, fieldLabel } from '#/model/types'
import type { CompareNode } from '#/model/types'

/** React Flow node type for C4 elements. */
export type C4FlowNode = Node<CompareNode, 'c4'>

const KIND_STYLE = {
  person: 'bg-[#08427b] text-white',
  softwareSystem: 'bg-[#1168bd] text-white',
  container: 'bg-[#438dd5] text-white',
  component: 'bg-[#85bbf0] text-[#0b2540]',
} as const

const STATUS_STYLE = {
  added: 'ring-4 ring-added',
  removed: 'opacity-45 outline-dashed outline-[3px] outline-removed outline-offset-2',
  modified: 'ring-4 ring-modified',
  unchanged: '',
} as const

const BADGE = {
  added: { text: '+', className: 'bg-added text-black', label: 'Added' },
  removed: { text: '−', className: 'bg-removed text-white', label: 'Removed' },
  modified: { text: '~', className: 'bg-modified text-black', label: 'Modified' },
} as const

const MAX_CHANGE_LINES = 2

function C4NodeView({ data, selected }: NodeProps<C4FlowNode>) {
  const technology = data.technology ? `: ${data.technology}` : ''
  const changes = data.status === 'modified' ? data.changes : []
  return (
    <div
      style={{ width: NODE_WIDTH, height: NODE_HEIGHT }}
      className={cn(
        'relative flex flex-col items-center justify-center gap-1 overflow-hidden rounded-lg px-3 py-2 text-center shadow-md',
        data.external || data.outside ? 'bg-[#6b6b6b] text-white' : KIND_STYLE[data.kind],
        data.kind === 'person' && 'rounded-[28px]',
        selected && 'outline outline-2 outline-offset-2 outline-primary',
        STATUS_STYLE[data.status],
      )}
    >
      <Handle type="target" position={Position.Left} className="!size-2.5 !bg-white/70" />
      {data.status !== 'unchanged' && (
        <span
          title={BADGE[data.status].label}
          aria-label={BADGE[data.status].label}
          className={cn(
            'absolute top-1 right-1 grid size-5 place-items-center rounded-full text-sm font-bold leading-none',
            BADGE[data.status].className,
          )}
        >
          {BADGE[data.status].text}
        </span>
      )}
      <div className="line-clamp-1 text-sm font-semibold leading-tight">{data.name}</div>
      <div className="line-clamp-1 text-[11px] opacity-80">
        [{KIND_LABEL[data.kind]}
        {technology}]
      </div>
      {changes.length > 0 ? (
        <div className="w-full rounded bg-black/35 px-1.5 py-1 text-left text-[10px] leading-snug text-amber-200">
          {changes.slice(0, MAX_CHANGE_LINES).map((c) => (
            <div key={c.field} className="line-clamp-1">
              was: {fieldLabel(c.field)}: {c.from || '—'}
            </div>
          ))}
          {changes.length > MAX_CHANGE_LINES && <div>… {changes.length - MAX_CHANGE_LINES} more</div>}
        </div>
      ) : (
        data.description && <div className="line-clamp-2 text-[11px] leading-snug opacity-90">{data.description}</div>
      )}
      {data.childViewKey && data.hasChildren && data.status === 'unchanged' && (
        <div className="text-[10px] opacity-70">⤢ double-click to open</div>
      )}
      <Handle type="source" position={Position.Right} className="!size-2.5 !bg-white/70" />
    </div>
  )
}

/** C4 element node. */
export const C4Node = memo(C4NodeView)
