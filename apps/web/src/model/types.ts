import type * as Schemas from '@archlens/contracts/types'

/** A model snapshot as Core defines it. */
export type Snapshot = Schemas.ModelSnapshot
/** A C4 element. */
export type Element = Schemas.Element
/** An explicit relationship. */
export type Relationship = Schemas.Relationship
/** C4 element kind. */
export type ElementKind = Schemas.ElementKind
/** Canvas position. */
export type Point = Schemas.Point
/** One derived view, as drawn by the editor. */
export type ViewGraph = Schemas.ViewGraph
/** Union of a view in two snapshots with change statuses. */
export type CompareGraph = Schemas.CompareGraph
/** A node of a compare graph. */
export type CompareNode = Schemas.CompareNode
/** An edge of a compare graph. */
export type CompareEdge = Schemas.CompareEdge
/** Change status of a node or edge. */
export type ChangeStatus = Schemas.ChangeStatus
/** One changed field. */
export type FieldChange = Schemas.FieldChange
/** Semantic diff. */
export type DiffResult = Schemas.DiffResult
/** Total number of changed elements and relationships. */
export function totalChanges(s: DiffResult['summary']): number {
  return (
    s.elementsAdded +
    s.elementsRemoved +
    s.elementsModified +
    s.relationshipsAdded +
    s.relationshipsRemoved +
    s.relationshipsModified
  )
}

/** Drift rule R1 result. */
export type DriftResult = Schemas.DriftResult
/** Kind of a C4 view. */
export type ViewKind = Schemas.ViewKind
/** Identity of a drawn view. */
export type GraphView = Schemas.GraphView
/** Model metadata. */
export type ModelInfo = Schemas.Model
/** Version with its snapshot. */
export type ModelVersion = Schemas.ModelVersion

/** An empty model (what "Empty model" starts from). */
export const emptySnapshot: Snapshot = { schemaVersion: 1, elements: [], relationships: [], layout: {} }

/** Labels of element kinds (presentation only). */
export const KIND_LABEL: Readonly<Record<ElementKind, string>> = {
  person: 'Person',
  softwareSystem: 'Software system',
  container: 'Container',
  component: 'Component',
}

const FIELD_LABEL: Readonly<Record<string, string>> = {
  name: 'name',
  description: 'description',
  technology: 'technology',
  tags: 'tags',
  parentId: 'parent',
  external: 'external',
  sourceId: 'source',
  targetId: 'target',
}

/** Label of a changed field. */
export function fieldLabel(field: string): string {
  return FIELD_LABEL[field] ?? field
}

/**
 * Shape check at the import boundary: a file is sent to Core only if it looks like a snapshot or an exported
 * version (`{ ..., snapshot }`). Core then validates every model rule.
 */
export function snapshotFromFile(value: unknown): Snapshot | null {
  const inner = typeof value === 'object' && value !== null && 'snapshot' in value ? value.snapshot : value
  if (typeof inner !== 'object' || inner === null) return null
  const looksRight =
    'elements' in inner &&
    Array.isArray(inner.elements) &&
    'relationships' in inner &&
    Array.isArray(inner.relationships)
  return looksRight ? ({ layout: {}, schemaVersion: 1, ...inner } as Snapshot) : null
}
