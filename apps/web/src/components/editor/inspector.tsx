import { zElement } from '@archlens/contracts/zod'
import { useForm } from '@tanstack/react-form'
import type { z } from 'zod'
import { ArrowRight, CircleAlert, FolderOpen, Plus, Trash2 } from 'lucide-react'
import { useId, useState } from 'react'
import { FormField } from '#/components/form-field'
import { Button } from '#/components/ui/button'
import { Checkbox } from '#/components/ui/checkbox'
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '#/components/ui/dialog'
import { Input } from '#/components/ui/input'
import { Label } from '#/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '#/components/ui/select'
import { Separator } from '#/components/ui/separator'
import { Textarea } from '#/components/ui/textarea'
import { KIND_LABEL } from '#/model/types'
import type { EditorAction } from '#/model/editor'
import type { Element, ElementKind, Relationship, Snapshot, ViewGraph, ViewKind } from '#/model/types'

/** Current selection on the canvas. */
export type Selection = { readonly kind: 'element' | 'edge'; readonly id: string } | null

type InspectorProps = {
  readonly snapshot: Snapshot
  readonly view: ViewGraph
  readonly selection: Selection
  readonly problems: ReadonlyArray<string>
  readonly dispatch: (action: EditorAction) => void
  readonly onOpen: (viewKey: string) => void
  readonly onSelect: (selection: Selection) => void
}

/** Side panel: edits the selected element or relationship, or adds an element to the current view. */
export function Inspector(props: InspectorProps) {
  const { snapshot, view, selection } = props
  const element = selection?.kind === 'element' ? snapshot.elements.find((e) => e.id === selection.id) : undefined
  const node = element && view.nodes.find((n) => n.elementId === element.id)
  const edge = selection?.kind === 'edge' ? view.edges.find((e) => e.id === selection.id) : undefined
  return (
    <aside className="flex w-80 shrink-0 flex-col gap-4 overflow-y-auto border-l p-4 text-sm">
      {props.problems.length > 0 && <Problems problems={props.problems} />}
      {element && (
        <ElementPanel key={element.id} {...props} element={element} childViewKey={node?.childViewKey ?? null} />
      )}
      {edge && (
        <EdgePanel
          key={edge.id}
          {...props}
          relationships={edge.relationshipIds.flatMap((id) => snapshot.relationships.find((r) => r.id === id) ?? [])}
          implied={edge.implied}
        />
      )}
      {!element && !edge && <AddElementPanel key={view.view.key} {...props} />}
    </aside>
  )
}

function Problems({ problems }: { readonly problems: ReadonlyArray<string> }) {
  return (
    <section className="rounded-md border border-destructive/50 bg-destructive/10 p-3 text-xs">
      <div className="mb-1 flex items-center gap-1.5 font-medium">
        <CircleAlert className="size-3.5 text-destructive" /> The model has problems ({problems.length})
      </div>
      <ul className="list-disc pl-4">
        {problems.slice(0, 5).map((p) => (
          <li key={p}>{p}</li>
        ))}
      </ul>
    </section>
  )
}

function ElementPanel(props: InspectorProps & { readonly element: Element; readonly childViewKey: string | null }) {
  const { element, dispatch } = props
  return (
    <>
      <div className="text-xs uppercase tracking-wide text-muted-foreground">{KIND_LABEL[element.kind]}</div>
      <Field
        label="Name"
        initial={element.name}
        onCommit={(name) => dispatch({ type: 'updateElement', id: element.id, fields: { name } })}
      />
      <Field
        label="Technology"
        initial={element.technology ?? ''}
        onCommit={(technology) => dispatch({ type: 'updateElement', id: element.id, fields: { technology } })}
      />
      <Field
        label="Description"
        multiline
        initial={element.description ?? ''}
        onCommit={(description) => dispatch({ type: 'updateElement', id: element.id, fields: { description } })}
      />
      <Field
        label="Tags (comma-separated)"
        initial={(element.tags ?? []).join(', ')}
        onCommit={(tags) => dispatch({ type: 'updateElement', id: element.id, fields: { tags: tags.split(',') } })}
      />
      {(element.kind === 'person' || element.kind === 'softwareSystem') && (
        <label className="flex items-center gap-2">
          <Checkbox
            checked={element.external ?? false}
            onCheckedChange={(v) =>
              dispatch({ type: 'updateElement', id: element.id, fields: { external: v === true } })
            }
          />
          External element
        </label>
      )}
      <Separator />
      <div className="flex flex-wrap gap-2">
        {props.childViewKey && (
          <Button size="sm" variant="secondary" onClick={() => props.childViewKey && props.onOpen(props.childViewKey)}>
            <FolderOpen /> Open level
          </Button>
        )}
        <Button
          size="sm"
          variant="destructive"
          onClick={() => {
            dispatch({ type: 'deleteElement', id: element.id })
            props.onSelect(null)
          }}
        >
          <Trash2 /> Delete
        </Button>
      </div>
      <p className="text-xs text-muted-foreground">
        To add a relationship, drag from an element's right handle to another element on the canvas.
      </p>
    </>
  )
}

