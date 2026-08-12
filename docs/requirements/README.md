# Project Chicago Requirements

The canonical business/system requirements are:

- [Lightweight CRM Product and System Requirements](lightweight-crm-product-and-system-requirements.md)

## Requirements are authoritative for product behavior

Architecture and implementation artifacts may explain *how* a requirement is satisfied, but they must not silently invent business behavior not present in the requirements.

Requirement families include Clients, Projects, Tasks, dashboard/search, data integrity, security, tracing, OpenTelemetry, observability, audit, asynchronous messaging, outbox, errors, APIs, performance, reliability, privacy, UX/accessibility, design system, testing, deployment and operations.

## Change discipline

When requirements change:

1. update the requirement and ID deliberately,
2. assess affected ADR/design docs,
3. update the traceability matrix,
4. add/change the smallest SCRUB micro-prompts,
5. update tests,
6. only then change implementation.

Do not edit old requirements merely to make current code look compliant.
