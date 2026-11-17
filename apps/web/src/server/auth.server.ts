import { betterAuth } from 'better-auth'
import { getMigrations } from 'better-auth/db/migration'
import { tanstackStartCookies } from 'better-auth/tanstack-start'
import { Pool } from 'pg'
import { serverConfig } from './config.server'

function create() {
  const config = serverConfig()
  return betterAuth({
    appName: 'ArchLens',
    baseURL: config.baseUrl,
    secret: config.authSecret.unwrap(),
    database: new Pool({ connectionString: config.databaseUrl.unwrap() }),
    emailAndPassword: { enabled: config.devLogin },
    socialProviders: config.github
      ? { github: { clientId: config.github.clientId, clientSecret: config.github.clientSecret.unwrap() } }
      : {},
    plugins: [tanstackStartCookies()],
  })
}

/** The configured Better Auth instance type. */
export type Auth = ReturnType<typeof create>

let instance: Auth | undefined
let migrated: Promise<void> | undefined

/**
 * Better Auth, created on first use. Its tables are created/updated in Postgres before the first request.
 * A failed migration is not cached: the next request tries again (e.g. once Postgres is up).
 */
export async function auth(): Promise<Auth> {
  instance ??= create()
  const current = instance
  migrated ??= getMigrations(current.options)
    .then(({ runMigrations }) => runMigrations())
    .catch((cause: unknown) => {
      migrated = undefined
      throw cause
    })
  await migrated
  return current
}
