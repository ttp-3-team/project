import { zCreateWorkspaceRequest } from '@archlens/contracts/zod'
import { useForm } from '@tanstack/react-form'
import { useState } from 'react'
import { toast } from 'sonner'
import type { ReactNode } from 'react'
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
import { errorMessage } from '#/lib/core-result'

type NameDialogProps = {
  readonly trigger: ReactNode
  readonly title: string
  readonly description?: ReactNode
  readonly initial?: string
  readonly submitLabel: string
  readonly onSubmit: (name: string) => Promise<void>
}

/** A dialog that asks for one name (of a workspace or a model), validated by the schema generated from Core. */
export function NameDialog(props: NameDialogProps) {
  const [open, setOpen] = useState(false)
  const form = useForm({
    defaultValues: { name: props.initial ?? '' },
    validators: { onChange: zCreateWorkspaceRequest },
    onSubmit: async ({ value, formApi }) => {
      try {
        await props.onSubmit(value.name.trim())
        setOpen(false)
        formApi.reset()
      } catch (error) {
        toast.error(errorMessage(error))
      }
    },
  })
  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>{props.trigger}</DialogTrigger>
      <DialogContent>
        <form
          className="grid gap-4"
          onSubmit={(e) => {
            e.preventDefault()
            void form.handleSubmit()
          }}
        >
          <DialogHeader>
            <DialogTitle>{props.title}</DialogTitle>
            {props.description && <DialogDescription>{props.description}</DialogDescription>}
          </DialogHeader>
          <form.Field name="name">{(field) => <FormField field={field} label="Name" autoFocus />}</form.Field>
          <DialogFooter>
            <form.Subscribe selector={(s) => [s.canSubmit, s.isSubmitting] as const}>
              {([canSubmit, isSubmitting]) => (
                <Button type="submit" disabled={!canSubmit || isSubmitting}>
                  {isSubmitting ? 'Please wait…' : props.submitLabel}
                </Button>
              )}
            </form.Subscribe>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
