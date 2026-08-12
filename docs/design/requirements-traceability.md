# Project Chicago — Requirements Traceability

This is a navigation map, not a substitute for the canonical requirement IDs.

| Requirement family | Primary design            | ADR/pattern anchors            | Prompt coverage                   |
| ------------------ | ------------------------- | ------------------------------ | --------------------------------- |
| PR                 | HLD, Product Completeness | ADR-0015                       | 000–011                           |
| CLIENT             | Domain Model              | archival/concurrency           | Client prompt phase               |
| PROJECT            | Domain Model              | archival/concurrency           | Project prompt phase              |
| TASK               | Domain Model              | archival/concurrency           | Task prompt phase                 |
| DASH               | HLD/Product Completeness  | data ownership                 | dashboard prompts                 |
| SEARCH             | HLD                       | gateway/data ownership         | search prompts                    |
| DATA               | HLD/Domain Model          | ADR-0001, 0014                 | persistence + SQL verification    |
| SEC                | Security Design           | ADR-0006, 0007, 0018           | Identity/security prompts         |
| TRACE              | Observability Design      | ADR-0010, 0011                 | shared telemetry + E2E trace      |
| OTEL               | Observability Design      | ADR-0011                       | ServiceDefaults/telemetry prompts |
| OBS                | Observability Design      | ADR-0011                       | dashboard/metrics prompts         |
| LOG                | Observability Design      | ADR-0011                       | common telemetry/error prompts    |
| AUDIT              | HLD/Observability         | ADR-0016                       | Audit service prompts             |
| ASYNC              | HLD                       | ADR-0002, 0004, 0013, 0017     | messaging/Function prompts        |
| OUTBOX             | HLD                       | ADR-0003                       | outbox/relay prompts              |
| ERROR              | Security/HLD              | error-shape pattern            | ProblemDetails/API prompts        |
| API                | HLD/Security              | ADR-0006                       | endpoint/gateway prompts          |
| PERF               | Product Completeness      | data/query patterns            | performance gate                  |
| REL                | HLD                       | messaging/concurrency patterns | resilience verification           |
| PRIV               | Security Design           | ADR-0016                       | audit/security prompts            |
| UX                 | Product Completeness      | PCDS pattern                   | React prompts                     |
| ACCESS             | Product Completeness      | ADR-0012                       | React/accessibility gate          |
| DESIGN             | HLD                       | ADR-0012                       | PCDS prompts                      |
| TEST               | Testing Strategy          | all architecture patterns      | every microstep + release gates   |
| DEPLOY             | Ongoing Plan              | ADR-0008, 0013, 0019, 0020     | AppHost/deployment prompts        |
| OPS                | Observability Design      | ADR-0011                       | health/metrics/alerts prompts     |

The canonical micro-prompt document also contains/should contain an exhaustive requirement-ID-to-prompt matrix. Use that machine-like mapping for completeness checks; use this document for human navigation.
