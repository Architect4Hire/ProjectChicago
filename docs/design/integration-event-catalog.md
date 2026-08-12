# Project Chicago — Integration Event Catalog

**Status:** Supporting ADR-0015  
**Purpose:** Canonical reference for all cross-service integration events, schemas, and routing

This document defines every integration event published by services and their consumers.

---

## Event Envelope Standard

All integration events use a consistent envelope structure:

```csharp
{
  "eventId": "guid",                        // Unique event instance identifier
  "eventType": "Domain.Entity.Action",      // Versioned event name (e.g., "Crm.Client.Created.v1")
  "version": "1",                           // Schema version for this event type
  "occurredUtc": "2026-08-12T10:30:00Z",   // When the domain event occurred (UTC)
  "correlationId": "guid",                  // User/request correlation ID
  "causationId": "guid",                    // Message ID that caused this event (for causation chains)
  "traceId": "guid",                        // Distributed trace ID
  "actorId": "guid",                        // User/service ID that initiated the action
  "actorType": "User|System|Service",       // Type of actor
  "sourceService": "Crm|Identity|...",      // Service that published this event
  "payload": { ... }                        // Event-specific data
}
```

**Invariants:**
- `eventId`, `traceId`, `correlationId`, `causationId` enable end-to-end traceability
- `actorId` and `actorType` support audit questioning of "who did this"
- `version` allows event contract evolution without breaking consumers
- Sensitive values (passwords, tokens, credentials) must never appear in any field

---

## CRM Service Events

### Crm.Client.Created.v1

**Published when:** New Client created via API  
**Consumers:** Audit, Notification, Search, Workflow  
**Payload:**
```json
{
  "clientId": "guid",
  "name": "string",
  "lifecycleStatus": "Lead|Prospect|Active|OnHold|Inactive|Archived",
  "ownerUserId": "guid",
  "createdUtc": "2026-08-12T10:30:00Z"
}
```

---

### Crm.Client.LifecycleChanged.v1

**Published when:** Client lifecycle status transitions (e.g., Lead → Prospect)  
**Consumers:** Audit, Notification, Search, Workflow  
**Payload:**
```json
{
  "clientId": "guid",
  "previousStatus": "string",
  "newStatus": "string",
  "transitionReason": "string",
  "changedUtc": "2026-08-12T10:30:00Z"
}
```

---

### Crm.Client.Archived.v1

**Published when:** Client moved to Archived status  
**Consumers:** Audit, Notification, Search  
**Payload:**
```json
{
  "clientId": "guid",
  "name": "string",
  "archivedUtc": "2026-08-12T10:30:00Z"
}
```

---

### Crm.Project.Created.v1

**Published when:** New Project created for a Client  
**Consumers:** Audit, Notification, Search, Workflow  
**Payload:**
```json
{
  "projectId": "guid",
  "clientId": "guid",
  "name": "string",
  "status": "Planned|Active|OnHold|Completed|Cancelled|Archived",
  "priority": "Low|Normal|High|Critical",
  "ownerUserId": "guid",
  "startDate": "2026-08-12",
  "targetCompletionDate": "2026-09-12",
  "createdUtc": "2026-08-12T10:30:00Z"
}
```

---

### Crm.Project.StatusChanged.v1

**Published when:** Project status changes  
**Consumers:** Audit, Notification, Search, Workflow  
**Payload:**
```json
{
  "projectId": "guid",
  "clientId": "guid",
  "previousStatus": "string",
  "newStatus": "string",
  "changedUtc": "2026-08-12T10:30:00Z"
}
```

---

### Crm.Project.Completed.v1

**Published when:** Project marked Completed (captures actual completion timestamp)  
**Consumers:** Audit, Notification, Workflow  
**Payload:**
```json
{
  "projectId": "guid",
  "clientId": "guid",
  "name": "string",
  "targetCompletionDate": "2026-09-12",
  "actualCompletionDate": "2026-08-15",
  "completedUtc": "2026-08-15T14:22:00Z"
}
```

