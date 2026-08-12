# Tracing a Slice — Create a Client

This document follows one requirement-backed mutation through every layer and asynchronous boundary. It is the Project Chicago counterpart to JobBoard's “trace a slice” walkthrough.

> The Crm/Audit bounded contexts shown here depend on proposed ADR-0015/0016. The flow becomes authoritative only when those ADRs are accepted.

## User intent

An authorized Manager creates a new Client.

## 1. React

The Client create form:
- uses PCDS fields/buttons,
- sends only the public request DTO,
- uses the shared Gateway API client,
- never knows an internal service address,
- handles server validation, duplicate warning/policy, 401/403 and conflict/error states.

## 2. YARP

YARP:
- receives HTTPS request,
- participates in W3C tracing,
- normalizes/creates approved correlation reference,
- applies the approved auth/session edge behavior,
- routes the stable public CRM path through service discovery.

No CRM business validation occurs here.

## 3. CRM Controller

The controller:
- binds `CreateClientRequest`,
- obtains trusted authenticated actor/context,
- calls Client Facade,
- maps typed result to public response/ProblemDetails.

It does not inject DbContext/Repository/Service Bus.

## 4. Facade

The Client Facade:
- verifies use-case validation,
- enforces authorization/resource scope,
- evaluates duplicate-warning behavior,
- calls Business only.

## 5. Business

Business:
- applies Client creation rules/default lifecycle,
- normalizes/translates models,
- creates the business/audit fact representing the actual mutation,
- calls Data only.

## 6. Data transaction

Data opens one CrmDb transaction:

```text
INSERT Client
INSERT OutboxMessage(ClientCreated/Audit fact)
COMMIT
```

If either insert fails, neither is committed.

The HTTP request can return after the local durable commit. It does **not** wait synchronously for Audit.

## 7. Timer Function

`Crm.Functions` TimerTrigger invokes the reusable outbox relay:
- leases bounded pending records,
- publishes the versioned envelope,
- preserves trace/correlation/causation/actor/event IDs,
- marks dispatched only after successful Service Bus send.

## 8. Service Bus

Service Bus provides durable at-least-once delivery. Duplicate delivery is expected.

## 9. Audit Function

Audit ServiceBusTrigger:
- binds/deserializes,
- establishes/extracts OTel context,
- delegates to Audit Facade,
- allows unexpected failure to fail the invocation.

## 10. Audit Core/Data

Audit:
- validates supported contract/redaction rules,
- checks persistent Inbox by EventId,
- appends one AuditEntry,
- completes Inbox state transactionally.

Replay of the same EventId must not create another AuditEntry.

## 11. Observability view

An operator should be able to pivot across:
- original YARP/API TraceId,
- CRM custom business span,
- SQL dependency,
- outbox EventId,
- relay Function,
- Service Bus operation,
- Audit consumer Function,
- Audit SQL dependency,
- durable AuditEntry.

CorrelationId connects the logical flow even where async trace parentage/lifetime differs.

## 12. Tests that defend the slice

| Boundary | Test |
|---|---|
| Client rules | unit |
| EF/transaction/outbox | SQL integration |
| Controller/auth/error contract | API integration |
| Timer trigger delegation | Function unit |
| Relay failure behavior | Shared/Core test |
| Audit inbox/idempotency | SQL integration |
| Audit trigger | Function unit |
| React form | component/accessibility |
| Full flow | local Aspire end-to-end trace |

That is what “cradle to grave” means in Project Chicago: not one large log statement, but an intentionally correlated set of durable state, traces and business audit evidence.
