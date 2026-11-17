import { getRequestHeaders } from '@tanstack/react-start/server'
import { auth } from './auth.server'

/** The signed-in user as the UI sees it. */
export type SessionUser = {
  readonly id: string
  readonly name: string
  readonly image: string | null
}

/** Resolve the current user from the request cookies (server side only). */
export async function currentUser(): Promise<SessionUser | null> {
  const session = await (await auth()).api.getSession({ headers: getRequestHeaders() })
  if (!session) return null
  return { id: session.user.id, name: session.user.name, image: session.user.image ?? null }
}
