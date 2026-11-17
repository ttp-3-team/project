import { redirect } from '@tanstack/react-router'
import type { CoreError, CoreResult } from '#/server/core-client.server'

/** A failed BFF call surfaced to TanStack Query (which expects rejections). */
export class CoreRequestFailed extends Error {
  readonly _tag = 'CoreRequestFailed' as const

  constructor(readonly error: CoreError) {
    super(
      `${error.title}: ${error.problems.length > 0 ? error.problems.slice(0, 3).join('; ') : error.detail}`.replace(
        /: $/,
        '',
      ),
    )
  }
}

/**
 * Unwrap a BFF result inside a loader, query or mutation. This is the boundary where the typed error becomes a
 * rejection, because the router and TanStack Query model failures as rejected promises. A missing or expired
 * session (401) redirects to the sign-in page.
 *
 * @template T - The value type.
 * @throws A redirect to /login on 401, or CoreRequestFailed for any other error.
 */
export function unwrap<T>(result: CoreResult<T>): T {
  if (result.ok) return result.value
  if (result.error.status === 401) throw redirect({ to: '/login' })
  throw new CoreRequestFailed(result.error)
}

/** A message for a failed call (Core's message lists model problems when it reported them). */
export function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : 'Unknown error'
}
