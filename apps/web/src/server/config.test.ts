import { describe, expect, it } from 'vitest'
import { DevLoginNotLocal, readEnv, toConfig } from './config.server'

const base = {
  DATABASE_URL: 'postgres://u:p@localhost:5433/db',
  BETTER_AUTH_SECRET: 'x'.repeat(32),
  ARCHLENS_JWT_SECRET: 'y'.repeat(32),
}

describe('server configuration', () => {
  it('derives defaults and keeps secrets redacted', () => {
    const config = toConfig(readEnv(base))
    expect(config).not.toBeInstanceOf(DevLoginNotLocal)
    if (config instanceof DevLoginNotLocal) return
    expect(config).toMatchObject({
      baseUrl: 'http://localhost:3000',
      coreUrl: 'http://localhost:5080',
      devLogin: false,
      github: null,
    })
    expect(String(config.jwtSecret)).toBe('<redacted>')
  })

  it('enables GitHub only with both credentials', () => {
    expect(toConfig(readEnv({ ...base, GITHUB_CLIENT_ID: 'id', GITHUB_CLIENT_SECRET: '' }))).toMatchObject({
      github: null,
    })
  })

  it('refuses the dev login on a public URL', () => {
    expect(
      toConfig(readEnv({ ...base, ARCHLENS_DEV_LOGIN: 'true', BETTER_AUTH_URL: 'https://archlens.example' })),
    ).toBeInstanceOf(DevLoginNotLocal)
  })

  it('rejects a short secret', () => {
    expect(() => readEnv({ ...base, ARCHLENS_JWT_SECRET: 'short' })).toThrow()
  })
})
