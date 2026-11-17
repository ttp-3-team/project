import { createServerEntry, createStartHandler, defaultStreamHandler } from '@tanstack/react-start/server'
import { serverConfig } from './server/config.server'

serverConfig()

export default createServerEntry({ fetch: createStartHandler(defaultStreamHandler) })
