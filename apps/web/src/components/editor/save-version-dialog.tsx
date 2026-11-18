import { zCreateVersionRequest } from '@archlens/contracts/zod'
import { useForm } from '@tanstack/react-form'
import { useQueryClient } from '@tanstack/react-query'
import { Save } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { z } from 'zod'
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
import { CoreRequestFailed, errorMessage, unwrap } from '#/lib/core-result'
import { createVersion } from '#/server/core-api'

type SaveVersionDialogProps = {
  readonly modelId: string
  readonly parentVersionId: string | null
  readonly snapshot: Snapshot
  readonly disabled?: boolean
  readonly defaultMessage?: string
  readonly label?: string
}

const MessageForm = z.object({ message: zCreateVersionRequest.shape.message.trim().min(1) })

/** "Save version": saves the snapshot as a new immutable version on top of `parentVersionId`. */
export function SaveVersionDialog(props: SaveVersionDialogProps) {
  const [open, setOpen] = useState(false)
  const queryClient = useQueryClient()
  const form = useForm({
    defaultValues: { message: props.defaultMessage ?? '' },
    validators: { onChange: MessageForm },
    onSubmit: async ({ value, formApi }) => {
      try {
        const version = unwrap(
          await createVersion({
            data: {
              modelId: props.modelId,
              body: { snapshot: props.snapshot, message: value.message.trim(), parentVersionId: props.parentVersionId },
            },
          }),
        )
        setOpen(false)
        formApi.reset()
        toast.success(`Saved version v${version.number}`)
        await queryClient.invalidateQueries({ queryKey: ['model', props.modelId] })
      } catch (error) {
        if (error instanceof CoreRequestFailed && error.error.status === 409)
          toast.error('The model has changed: a newer version was saved. Reload the page and redo your changes.')
        else toast.error(errorMessage(error))
      }
    },
  })
  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" disabled={props.disabled}>
          <Save /> {props.label ?? 'Save version'}
        </Button>
      </DialogTrigger>
      <DialogContent>
        <form
          className="grid gap-4"
          onSubmit={(e) => {
            e.preventDefault()
            void form.handleSubmit()
          }}
        >
          <DialogHeader>
            <DialogTitle>{props.label ?? 'Save version'}</DialogTitle>
            <DialogDescription>
              A version is immutable: it cannot be changed or deleted, only compared with others.
            </DialogDescription>
          </DialogHeader>
          <form.Field name="message">
            {(field) => <FormField field={field} label="Change description" multiline autoFocus />}
          </form.Field>
          <DialogFooter>
            <form.Subscribe selector={(s) => [s.canSubmit, s.isSubmitting] as const}>
              {([canSubmit, isSubmitting]) => (
                <Button type="submit" disabled={!canSubmit || isSubmitting}>
                  {isSubmitting ? 'Saving…' : 'Save'}
                </Button>
              )}
            </form.Subscribe>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
