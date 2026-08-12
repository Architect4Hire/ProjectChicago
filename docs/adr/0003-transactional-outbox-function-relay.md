# ADR-0003 — Transactional Outbox with Timer-Triggered Function Relay

- **Status:** Accepted
- **Requirements:** OUTBOX-001..006, ASYNC-001..008

## Context
A business mutation that both changes SQL state and emits an event must not create a dual-write gap.

## Decision
The owning service writes business state and an outbox row in the same SQL transaction. A timer-triggered Azure Function in that service's sibling `.Functions` project drains pending outbox records and publishes them to Azure Service Bus.

Only the outbox relay sends the integration event. Request-path code never publishes directly.

## Consequences
- Database commit is the local source of truth.
- Publication is eventually consistent.
- Relay needs bounded batches, lease/concurrency protection, retry metadata and observability.
- A successful broker send must precede marking a row dispatched.
- No `BackgroundService`/`IHostedService` is used for outbox draining.

## Alternatives considered
- Direct Service Bus send in the HTTP transaction: rejected due to dual-write failure modes.
- Aspire-wired background worker: rejected; Functions are the async hosting model.
- Database CDC: not selected for the initial system.

## Validation
SQL tests prove state + outbox atomicity; relay tests prove failure leaves records pending; end-to-end tracing proves SQL → timer Function → Service Bus.