---

### Crm.Project.Archived.v1

**Published when:** Project moved to Archived status  
**Consumers:** Audit, Notification, Search  
**Payload:**
```json
{
  "projectId": "guid",
  "clientId": "guid",
  "name": "string",
  "archivedUtc": "2026-08-12T10:30:00Z"
}
```

---

### Crm.Task.Created.v1

**Published when:** New Task created for a Project  
**Consumers:** Audit, Notification, Search, Workflow  
**Payload:**
```json
{
  "taskId": "guid",
  "projectId": "guid",
  "clientId": "guid",
  "title": "string",
  "status": "Backlog|ToDo|InProgress|Blocked|Completed|Cancelled",
  "priority": "Low|Normal|High|Critical",
  "assignedUserId": "guid|null",
  "dueDate": "2026-08-20",
  "createdUtc": "2026-08-12T10:30:00Z"
}
```

---

### Crm.Task.Assigned.v1

**Published when:** Task assigned to a user (initial assignment)  
**Consumers:** Audit, Notification, Search, Workflow  
**Payload:**
```json
{
  "taskId": "guid",
  "projectId": "guid",
  "clientId": "guid",
  "title": "string",
  "assignedUserId": "guid",
  "dueDate": "2026-08-20",
  "assignedUtc": "2026-08-12T10:30:00Z"
}
```

---

### Crm.Task.Reassigned.v1

**Published when:** Task reassigned to a different user  
**Consumers:** Audit, Notification, Workflow  
**Payload:**
```json
{
  "taskId": "guid",
  "projectId": "guid",
  "clientId": "guid",
  "title": "string",
  "previousAssigneeUserId": "guid|null",
  "newAssigneeUserId": "guid",
  "reassignedUtc": "2026-08-12T10:30:00Z"
}
```

---

### Crm.Task.Completed.v1

**Published when:** Task marked Completed (captures completion timestamp)  
**Consumers:** Audit, Notification, Workflow  
**Payload:**
```json
{
  "taskId": "guid",
  "projectId": "guid",
  "clientId": "guid",
  "title": "string",
  "assignedUserId": "guid|null",
  "completedUtc": "2026-08-12T14:30:00Z"
}
```

---

### Crm.Task.Reopened.v1

**Published when:** Completed Task reopened  
**Consumers:** Audit, Notification, Workflow  
**Payload:**
```json
{
  "taskId": "guid",
  "projectId": "guid",
  "clientId": "guid",
  "title": "string",
  "reopenedUtc": "2026-08-12T14:30:00Z"
}
```

---

### Crm.Task.PriorityChanged.v1

**Published when:** Task priority changed  
**Consumers:** Audit, Notification, Workflow  
**Payload:**
```json
{
  "taskId": "guid",
  "projectId": "guid",
  "clientId": "guid",
  "title": "string",
  "previousPriority": "string",
  "newPriority": "string",
  "changedUtc": "2026-08-12T10:30:00Z"
}
```

---

## Identity Service Events

### Identity.User.Created.v1

**Published when:** New user account created  
**Consumers:** Audit, Notification, Workflow  
**Payload:**
```json
{
  "userId": "guid",
  "email": "string",
  "displayName": "string",
  "createdUtc": "2026-08-12T10:30:00Z"
}
```

**Sensitive value redaction:** Never include password hash, reset token, or temporary credentials.

---

### Identity.User.Activated.v1

**Published when:** User account activated (enabled for login)  
**Consumers:** Audit, Notification  
**Payload:**
```json
{
  "userId": "guid",
  "email": "string",
  "activatedUtc": "2026-08-12T10:30:00Z"
}
```

---

### Identity.User.Deactivated.v1

**Published when:** User account deactivated (disabled for login)  
**Consumers:** Audit, Notification  
**Payload:**
```json
{
  "userId": "guid",
  "email": "string",
  "deactivatedUtc": "2026-08-12T10:30:00Z"
}
```

---

### Identity.User.Locked.v1

