import { existsSync } from 'node:fs'
import { createEnv } from '@t3-oss/env-core'
import { z } from 'zod'
import { Redacted } from './redacted'

type ServerConfig = {
  readonly databaseUrl: Redacted
  readonly authSecret: Redacted
  readonly baseUrl: string
  readonly github: { readonly clientId: string; readonly clientSecret: Redacted } | null
  readonly devLogin: boolean
  readonly coreUrl: string
  readonly jwtSecret: Redacted
}

/** The dev login was enabled for a public URL. */
export class DevLoginNotLocal extends Error {
  readonly _tag = 'DevLoginNotLocal' as const

  constructor() {
    super('ARCHLENS_DEV_LOGIN=true is allowed only when BETTER_AUTH_URL is localhost')
  }
}

const secret = z.string().min(32)

/**
 * Read and check the environment (t3 env): unknown or invalid values stop the server with a list of the variables.
 *
 * @throws When a variable is missing or invalid: a startup failure.
 */
export function readEnv(runtimeEnv: Record<string, string | undefined>) {
  return createEnv({
    server: {
      DATABASE_URL: z.string().min(1),
      BETTER_AUTH_SECRET: secret,
      BETTER_AUTH_URL: z.url().default('http://localhost:3000'),
      GITHUB_CLIENT_ID: z.string().optional(),
      GITHUB_CLIENT_SECRET: z.string().optional(),
      ARCHLENS_DEV_LOGIN: z.enum(['true', 'false']).default('false'),
      ARCHLENS_CORE_URL: z.url().default('http://localhost:5080'),
      ARCHLENS_JWT_SECRET: secret,
    },
    runtimeEnv,
    isServer: true,
    emptyStringAsUndefined: true,
  })
}

/** Derive the application configuration from the checked environment. */
export function toConfig(env: ReturnType<typeof readEnv>): ServerConfig | DevLoginNotLocal {
  const devLogin = env.ARCHLENS_DEV_LOGIN === 'true'
  if (devLogin && !isLocalhost(env.BETTER_AUTH_URL)) return new DevLoginNotLocal()
  return {
    databaseUrl: new Redacted(env.DATABASE_URL),
    authSecret: new Redacted(env.BETTER_AUTH_SECRET),
    baseUrl: env.BETTER_AUTH_URL,
    github:
      env.GITHUB_CLIENT_ID && env.GITHUB_CLIENT_SECRET
        ? { clientId: env.GITHUB_CLIENT_ID, clientSecret: new Redacted(env.GITHUB_CLIENT_SECRET) }
        : null,
    devLogin,
    coreUrl: env.ARCHLENS_CORE_URL.replace(/\/$/, ''),
    jwtSecret: new Redacted(env.ARCHLENS_JWT_SECRET),
  }
}

function isLocalhost(url: string): boolean {
  const host = new URL(url).hostname
  return host === 'localhost' || host === '127.0.0.1' || host === '::1'
}

let cached: ServerConfig | undefined

/**
 * The server configuration. Loads `.env.local` / `.env` (without overriding real environment variables) on first use.
 * Called once by the server entry, so a misconfigured server refuses to start.
 *
 * @throws On invalid configuration: a startup failure.
 */
export function serverConfig(): ServerConfig {
  if (cached) return cached
  for (const file of ['.env.local', '.env']) if (existsSync(file)) process.loadEnvFile(file)
  const config = toConfig(readEnv(process.env))
  if (config instanceof DevLoginNotLocal) throw config
  cached = config
  return config
}
