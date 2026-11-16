import { defineConfig } from '@hey-api/openapi-ts'

export default defineConfig({
  input: './openapi.json',
  output: { path: './generated/ts' },
  plugins: ['@hey-api/typescript', { name: 'zod', requests: true, responses: false }, '@hey-api/client-fetch', { name: '@hey-api/sdk' }],
})
