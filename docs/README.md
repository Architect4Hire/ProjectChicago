# Project Chicago Documentation

This folder is the navigational center for Project Chicago. It is organized like the reference JobBoard documentation set, but every artifact has been rewritten for the Project Chicago requirements and architecture.

## Recommended reading order

1. [Product and System Requirements](requirements/lightweight-crm-product-and-system-requirements.md)
2. [High-Level Design](design/high-level-design.md)
3. [Proposed Solution Structure](PROPOSED-SOLUTION-STRUCTURE.md)
4. [Architecture Decision Records](adr/README.md)
5. [Product Completeness](design/product-completeness.md)
6. [Ongoing Architecture Plan](design/ongoing-architecture-plan.md)
7. [SCRUB Microstep Prompts](prompts/project-chicago-scrub-microprompts.md)
8. [Developer Patterns](developer/patterns/README.md)

## Design documents

| Document                                                         | Purpose                                                                                          |
| ---------------------------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| [High-Level Design](design/high-level-design.md)                 | System context, containers, bounded contexts, data ownership, synchronous and asynchronous flows |
| [Domain Model](design/domain-model.md)                           | Client, Project and Task model, lifecycle/state rules and invariants                             |
| [Security Design](design/security-design.md)                     | Identity, authentication decision boundary, authorization, secrets and API security              |
| [Observability Design](design/observability-design.md)           | Trace/log/metric design and cradle-to-grave correlation                                          |
| [Product Completeness](design/product-completeness.md)           | Requirements coverage and distinction between specified, prompt-covered and implemented          |
| [Ongoing Architecture Plan](design/ongoing-architecture-plan.md) | Decisions still open, planned validation work and architectural evolution                        |
| [Requirements Traceability](design/requirements-traceability.md) | Requirement families mapped to design artifacts, ADRs and implementation prompts                 |
| [Why This Architecture](why-this-architecture.md)                |                                                                                                  |
| [Prompting Logic](project-story.md)                              |                                                                                                  |

## Requirements Traceability Matrix

| Document                                                                    | Purpose                                                                                                                                                                                   |
| --------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [Requirements-to-Prompt Matrix](matrix/requirements-traceability-matrix.md) | Complete mapping of all 298 requirement IDs to the 164 SCRUB implementation prompts (P000–P163); shows architecture, implementation, and verification prompts for each requirement family |

## Architecture Decision Records

The [ADR index](adr/README.md) separates **accepted constraints** already established by Project Chicago from **proposed decisions** that the SCRUB sequence intentionally asks a human to approve.

Do not mark a proposed ADR accepted merely because downstream design documents illustrate it.

## Developer guides

| Guide                                                                                   | Purpose                                                         |
| --------------------------------------------------------------------------------------- | --------------------------------------------------------------- |
| [Adding an endpoint manually](developer/adding-an-endpoint-manually.md)                 | Contract-to-controller path without breaking the layer arrows   |
| [Adding a Function trigger](developer/adding-a-function-trigger.md)                     | Thin Functions, Service Bus triggers and timer triggers         |
| [Adding seed data](developer/adding-seed-data.md)                                       | Safe, development-only seed strategy                            |
| [Tracing the outbox ClientCreated flow](developer/tracing-the-outbox-client-created.md) | Debug durable publication from SQL to Audit                     |
| [Testing strategy](developer/testing-strategy.md)                                       | Unit, SQL integration, API, Function, UI and architecture tests |
| [Pattern index](developer/patterns/README.md)                                           | Deep dives into recurring mechanics                             |

## Walkthrough

[Tracing a slice: Create a Client](tracing-a-slice-create-a-client.md) follows one mutation from React/YARP through the CRM layers, SQL transaction, outbox relay, Service Bus, Audit Function, AuditDb and OpenTelemetry.

## Prompts

- [Project Chicago SCRUB micro-prompts](prompts/project-chicago-scrub-microprompts.md) — canonical ordered implementation sequence.
- [Audit SCRUB prompts](prompts/audit-scrub-prompts.md) — focused audit subset/runbook; use it as a review/extraction aid, not as a second independent implementation plan.
- [Prompts README](prompts/README.md) — execution rules.

## Requirements

- [Lightweight CRM Product and System Requirements](requirements/lightweight-crm-product-and-system-requirements.md)
- [Requirements README](requirements/README.md)

## Documentation rules

- **Requirements** say what the product/system must do.
- **ADRs** say what architectural decisions have been made.
- **Design docs** explain how accepted decisions fit together and clearly label proposed assumptions.
- **Developer docs** teach implementation within those decisions.
- **Prompts** perform one atomic implementation action and prove it.
- **Code/runtime evidence** ultimately determines what is implemented.

If an artifact becomes stale, update or supersede it deliberately. Never let a confident design document silently overrule code or an accepted ADR.