function EdgePanel(
  props: InspectorProps & { readonly relationships: ReadonlyArray<Relationship>; readonly implied: boolean },
) {
  const { snapshot, dispatch } = props
  const name = (id: string) => snapshot.elements.find((e) => e.id === id)?.name ?? id
  const ids = props.relationships.map((r) => r.id)
  return (
    <>
      <div className="text-xs uppercase tracking-wide text-muted-foreground">
        {props.implied ? 'Implied relationship (derived from lower levels)' : 'Relationship'}
      </div>
      {props.relationships.map((r) => (
        <div key={r.id} className="flex flex-col gap-3 rounded-md border p-3">
          <div className="flex items-center gap-1 text-xs">
            {name(r.sourceId)} <ArrowRight className="size-3" /> {name(r.targetId)}
          </div>
          <Field
            label="Description"
            initial={r.description ?? ''}
            onCommit={(description) => dispatch({ type: 'updateRelationship', id: r.id, fields: { description } })}
          />
          <Field
            label="Technology"
            initial={r.technology ?? ''}
            onCommit={(technology) => dispatch({ type: 'updateRelationship', id: r.id, fields: { technology } })}
          />
          {!props.implied && (
            <Button
              size="sm"
              variant="destructive"
              onClick={() => {
                dispatch({ type: 'deleteRelationships', ids: [r.id] })
                if (props.relationships.length === 1) props.onSelect(null)
              }}
            >
              <Trash2 /> Delete relationship
            </Button>
          )}
        </div>
      ))}
      {props.implied && (
        <Dialog>
          <DialogTrigger asChild>
            <Button size="sm" variant="destructive">
              <Trash2 /> Delete relationships ({ids.length})
            </Button>
          </DialogTrigger>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Delete the lower-level relationships?</DialogTitle>
              <DialogDescription>
                This relationship is derived from {ids.length} relationships between nested elements. All of them will
                be deleted:
              </DialogDescription>
            </DialogHeader>
            <ul className="list-disc pl-5 text-sm">
              {props.relationships.map((r) => (
                <li key={r.id}>
                  {name(r.sourceId)} → {name(r.targetId)}
                  {r.description ? ` (${r.description})` : ''}
                </li>
              ))}
            </ul>
            <DialogFooter>
              <DialogClose asChild>
                <Button variant="outline">Cancel</Button>
              </DialogClose>
              <Button
                variant="destructive"
                onClick={() => {
                  dispatch({ type: 'deleteRelationships', ids })
                  props.onSelect(null)
                }}
              >
                Delete
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      )}
    </>
  )
}

const KINDS: Readonly<Record<ViewKind, ReadonlyArray<ElementKind>>> = {
  context: ['softwareSystem', 'person'],
  container: ['container'],
  component: ['component'],
}

const NewElementForm = zElement
  .pick({ kind: true, name: true, technology: true })
  .extend({ name: zElement.shape.name.trim().min(1) })

function AddElementPanel(props: InspectorProps) {
  const kinds = KINDS[props.view.view.kind]
  const defaultValues: z.input<typeof NewElementForm> = { kind: kinds[0] ?? 'softwareSystem', name: '', technology: '' }
  const form = useForm({
    defaultValues,
    validators: { onChange: NewElementForm },
    onSubmit: ({ value, formApi }) => {
      props.dispatch({
        type: 'addElement',
        id: crypto.randomUUID(),
        kind: value.kind,
        name: value.name,
        parentId: props.view.view.scopeId,
        technology: value.technology ?? '',
      })
      formApi.reset()
    },
  })
  return (
    <form
      className="flex flex-col gap-3"
      onSubmit={(e) => {
        e.preventDefault()
        void form.handleSubmit()
      }}
    >
      <div className="font-medium">Add element</div>
      <p className="text-xs text-muted-foreground">{props.view.view.title}</p>
      {kinds.length > 1 && (
        <form.Field name="kind">
          {(field) => (
            <div className="grid gap-1.5">
              <Label>Kind</Label>
              <Select
                value={field.state.value}
                onValueChange={(v) => field.handleChange(kinds.find((k) => k === v) ?? field.state.value)}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {kinds.map((k) => (
                    <SelectItem key={k} value={k}>
                      {KIND_LABEL[k]}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          )}
        </form.Field>
      )}
      <form.Field name="name">
        {(field) => <FormField field={field} label="Name" autoFocus={props.view.nodes.length === 0} />}
      </form.Field>
      <form.Field name="technology">{(field) => <FormField field={field} label="Technology" />}</form.Field>
      <form.Subscribe selector={(s) => [s.canSubmit, s.values.kind, s.values.name] as const}>
        {([canSubmit, kind, name]) => (
          <Button type="submit" disabled={!canSubmit || !name.trim()}>
            <Plus /> Add {KIND_LABEL[kind].toLowerCase()}
          </Button>
        )}
      </form.Subscribe>
      <Separator />
      <p className="text-xs text-muted-foreground">
        Click an element to edit it; double-click a system or container to open the level below.
      </p>
    </form>
  )
}

function Field(props: {
  readonly label: string
  readonly initial: string
  readonly multiline?: boolean
  readonly onCommit: (value: string) => void
}) {
  const [value, setValue] = useState(props.initial)
  const id = useId()
  const commit = () => {
    if (value !== props.initial) props.onCommit(value)
  }
  return (
    <div className="grid gap-1.5">
      <Label htmlFor={id}>{props.label}</Label>
      {props.multiline ? (
        <Textarea id={id} value={value} rows={3} onChange={(e) => setValue(e.target.value)} onBlur={commit} />
      ) : (
        <Input
          id={id}
          value={value}
          onChange={(e) => setValue(e.target.value)}
          onBlur={commit}
          onKeyDown={(e) => {
            if (e.key === 'Enter') commit()
          }}
        />
      )}
    </div>
  )
}
