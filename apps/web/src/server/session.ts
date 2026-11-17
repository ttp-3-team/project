import { createServerFn } from '@tanstack/react-start'
import { getRequestHeaders } from '@tanstack/react-start/server'
import { auth } from './auth.server'
import { serverConfig } from './config.server'
import { currentUser } from './session.server'

/** Server function: the current user or null. */
export const getSessionUser = createServerFn({ method: 'GET' }).handler(() => currentUser())

/** Server function: which login methods are available. */
export const getLoginOptions = createServerFn({ method: 'GET' }).handler(() => {
  const config = serverConfig()
  return { github: config.github !== null, devLogin: config.devLogin }
})

const DEV_USER = { email: 'dev@archlens.local', password: 'archlens-dev-password', name: 'Developer' }

type DevSignInResult = { readonly ok: true } | { readonly ok: false; readonly message: string }

/**
 * Server function: sign in as the shared local developer account, creating it on first use. Only when
 * ARCHLENS_DEV_LOGIN=true. Only a missing account leads to sign-up; any other failure is reported, never swallowed.
 */
export const devSignIn = createServerFn({ method: 'POST' }).handler(async (): Promise<DevSignInResult> => {
  if (!serverConfig().devLogin) return { ok: false, message: 'Developer sign-in is disabled (ARCHLENS_DEV_LOGIN).' }
  try {
    const a = await auth()
    const headers = getRequestHeaders()
    const existing = await (await a.$context).internalAdapter.findUserByEmail(DEV_USER.email)
    if (!existing) await a.api.signUpEmail({ body: DEV_USER, headers })
    await a.api.signInEmail({ body: { email: DEV_USER.email, password: DEV_USER.password }, headers })
    return { ok: true }
  } catch (cause) {
    console.error('dev sign-in failed', { cause: cause instanceof Error ? cause.message : String(cause) })
    return { ok: false, message: 'Could not sign in as developer. Check that Postgres is running.' }
  }
})

/** Server function: sign out. */
export const signOut = createServerFn({ method: 'POST' }).handler(async () => {
  await (await auth()).api.signOut({ headers: getRequestHeaders() })
})
