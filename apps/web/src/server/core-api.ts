import * as sdk from '@archlens/contracts/sdk'
import {
  zCompareRequest,
  zComposeDraftRequest,
  zCreateModelRequest,
  zCreateVersionRequest,
  zCreateWorkspaceRequest,
  zSnapshotRequest,
} from '@archlens/contracts/zod'
import { createServerFn } from '@tanstack/react-start'
import { z } from 'zod'
import { withCore } from './core-client.server'

const id = z.uuid()

/** List the caller's workspaces. */
export const listWorkspaces = createServerFn({ method: 'GET' }).handler(() =>
  withCore((client) => sdk.listWorkspaces({ client })),
)

/** Create a workspace. */
export const createWorkspace = createServerFn({ method: 'POST' })
  .validator(zCreateWorkspaceRequest)
  .handler(({ data }) => withCore((client) => sdk.createWorkspace({ client, body: data })))

/** Get one workspace. */
export const getWorkspace = createServerFn({ method: 'GET' })
  .validator(z.object({ workspaceId: id }))
  .handler(({ data }) => withCore((client) => sdk.getWorkspace({ client, path: data })))

/** List models of a workspace. */
export const listModels = createServerFn({ method: 'GET' })
  .validator(z.object({ workspaceId: id }))
  .handler(({ data }) => withCore((client) => sdk.listModels({ client, path: data })))

/** Create a model, optionally from a snapshot (template or import → version 1). */
export const createModel = createServerFn({ method: 'POST' })
  .validator(z.object({ workspaceId: id, body: zCreateModelRequest }))
  .handler(({ data }) =>
    withCore((client) => sdk.createModel({ client, path: { workspaceId: data.workspaceId }, body: data.body })),
  )

/** Get a model with its latest version. */
export const getModel = createServerFn({ method: 'GET' })
  .validator(z.object({ modelId: id }))
  .handler(({ data }) => withCore((client) => sdk.getModel({ client, path: data })))

/** List versions of a model, newest first. */
export const listVersions = createServerFn({ method: 'GET' })
  .validator(z.object({ modelId: id }))
  .handler(({ data }) => withCore((client) => sdk.listVersions({ client, path: data })))

/** Get a version with its snapshot. */
export const getVersion = createServerFn({ method: 'GET' })
  .validator(z.object({ versionId: id }))
  .handler(({ data }) => withCore((client) => sdk.getVersion({ client, path: data })))

/** Save a new immutable version (parent must be the latest version). */
export const createVersion = createServerFn({ method: 'POST' })
  .validator(z.object({ modelId: id, body: zCreateVersionRequest }))
  .handler(({ data }) =>
    withCore((client) => sdk.createVersion({ client, path: { modelId: data.modelId }, body: data.body })),
  )

/** Semantic diff between two versions. */
export const diffVersions = createServerFn({ method: 'GET' })
  .validator(z.object({ modelId: id, from: id, to: id }))
  .handler(({ data }) =>
    withCore((client) =>
      sdk.diffVersions({ client, path: { modelId: data.modelId }, query: { from: data.from, to: data.to } }),
    ),
  )

/** Draft + diff + drift from a docker-compose file. */
export const composeDraft = createServerFn({ method: 'POST' })
  .validator(z.object({ modelId: id, body: zComposeDraftRequest }))
  .handler(({ data }) =>
    withCore((client) => sdk.createComposeDraft({ client, path: { modelId: data.modelId }, body: data.body })),
  )

/** Problems of an (unsaved) snapshot, as Core judges them. */
export const validateSnapshot = createServerFn({ method: 'POST' })
  .validator(zSnapshotRequest)
  .handler(({ data }) => withCore((client) => sdk.validateSnapshot({ client, body: data })))

/** Every view of an (unsaved) snapshot, derived by Core. */
export const deriveViews = createServerFn({ method: 'POST' })
  .validator(zSnapshotRequest)
  .handler(({ data }) => withCore((client) => sdk.deriveViews({ client, body: data })))

/** Compare graph of one view between two stored versions. */
export const compareVersions = createServerFn({ method: 'GET' })
  .validator(z.object({ modelId: id, from: id, to: id, view: z.string().min(1) }))
  .handler(({ data }) =>
    withCore((client) =>
      sdk.compareVersions({
        client,
        path: { modelId: data.modelId },
        query: { from: data.from, to: data.to, view: data.view },
      }),
    ),
  )

/** Compare graph of one view between two snapshots (the saved version and a draft). */
export const compareSnapshots = createServerFn({ method: 'POST' })
  .validator(zCompareRequest)
  .handler(({ data }) => withCore((client) => sdk.compareSnapshots({ client, body: data })))

/** The ShopFlow reference template. */
export const getShopFlowTemplate = createServerFn({ method: 'GET' }).handler(() =>
  withCore((client) => sdk.getShopFlowTemplate({ client })),
)
