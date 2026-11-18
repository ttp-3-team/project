import { zComposeDraftRequest } from '@archlens/contracts/zod'
import { useForm } from '@tanstack/react-form'
import { FileCode2 } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import type { ComposeDraftResult } from '@archlens/contracts/types'
import type { Snapshot } from '#/model/types'
import { FormField } from '#/components/form-field'
import { Button } from '#/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '#/components/ui/dialog'
import { Label } from '#/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '#/components/ui/select'
import { errorMessage, unwrap } from '#/lib/core-result'
import { composeDraft } from '#/server/core-api'

/** A draft proposed by the compose analysis, with the system it targets. */
export type ComposeDraft = ComposeDraftResult & { readonly targetSystemId: string }

type ComposeImportDialogProps = {
  readonly modelId: string
  readonly baseVersionId: string | null
  readonly snapshot: Snapshot
  readonly preferredSystemId: string | null
  readonly blockedReason: string | null
  readonly onDraft: (draft: ComposeDraft) => void
}

const ComposeForm = zComposeDraftRequest.pick({ composeYaml: true, targetSystemId: true })

/** "Import from docker-compose": sends a compose file to Core, which proposes a draft and checks drift. */
export function ComposeImportDialog(props: ComposeImportDialogProps) {
  const systems = props.snapshot.elements.filter((e) => e.kind === 'softwareSystem' && !e.external)
  const children = (id: string) => props.snapshot.elements.filter((e) => e.parentId === id).length
  const fallback = [...systems].sort((a, b) => children(b.id) - children(a.id))[0]?.id ?? ''
  const initialSystem = systems.find((s) => s.id === props.preferredSystemId)?.id ?? fallback
  const [open, setOpen] = useState(false)
  const form = useForm({
    defaultValues: { composeYaml: '', targetSystemId: initialSystem },
    validators: { onChange: ComposeForm },
    onSubmit: async ({ value }) => {
      if (!props.baseVersionId) return
      try {
        const result = unwrap(
          await composeDraft({
            data: { modelId: props.modelId, body: { ...value, baseVersionId: props.baseVersionId } },
          }),
        )
        setOpen(false)
        props.onDraft({ ...result, targetSystemId: value.targetSystemId })
      } catch (error) {
        toast.error(errorMessage(error))
      }
    },
  })
  return (
    <Dialog
      open={open}
      onOpenChange={(o) => {
        setOpen(o)
        if (o) form.setFieldValue('targetSystemId', initialSystem)
      }}
    >
      <DialogTrigger asChild>
        <Button size="sm" variant="ghost" disabled={props.blockedReason !== null}>
          <FileCode2 /> Import from docker-compose
          {props.blockedReason && <span className="text-xs text-muted-foreground">({props.blockedReason})</span>}
        </Button>
      </DialogTrigger>
      <DialogContent className="sm:max-w-2xl">
        <form
          className="grid gap-4"
          onSubmit={(e) => {
            e.preventDefault()
            void form.handleSubmit()
          }}
        >
          <DialogHeader>
            <DialogTitle>Draft from docker-compose</DialogTitle>
            <DialogDescription>
              ArchLens matches the services to the system's containers, proposes a draft (new containers and
              relationships from depends_on) and checks the drift between compose and the model. Nothing is saved until
              you accept the draft.
            </DialogDescription>
          </DialogHeader>
          <form.Field name="targetSystemId">
            {(field) => (
              <div className="grid gap-2">
                <Label>Target system</Label>
                <Select value={field.state.value} onValueChange={field.handleChange}>
                  <SelectTrigger className="w-72">
                    <SelectValue placeholder="Pick a system" />
                  </SelectTrigger>
                  <SelectContent>
                    {systems.map((s) => (
                      <SelectItem key={s.id} value={s.id}>
                        {s.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
          </form.Field>
          <form.Field name="composeYaml">
            {(field) => (
              <div className="grid gap-2">
                <input
                  type="file"
                  accept=".yml,.yaml,text/yaml"
                  aria-label="Upload a docker-compose file"
                  className="justify-self-end text-xs file:mr-2 file:rounded file:border-0 file:bg-secondary file:px-2 file:py-1 file:text-secondary-foreground"
                  onChange={async (e) => {
                    const file = e.target.files?.[0]
                    if (!file) return
                    try {
                      field.handleChange(await file.text())
                    } catch {
                      toast.error(`Could not read ${file.name}`)
                    }
                  }}
                />
                <FormField
                  field={field}
                  label="docker-compose.yml"
                  multiline
                  className="h-64 font-mono text-xs"
                  placeholder={'services:\n  api:\n    image: shopflow/api:1.0\n    depends_on: [db]'}
                />
              </div>
            )}
          </form.Field>
          <DialogFooter>
            <form.Subscribe selector={(s) => [s.canSubmit, s.isSubmitting, s.values.composeYaml] as const}>
              {([canSubmit, isSubmitting, yaml]) => (
                <Button type="submit" disabled={!canSubmit || isSubmitting || !yaml.trim()}>
                  {isSubmitting ? 'Analyzing…' : 'Analyze'}
                </Button>
              )}
            </form.Subscribe>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