**Published when:** User account locked due to failed login attempts  
**Consumers:** Audit, Notification  
**Payload:**
```json
{
  "userId": "guid",
  "email": "string",
  "lockedUtc": "2026-08-12T10:30:00Z",
  "lockoutDuration": "00:15:00"
}
```

---

### Identity.PasswordReset.v1

**Published when:** User initiates password reset  
**Consumers:** Audit, Notification  
**Payload:**
```json
{
  "userId": "guid",
  "email": "string",
  "resetRequestedUtc": "2026-08-12T10:30:00Z"
}
```

**Sensitive value redaction:** Never include reset token or new password.

---

### Identity.PasswordChanged.v1

**Published when:** User changes password  
**Consumers:** Audit  
**Payload:**
```json
{
  "userId": "guid",
  "email": "string",
  "changedUtc": "2026-08-12T10:30:00Z"
}
```

**Sensitive value redaction:** Never include old or new password.

---

### Identity.RoleAssigned.v1

**Published when:** Role assigned to user  
**Consumers:** Audit, Notification  
**Payload:**
```json
{
  "userId": "guid",
  "email": "string",
  "roleName": "Administrator|Manager|Contributor|ReadOnly",
  "assignedUtc": "2026-08-12T10:30:00Z"
}
```

---

### Identity.RoleRemoved.v1

**Published when:** Role removed from user  
**Consumers:** Audit, Notification  
**Payload:**
```json
{
  "userId": "guid",
  "email": "string",
  "roleName": "Administrator|Manager|Contributor|ReadOnly",
  "removedUtc": "2026-08-12T10:30:00Z"
}
```

---

## Notification Service Events

### Notification.Sent.v1

**Published when:** Notification delivered or queued  
**Consumers:** Audit, (optional: Workflow for rule dependencies)  
**Payload:**
```json
{
  "notificationId": "guid",
  "recipientUserId": "guid",
  "channel": "InApp|Email|Webhook",
  "triggerEvent": "TaskAssigned|DeadlineApproaching|...",
  "relatedEntityId": "guid",
  "relatedEntityType": "Task|Project|Client",
  "sentUtc": "2026-08-12T10:30:00Z"
}
```

---

### Notification.Failed.v1

**Published when:** Notification delivery fails (transient)  
**Consumers:** Audit  
**Payload:**
```json
{
  "notificationId": "guid",
  "recipientUserId": "guid",
  "channel": "string",
  "failureReason": "string",
  "retryCount": 1,
  "failedUtc": "2026-08-12T10:30:00Z"
}
```

---

### Notification.Delivered.v1

**Published when:** Notification confirmed delivered  
**Consumers:** Audit  
**Payload:**
```json
{
  "notificationId": "guid",
  "recipientUserId": "guid",
  "channel": "string",
  "deliveredUtc": "2026-08-12T10:30:00Z"
}
```

---

## Search Service Events

### Search.IndexUpdated.v1

**Published when:** Search index updated from source event (optional, for observability)  
**Consumers:** Audit (optional)  
**Payload:**
```json
{
  "indexUpdateId": "guid",
  "sourceEventId": "guid",
  "sourceEventType": "string",
  "entityType": "Client|Project|Task",
  "entityId": "guid",
  "operation": "Upsert|Delete",
  "updatedUtc": "2026-08-12T10:30:00Z"
}
```

---

## Workflow Service Events

### Workflow.Triggered.v1

**Published when:** Automation rule evaluates and triggers  
**Consumers:** Audit, (optional: Notification)  
**Payload:**
```json
{
  "workflowExecutionId": "guid",
  "ruleId": "guid",
  "ruleName": "string",
  "sourceEvent": "string",
  "sourceEntityType": "Client|Project|Task",
  "sourceEntityId": "guid",
  "triggeredUtc": "2026-08-12T10:30:00Z"
}
```

---

### Workflow.Executed.v1

