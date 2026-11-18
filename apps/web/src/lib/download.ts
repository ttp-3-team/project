const REVOKE_AFTER_MS = 10_000

/** Offer a JSON document as a file download in the browser. */
export function downloadJson(fileName: string, value: unknown): void {
  const url = URL.createObjectURL(new Blob([`${JSON.stringify(value, null, 2)}\n`], { type: 'application/json' }))
  const a = document.createElement('a')
  a.href = url
  a.download = fileName
  a.click()
  setTimeout(() => URL.revokeObjectURL(url), REVOKE_AFTER_MS)
}

/** A file-name-safe version of a display name. */
export function fileNameOf(name: string): string {
  return (
    name
      .trim()
      .toLowerCase()
      .replace(/[^\p{L}\p{N}]+/gu, '-')
      .replace(/^-|-$/g, '') || 'model'
  )
}
