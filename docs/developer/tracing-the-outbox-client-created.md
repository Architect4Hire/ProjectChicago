# Tracing the Client-Created Outbox Flow

This guide is for diagnosing the durable path after a Client mutation commits.

## Expected path

```text
CrmDb Client row
  + CrmDb OutboxMessage row
        ↓
Crm Timer Function
        ↓
Azure Service Bus
        ↓
Audit ServiceBusTrigger Function
        ↓
Audit InboxMessage + AuditEntry
```

## 1. Start from the HTTP trace

Capture the safe response/support reference or TraceId/CorrelationId from the create-Client request.

In Aspire Dashboard locally or Application Insights in production:
- find the YARP request,
- follow the CRM API span,
- confirm SQL dependency success,
- record TraceId and CorrelationId.

## 2. Check the local transaction result

In CrmDb:
- Client exists,
- matching outbox event exists,
- outbox CorrelationId/CausationId/EventId are populated.

If Client exists but no outbox row exists, the mutation violated the atomic audit/outbox requirement.

## 3. Check relay state

Find the Crm Timer Function execution around the publication time.

Confirm:
- batch found the row,
- Service Bus send succeeded,
- outbox row marked dispatched **after** broker acceptance,
- failure count/retry metrics if it did not.

A failed send should leave the message pending/retryable.

## 4. Check Service Bus / Audit Function

Confirm the Audit consumer received the same EventId and logical correlation. If delivery failed repeatedly, inspect the dead-letter state/reason.

## 5. Check AuditDb

Find:
- InboxMessage for the EventId,
- one AuditEntry for the Client Created action.

Replay/redelivery must not create a second AuditEntry.

## Common failure signatures

| Symptom | Likely boundary |
|---|---|
| Client saved, no outbox | Data transaction/event creation |
| Outbox pending forever | timer schedule, relay DI, SQL lease |
| Outbox marked sent, no broker message | relay correctness bug—must investigate immediately |
| Broker message retries | Audit Function/Core failure |
| Duplicate AuditEntry | inbox/idempotency transaction bug |
| Trace broken after bus | propagation/extraction/link configuration |