**Published when:** Workflow automation completed successfully  
**Consumers:** Audit, Notification  
**Payload:**
```json
{
  "workflowExecutionId": "guid",
  "ruleId": "guid",
  "ruleName": "string",
  "sourceEvent": "string",
  "actions": [
    {
      "actionType": "CreateTask|UpdateProject|SendNotification|PublishEvent",
      "actionResult": "Success|Skipped",
      "resultSummary": "string"
    }
  ],
  "executedUtc": "2026-08-12T10:30:00Z"
}
```

---

### Workflow.Failed.v1

**Published when:** Workflow automation fails  
**Consumers:** Audit, Notification  
**Payload:**
```json
{
  "workflowExecutionId": "guid",
  "ruleId": "guid",
  "ruleName": "string",
  "sourceEvent": "string",
  "failureReason": "string",
  "failedStep": "RuleEvaluation|ActionExecution",
  "failedUtc": "2026-08-12T10:30:00Z"
}
```

---

### Workflow.Compensated.v1

**Published when:** Workflow compensation/rollback executed  
**Consumers:** Audit  
**Payload:**
```json
{
  "workflowExecutionId": "guid",
  "originalExecutionId": "guid",
  "ruleId": "guid",
  "ruleName": "string",
  "compensationReason": "string",
  "compensatedUtc": "2026-08-12T10:30:00Z"
}
```

---

## Event Subscription Matrix

| Event | Crm | Identity | Audit | Notification | Search | Workflow |
|-------|-----|----------|-------|--------------|--------|----------|
| Crm.Client.* | - | - | ✓ | ✓ | ✓ | ✓ |
| Crm.Project.* | - | - | ✓ | ✓ | ✓ | ✓ |
| Crm.Task.* | - | - | ✓ | ✓ | ✓ | ✓ |
| Identity.User.* | - | - | ✓ | ✓ | - | ✓ |
| Identity.Password.* | - | - | ✓ | - | - | - |
| Identity.Role.* | - | - | ✓ | ✓ | - | - |
| Notification.* | - | - | ✓ | - | - | - |
| Search.IndexUpdated.* | - | - | ✓ | - | - | - |
| Workflow.* | - | - | ✓ | ✓ | - | - |

---

## Event Versioning and Evolution

When an event schema must change:

1. **Additive changes (new optional fields):** Increment to v1.1 (backward compatible)
2. **Breaking changes (remove/rename/type change):** Increment major version (Crm.Client.Created.v2)
3. **Deprecation:** Publish both old and new versions for a transition period
4. **Consumer responsibility:** Must handle unexpected schema versions gracefully

---

## Redaction Rules

The following values must NEVER appear in any event field:

- Passwords or password hashes
- Password reset/confirmation tokens
- Authentication secrets or API keys
- Access tokens or refresh tokens
- Session identifiers
- Private cryptographic keys
- Credit card numbers or PII beyond necessity
- Full request/response bodies with sensitive fields

Safe practices:
- Redact before serializing: `"userId": "{{ REDACTED }}"` or omit the field
- Use redaction middleware at the event publishing boundary
- Audit should redact at consumption time if needed for storage
- Notification channel payloads must not repeat sensitive data from source events

---

## Testing and Validation

Each event type requires:
- Unit tests for event serialization/deserialization
- Contract tests verifying publisher/subscriber compatibility
- Integration tests for end-to-end event flow (publish → consume → audit)
- Redaction tests confirming sensitive values never leak
- Version compatibility tests (old subscribers receive new events, vice versa)

---

## References

- [ASYNC-001..008: Asynchronous Processing](../requirements/lightweight-crm-product-and-system-requirements.md#async-001)
- [OUTBOX-001..006: Transactional Outbox](../requirements/lightweight-crm-product-and-system-requirements.md#outbox-001)
- [TRACE-001..007: Traceability](../requirements/lightweight-crm-product-and-system-requirements.md#trace-001)
- [AUDIT-001..008: Business Audit](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001)
- [ADR-0015: Bounded-Context Catalog](adr-0015-bounded-context-catalog.md)
