# ShopFlow reference models (AL-20)

Converted once from the ArchLens PoC fixtures (Structurizr workspace JSON of `model/workspace.dsl`):

- DSL identifiers are kept as element ids; hierarchy maps to `parentId`.
- Implied (linked) relationships are dropped: ArchLens derives them in views.
- Relationship ids are `r-<source>-<target>`.
- Deployment nodes and instances are dropped; every container with a container instance in the
  `Production` environment is tagged `deployed` instead (used by drift rule R1).
- Default Structurizr tags (`Element`, `Container`, ...) are dropped; `External` becomes `external: true`.

`head.json` differs from `base.json` by the PoC's model changes:

1. `mobileApp` renamed "Mobile App" → "Mobile Storefront" and technology changed (one modification).
2. Container `cache` (Session Cache, Redis) added …
3. … with relationship `apiGateway → cache`.
4. Relationship `notificationService → mobileApp` removed.
5. (The PoC's Production deployment change disappears with deployments.)

The expected diff was written by hand from these changes and is now the Verify snapshot
`tests/ArchLens.Domain.Tests/ViewImpactTests.ShopFlow_reference_pair_matches_expected_diff.verified.json`
(the integration test checks the same diff through the API).
Views per C4 rules: context; one container view per software system that has containers; one
component view per container that has components. Only `container:shop` is affected: the
implied relationships `apiGateway → cache` and `notificationService → mobileApp` both lift to
`shop → shop` in the context view and are dropped there.

`compose-*.yml` are the PoC compose files, unchanged: `compose-base.yml` matches `base.json`,
`compose-drift.yml` adds `cache` and an unmodelled `worker`, `compose-fixed.yml` matches `head.json`.
