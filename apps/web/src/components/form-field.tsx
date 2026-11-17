import { useId } from 'react'
import type { AnyFieldApi } from '@tanstack/react-form'
import { Input } from '#/components/ui/input'
import { Label } from '#/components/ui/label'
import { Textarea } from '#/components/ui/textarea'

type FormFieldProps = {
  readonly field: AnyFieldApi
  readonly label: string
  readonly multiline?: boolean
  readonly className?: string
  readonly placeholder?: string
  readonly autoFocus?: boolean
}

function message(error: unknown): string {
  if (typeof error === 'string') return error
  if (typeof error === 'object' && error !== null && 'message' in error && typeof error.message === 'string')
    return error.message
  return 'Invalid value'
}

/** A labelled text input bound to a TanStack Form field, showing the field's validation errors once touched. */
export function FormField(props: FormFieldProps) {
  const id = useId()
  const { field } = props
  const errors = field.state.meta.isTouched ? field.state.meta.errors : []
  const value = typeof field.state.value === 'string' ? field.state.value : ''
  const common = {
    id,
    name: field.name,
    value,
    placeholder: props.placeholder,
    autoFocus: props.autoFocus,
    'aria-invalid': errors.length > 0,
    onBlur: field.handleBlur,
  }
  return (
    <div className="grid gap-1.5">
      <Label htmlFor={id}>{props.label}</Label>
      {props.multiline ? (
        <Textarea {...common} className={props.className} onChange={(e) => field.handleChange(e.target.value)} />
      ) : (
        <Input {...common} className={props.className} onChange={(e) => field.handleChange(e.target.value)} />
      )}
      {errors.length > 0 && <p className="text-xs text-destructive">{errors.map(message).join('; ')}</p>}
    </div>
  )
}
