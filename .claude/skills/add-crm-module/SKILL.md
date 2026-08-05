---
name: add-crm-module
description: >
  Add one CRM area to the two-project API/Domain solution with an MVC controller boundary and mandatory
  Facade -> Business -> Data onion structure. Defines ownership, layer contracts, DI, data ownership,
  architecture tests, and admission criteria without creating feature operations.
---
# Add One CRM Area

## Discovery gate

Before changing code, discover the actual solution/project paths, namespaces, target frameworks, package versions, AppHost resource names, SQLDB connection name, DbContext, migrations assembly, test conventions, and feature location. Never treat example names as repository facts. Stop without editing when a required value cannot be proven. Aspire is required and is the supported source of local SQLDB connection information.

## Goal

Create the structural boundary for one cohesive CRM area. This microstep creates structure and registration
only; it does not implement endpoints, entities, migrations, or Angular screens.

## Admission test

Create a new area only when it owns a durable cluster of business rules, data operations, terminology, and
change cadence. Do not create an area merely for one controller, one table, utilities, or technical plumbing.

## Required structure

```text
src/<api-project>/
├── Controllers/<Area>Controller.cs      # only when an empty controller is repository convention
└── Contracts/<Area>/

src/<domain-project>/<Area>/
├── Facade/
├── Business/
├── Data/
└── <Area>Registration.cs
```

The call chain is always Controller -> Facade -> Business -> Data. Do not create Application, Handler,
Repository, Endpoint, or vertical-slice folders that bypass or duplicate these layers.

## Ownership charter

Document:

- Business capability and vocabulary.
- Data/tables the area owns.
- Invariants owned by Business.
- Validation/cache responsibilities owned by Facade.
- Persistence responsibilities owned by Data.
- Controller routes expected later.
- Cross-area dependencies and which layer coordinates them.

## Boundary contracts

- Controllers see only Facade interfaces/models.
- Facades see only Business interfaces/models plus approved context/cache abstractions.
- Business sees only Data interfaces/models plus domain abstractions.
- Data sees EF Core/SQL Server and no upper-layer types.
- No model crosses more than one seam.

## Registration

- API composition root calls one Domain registration extension.
- Area registration wires Facade, Business, and Data implementations with compatible lifetimes.
- Do not put each feature registration directly in `Program.cs`.
- Do not use reflection/service locator unless already standardized and tested.

## Architecture tests

Add or extend tests that fail on:

- API Controller referencing Business/Data namespaces.
- Facade referencing Data/EF namespaces.
- Business referencing API/Facade/cache-provider/EF namespaces.
- Data referencing API/Facade/Business implementations.
- Domain project referencing API project.
- Minimal API route mappings for product endpoints.

## Verification

Build solution and run architecture tests. Report future microsteps separately: first seam model/interface,
Data operation, Business operation, Facade operation, controller action, migration, and Angular integration.

## Completion checklist

- [ ] Area passes admission test.
- [ ] Ownership charter exists.
- [ ] Facade/Business/Data folders exist in Domain.
- [ ] API has only HTTP-facing placeholders consistent with convention.
- [ ] Registration extension exists.
- [ ] No operation/entity/migration was added.
- [ ] Architecture tests enforce all forbidden references.
- [ ] Solution builds.
