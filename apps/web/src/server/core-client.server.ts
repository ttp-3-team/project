import { createClient, createConfig } from '@archlens/contracts/client'
import { SignJWT } from 'jose'
import { serverConfig } from './config.server'
import { currentUser } from './session.server'
import type { Client } from '@archlens/contracts/client'
import type { ProblemDetails } from '@archlens/contracts/types'
import type { SessionUser } from './session.server'

/** A Core failure in a shape the UI can render: Core's problem details plus the model problems it lists. */
export type CoreError = {
  readonly status: number
  readonly title: string
  readonly detail: string
  readonly problems: ReadonlyArray<string>
}

/** Result of a BFF call: a value or a `CoreError`. */
export type CoreResult<T> = { readonly ok: true; readonly value: T } | { readonly ok: false; readonly error: CoreError }

const TOKEN_TTL_SECONDS = 5 * 60

async function mintCoreToken(user: SessionUser): Promise<string> {
  const key = new TextEncoder().encode(serverConfig().jwtSecret.unwrap())
  return new SignJWT({ name: user.name })
    .setProtectedHeader({ alg: 'HS256', typ: 'JWT' })
    .setSubject(user.id)
    .setIssuer('archlens-web')
    .setAudience('archlens-core')
    .setIssuedAt()
    .setExpirationTime(`${TOKEN_TTL_SECONDS}s`)
    .sign(key)
}

function fail(status: number, title: string, detail = '', problems: ReadonlyArray<string> = []): CoreResult<never> {
  return { ok: false, error: { status, title, detail, problems } }
}

function problemOf(status: number, body: unknown): CoreResult<never> {
  const problem: ProblemDetails & { problems?: unknown } = typeof body === 'object' && body !== null ? body : {}
  const problems = Array.isArray(problem.problems)
    ? problem.problems.filter((p): p is string => typeof p === 'string')
    : []
  return fail(status, problem.title ?? `Core responded ${status}`, problem.detail ?? '', problems)
}

/**
 * Run one or more calls of the generated SDK as the signed-in user. The browser never sees the token: it exists only here.
 *
 * @template T - The value produced from Core's responses.
 */
export async function withCore<T>(
  run: (client: Client) => Promise<{ data?: T; error?: unknown; response?: Response }>,
): Promise<CoreResult<T>> {
  const user = await currentUser()
  if (!user) return fail(401, 'Sign-in required')
  const token = await mintCoreToken(user)
  const client = createClient(
    createConfig({ baseUrl: serverConfig().coreUrl, headers: { Authorization: `Bearer ${token}` } }),
  )
  try {
    const { data, error, response } = await run(client)
    const status = response?.status ?? 503
    if (response?.ok && data !== undefined) return { ok: true, value: data }
    if (status === 401)
      return fail(502, 'Core rejected the BFF token', 'Check that ARCHLENS_JWT_SECRET is the same in web and Core.')
    return problemOf(status, error)
  } catch (cause) {
    console.error('core call failed', { cause: cause instanceof Error ? cause.message : String(cause) })
    return fail(503, 'ArchLens Core unavailable', 'Check that Core is running.')
  }
}
