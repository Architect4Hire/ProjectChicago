# Project Chicago

 Lightweight CRM Product and System Requirements

## 1. Purpose

Project Chicago is a production-quality, lightweight Customer Relationship Management application designed to manage the operational relationship between an organization and its clients.

The application centers on three primary business entities:

- **Clients**

- **Projects**

- **Tasks**

The application shall intentionally remain smaller and simpler than enterprise CRM platforms. It shall focus on the most common workflows required to understand:

1. Who the organization's clients are.

2. What work is being performed for each client.

3. What tasks remain to complete that work.

4. Where each client currently sits in its lifecycle.

5. Who performed every meaningful action in the system.

6. What happened to every API request or asynchronous operation from entry through completion.

Project Chicago must be suitable for production use and must provide enterprise-grade security, auditing, reliability, and observability despite its lightweight functional scope.

---

# 2. Product Principles

The following principles govern all product and technical requirements.

### PR-001 — Lightweight by Design

The system shall provide the minimum functionality necessary to manage clients, projects, and tasks effectively.

Functionality such as marketing automation, sales forecasting, invoicing, quoting, email campaigns, customer support ticketing, and ERP capabilities are outside the initial scope unless explicitly introduced through future requirements.

### PR-002 — Client-Centric Navigation

The Client shall be the primary business anchor of the application.

From a Client, an authorized user shall be able to understand:

- Client identity and contact information.

- Current lifecycle status.

- Active and historical projects.

- Open and completed tasks associated with those projects.

- Recent activity.

- Audit history.

### PR-003 — Traceable by Default

Every significant system interaction shall be traceable.

The system shall support tracing a request or operation:

> User → Gateway → API → Service → Database → Message → Azure Function → Downstream Service → Completion

without requiring developers or operators to manually correlate unrelated logs.

### PR-004 — Auditable by Default

Business data shall never change without the system being able to answer:

- What changed?

- When did it change?

- Who changed it?

- What was the previous value?

- What became the new value?

- Through which request or process was it changed?

### PR-005 — Secure by Default

Authentication, authorization, secrets management, service-to-service security, input validation, and least-privilege access shall be built into the architecture rather than added after feature development.

### PR-006 — Observable by Default

Every API, service, Azure Function, Service Bus operation, and database interaction shall participate in distributed OpenTelemetry tracing and centralized monitoring.

---

# 3. Primary Users

Project Chicago shall initially support internal organizational users.

## 3.1 Administrator

Administrators may:

- Manage application users.

- Manage roles and permissions.

- View all Clients, Projects, and Tasks.

- Create, update, archive, and restore records.

- View audit history.

- Access operational and administrative information.

## 3.2 Manager

Managers may:

- View Clients, Projects, and Tasks within their authorized scope.

- Create and update Clients.

- Create and manage Projects.

- Create, assign, update, and complete Tasks.

- View applicable audit history.

## 3.3 Contributor

Contributors may:

- View authorized Clients and Projects.

- View Tasks assigned to them or otherwise available within their authorization scope.

- Create and update Tasks where permitted.

- Complete assigned Tasks.

## 3.4 Read-Only User

Read-only users may:

- View authorized Clients.

- View authorized Projects.

- View authorized Tasks.

They shall not modify business data.

---

# 4. Client Requirements

## 4.1 Client Definition

A Client represents an organization or individual with whom the organization has an active or historical business relationship.

Each Client shall have a unique internal system identifier.

The database identity shall never be used as the only user-visible identification mechanism.

---

## 4.2 Required Client Information

### CLIENT-001

The system shall allow an authorized user to create a Client.

### CLIENT-002

A Client shall support, at minimum:

- Client ID

- Client name

- Primary contact name

- Primary email address

- Primary phone number

- Website

- Address

- City

- State/Province

- Postal code

- Country

- Lifecycle status

- Description or notes

- Assigned owner

- Created date/time

- Created by

- Last modified date/time

- Last modified by

Not every business field must be mandatory.

The minimum required fields shall be configurable through application validation rules where appropriate.

### CLIENT-003

Client names shall be searchable.

### CLIENT-004

The system should detect likely duplicate Clients during creation using reasonable matching criteria such as:

- Name

- Email

- Phone number

Duplicate detection shall warn the user rather than silently merge records.

---

# 5. Client Lifecycle

Project Chicago shall provide a simple client lifecycle rather than implementing a full sales opportunity pipeline.

## CLIENT-010

Each Client shall have exactly one current lifecycle status.

The initial lifecycle statuses shall be:

- Lead

- Prospect

- Active

- On Hold

- Inactive

- Archived

### CLIENT-011

Lifecycle transitions shall be auditable.

### CLIENT-012

The system shall retain the history of lifecycle status changes through the audit system.

### CLIENT-013

Archived Clients shall not appear in normal active Client lists unless explicitly requested.

### CLIENT-014

Archiving a Client shall not physically delete the Client's historical information.

### CLIENT-015

A Client containing active Projects shall not be permanently removed from the system.

---

# 6. Client Search and List Requirements

## CLIENT-020

Users shall be able to view a paginated Client list.

## CLIENT-021

Users shall be able to search Clients by:

- Name

- Contact name

- Email

- Phone

## CLIENT-022

Users shall be able to filter Clients by:

- Lifecycle status

- Assigned owner

- Active/inactive state

## CLIENT-023

Users shall be able to sort Clients by commonly used attributes including:

- Name

- Created date

- Modified date

- Lifecycle status

## CLIENT-024

Client list APIs shall use server-side pagination.

Unbounded result sets shall not be permitted.

---

# 7. Client Detail

## CLIENT-030

The Client detail experience shall provide a consolidated view containing:

- Client information

- Lifecycle status

- Assigned owner

- Active Projects

- Historical Projects

- Open Tasks associated with Client Projects

- Recently completed Tasks

- Recent activity

- Audit history where the user has permission to view it

### CLIENT-031

Users shall be able to navigate directly from a Client to its Projects.

### CLIENT-032

Users shall be able to navigate from the Client to Tasks belonging to those Projects.

---

# 8. Project Requirements

## 8.1 Project Definition

A Project represents a body of work performed for a Client.

Every Project shall belong to exactly one Client.

---

## PROJECT-001

Authorized users shall be able to create a Project for a Client.

## PROJECT-002

A Project shall contain, at minimum:

- Project ID

- Client ID

- Project name

- Description

- Status

- Priority

- Project owner

- Start date

- Target completion date

- Actual completion date

- Notes

- Created date/time

- Created by

- Last modified date/time

- Last modified by

---

# 9. Project Status

## PROJECT-010

A Project shall support the following initial statuses:

- Planned

- Active

- On Hold

- Completed

- Cancelled

- Archived

## PROJECT-011

Changing Project status shall generate an audit record.

## PROJECT-012

Completing a Project shall capture an actual completion timestamp.

## PROJECT-013

A completed Project may contain completed Tasks.

Open Tasks shall require explicit user acknowledgement before the Project can be marked Completed.

## PROJECT-014

Projects shall not be physically deleted as part of ordinary user workflows.

Projects shall instead be archived.

---

# 10. Project Listing and Search

## PROJECT-020

Users shall be able to view Projects:

- Across all authorized Clients.

- For a specific Client.

- Assigned to a specific Project owner.

## PROJECT-021

Projects shall be filterable by:

- Client

- Status

- Owner

- Priority

- Start date

- Target completion date

## PROJECT-022

Projects shall be searchable by:

- Project name

- Client name

- Description where appropriate

## PROJECT-023

Project lists shall use server-side pagination.

---

# 11. Project Detail

## PROJECT-030

The Project detail view shall show:

- Project information

- Client

- Project status

- Project owner

- Priority

- Important dates

- Open Tasks

- Completed Tasks

- Recent activity

- Audit history where authorized

## PROJECT-031

Users shall be able to create a Task directly from a Project.

---

# 12. Task Requirements

## 12.1 Task Definition

A Task represents an actionable unit of work associated with a Project.

Every Task shall belong to exactly one Project.

Through the Project relationship, every Task therefore belongs indirectly to exactly one Client.

---

## TASK-001

Authorized users shall be able to create Tasks.

## TASK-002

A Task shall support:

- Task ID

- Project ID

- Title

- Description

- Status

- Priority

- Assigned user

- Created by

- Start date

- Due date

- Completed date/time

- Notes

- Created date/time

- Last modified date/time

- Last modified by

---

# 13. Task Status and Workflow

## TASK-010

Tasks shall initially support the statuses:

- Backlog

- To Do

- In Progress

- Blocked

- Completed

- Cancelled

## TASK-011

A completed Task shall record its completion date and time.

## TASK-012

Reopening a completed Task shall be permitted for authorized users and shall generate an audit event.

## TASK-013

Users shall be able to assign and reassign Tasks.

## TASK-014

Task assignment changes shall be auditable.

## TASK-015

Users shall be able to change Task priority.

Initial priority values shall be:

- Low

- Normal

- High

- Critical

## TASK-016

Overdue Tasks shall be identifiable when:

- The Task has a due date earlier than the current date/time.

- The Task is not completed or cancelled.

---

# 14. Task Views

## TASK-020

Users shall be able to view:

- Tasks assigned to them.

- Tasks belonging to a specific Project.

- Open Tasks.

- Completed Tasks.

- Overdue Tasks.

## TASK-021

Task lists shall support filtering by:

- Status

- Priority

- Assignee

- Project

- Client

- Due date

## TASK-022

Task lists shall support sorting by:

- Due date

- Priority

- Created date

- Modified date

---

# 15. Dashboard Requirements

## DASH-001

The application shall provide a lightweight dashboard.

## DASH-002

The dashboard should provide summary information including:

- Active Clients

- Active Projects

- Projects approaching their target completion date

- Open Tasks

- Tasks assigned to the current user

- Overdue Tasks

- Recently completed Tasks

- Recent Client activity

## DASH-003

Dashboard data shall respect the current user's authorization scope.

---

# 16. Global Search

## SEARCH-001

The application shall provide a global search mechanism.

## SEARCH-002

Global search shall support locating:

- Clients

- Projects

- Tasks

## SEARCH-003

Search results shall clearly identify the entity type.

## SEARCH-004

Search results shall respect authorization boundaries.

A user shall never discover restricted data through search that they could not otherwise access.

---

# 17. Data Integrity

## DATA-001

Relationships shall enforce:

```text
CLIENT
   |
   +---- PROJECT
             |
             +---- TASK
```

## DATA-002

A Project shall not exist without a Client.

## DATA-003

A Task shall not exist without a Project.

## DATA-004

Foreign key relationships shall be enforced at the data layer where technically appropriate.

## DATA-005

Application validation shall occur before database mutation.

Database constraints shall provide an additional integrity boundary.

## DATA-006

All timestamps persisted by backend systems shall use UTC.

Localization into the user's timezone shall occur at the presentation boundary.

## DATA-007

Externally exposed identifiers shall be resistant to enumeration.

## DATA-008

Concurrency conflicts shall not silently overwrite newer data.

The API shall provide an appropriate optimistic-concurrency mechanism for mutable business records.

---

# 18. Record Archival

## DATA-020

Normal application workflows shall prefer archival over destructive deletion for Clients, Projects, and Tasks.

## DATA-021

Archived records shall remain available for:

- Audit

- Reporting

- Historical relationships

- Administrative recovery

## DATA-022

Only explicitly authorized administrative processes may permanently purge information.

## DATA-023

Permanent deletion shall comply with documented retention and privacy requirements.

---

# 19. Authentication

## SEC-001

Project Chicago shall use **Microsoft ASP.NET Core Identity** as its application identity system.

## SEC-002

The system shall not implement custom password storage or custom password hashing.

## SEC-003

Authentication functionality shall use the security primitives supplied by ASP.NET Core Identity.

## SEC-004

The system shall support account:

- Creation

- Activation

- Deactivation

- Lockout

- Password reset

- Password change

where applicable to the configured authentication model.

## SEC-005

Authentication events shall be auditable.

Examples include:

- Successful login

- Failed login

- Account lockout

- Password reset

- User activation

- User deactivation

Sensitive credentials shall never be recorded in the audit log.

---

# 20. Authorization

## SEC-010

The application shall enforce authorization on the server.

Client-side authorization controls shall only improve usability and shall never be treated as security enforcement.

## SEC-011

Authorization shall use ASP.NET Core authorization mechanisms including:

- Roles

- Claims

- Policies

as appropriate.

## SEC-012

Every API operation that accesses protected business information shall require explicit authorization.

## SEC-013

Every mutation operation shall verify the user's authorization before changing data.

## SEC-014

Functions performing system-level processing shall operate using application/service identities rather than shared user credentials.

## SEC-015

Azure resources shall use Managed Identity wherever supported.

## SEC-016

Least privilege shall be applied to:

- SQL databases

- Service Bus

- Key Vault

- Storage

- Azure Functions

- APIs

- Deployment identities

---

# 21. API Security

## SEC-020

All public application API access shall flow through the designated Project Chicago gateway.

Backend services shall not intentionally expose independently consumable public endpoints.

## SEC-021

All API traffic shall use HTTPS.

## SEC-022

API input shall be validated before reaching business operations.

## SEC-023

The API shall protect against common web and API vulnerabilities including:

- Injection

- Broken access control

- Excessive data exposure

- Invalid object references

- Cross-site scripting where applicable

- Cross-site request forgery where applicable

- Improper authentication

- Security misconfiguration

## SEC-024

Application logs shall never contain:

- Passwords

- Authentication secrets

- Access tokens

- Refresh tokens

- Connection strings

- Private cryptographic keys

## SEC-025

Personally identifiable information shall not be unnecessarily duplicated into application logs.

---

# 22. Complete API Request Traceability

Cradle-to-grave request tracing is a mandatory architectural requirement.

## TRACE-001

Every inbound API request shall receive or participate in a globally unique distributed trace.

## TRACE-002

The system shall support W3C distributed tracing conventions.

## TRACE-003

Trace context shall propagate across:

- Gateway

- APIs

- Domain/service operations

- Database calls

- HTTP calls

- Service Bus messages

- Azure Functions

- Downstream services

## TRACE-004

A trace initiated from an HTTP request shall remain logically correlated if that request produces asynchronous processing.

For example:

```text
Browser
   ↓
YARP Gateway
   ↓
Client API
   ↓
SQL Transaction
   ↓
Outbox Record
   ↓
Outbox Function
   ↓
Azure Service Bus
   ↓
Consumer Function
   ↓
Project Service
   ↓
SQL Database
```

Operations throughout that flow shall be discoverable through the originating trace or explicitly associated trace links.

## TRACE-005

Each request shall make the following diagnostic information available where applicable:

- Trace ID

- Span ID

- Parent span ID

- Correlation information

- Request route

- HTTP method

- Response status

- Processing duration

- Service name

- Service version

- Environment

- Deployment information

- Authenticated user identifier where permitted

- Exception information when applicable

## TRACE-006

Business identifiers may be attached to telemetry where useful, such as:

- Client ID

- Project ID

- Task ID

Sensitive business data shall not be indiscriminately added to traces.

## TRACE-007

The system shall make it possible for an operator to start with an API request and identify every participating service and operation.

---

# 23. OpenTelemetry Requirements

## OTEL-001

Every Project Chicago API, service, and Azure Function shall support **OpenTelemetry**.

## OTEL-002

OpenTelemetry shall be the standard application instrumentation mechanism for:

- Distributed traces

- Application metrics

- Structured log correlation

## OTEL-003

Services shall instrument, where applicable:

- ASP.NET Core requests

- HTTP clients

- SQL database operations

- Azure Service Bus operations

- Azure Functions

- Internal business operations

- Exceptions

## OTEL-004

Application-specific spans should be created for important business operations where automatic instrumentation alone does not explain system behavior.

Examples include:

- `Client.Create`

- `Client.UpdateLifecycle`

- `Project.Create`

- `Project.Complete`

- `Task.Assign`

- `Task.Complete`

- `Outbox.Publish`

## OTEL-005

Telemetry shall use consistent resource attributes including:

- Application name

- Service name

- Service version

- Deployment environment

## OTEL-006

Trace and log context shall be correlated automatically whenever technically possible.

---

# 24. Single Pane of Glass Observability

## OBS-001

Project Chicago shall provide centralized observability across all APIs, Azure Functions, and infrastructure involved in application execution.

## OBS-002

Azure Monitor and Application Insights shall be capable of acting as the primary operational single pane of glass.

## OBS-003

Operators shall be able to investigate:

- Individual requests

- Distributed traces

- Application exceptions

- Dependency failures

- Service Bus processing

- Function executions

- Database dependencies

- Performance degradation

- Availability

- Failed authentication activity

- Application health

from centralized telemetry.

## OBS-004

Telemetry shall allow filtering by:

- Environment

- Service

- Trace ID

- User identifier where appropriate

- Client ID

- Project ID

- Task ID

- Exception type

- HTTP route

## OBS-005

Dashboards shall expose, at minimum:

- Request rate

- Error rate

- Request latency

- Dependency latency

- Dependency failure rate

- Function execution success/failure

- Service Bus processing failures

- Service Bus dead-letter activity

- Database dependency health

---

# 25. Structured Logging

## LOG-001

Application logging shall be structured.

## LOG-002

Production application behavior shall not depend on parsing human-formatted log strings.

## LOG-003

Logs shall automatically contain distributed trace correlation information when a trace is active.

## LOG-004

Logs should contain meaningful operational context without logging entire request or response payloads by default.

## LOG-005

Exceptions shall be logged with:

- Exception type

- Error message

- Stack trace where appropriate

- Trace ID

- Service

- Operation

## LOG-006

Duplicate logging of the same exception at every architectural layer shall be avoided.

The error shall normally be recorded at the boundary responsible for handling or reporting it.

---

# 26. Business Audit Requirements

Application audit information and diagnostic logs are different concepts and shall not be treated interchangeably.

Application logs may expire according to operational retention policies.

Business audit records shall comply with explicit audit retention rules.

## AUDIT-001

Every mutation to Clients, Projects, and Tasks shall generate an audit event.

## AUDIT-002

An audit event shall record:

- Audit event ID

- Entity type

- Entity identifier

- Action

- Timestamp UTC

- Actor/user ID

- Actor type

- Trace ID

- Request correlation information

- Source service

- Previous values when applicable

- New values when applicable

## AUDIT-003

Common actions shall include:

- Created

- Updated

- Status Changed

- Assigned

- Reassigned

- Completed

- Reopened

- Archived

- Restored

## AUDIT-004

Audit records shall be append-only through normal application workflows.

## AUDIT-005

Ordinary application users shall not be able to modify historical audit records.

## AUDIT-006

Audit events generated by asynchronous processes shall identify both:

- The system process performing the action.

- The originating user or operation when known.

## AUDIT-007

Audit history shall preserve the distributed Trace ID linking the business event to its technical execution.

This allows an operator to move between:

```text
Business Audit Event
        ↕
Distributed Trace
        ↕
Application Logs
```

## AUDIT-008

Sensitive values shall be redacted from audit history.

Passwords, authentication secrets, tokens, and cryptographic information shall never be captured.

---

# 27. Asynchronous Processing

## ASYNC-001

Operations that require durable asynchronous processing shall use Azure Service Bus.

## ASYNC-002

Consumers shall be implemented using Azure Functions with Service Bus triggers where appropriate.

## ASYNC-003

Azure Functions shall support distributed OpenTelemetry instrumentation.

## ASYNC-004

Message metadata shall provide sufficient information to correlate asynchronous work with the initiating operation.

## ASYNC-005

Message consumers shall be designed to tolerate duplicate message delivery.

## ASYNC-006

Business operations triggered by messages shall be idempotent where duplicate processing could otherwise create incorrect results.

## ASYNC-007

Poison messages shall not be retried indefinitely.

Failed messages shall eventually be available through an appropriate dead-letter process.

## ASYNC-008

Dead-letter conditions shall generate observable operational signals.

---

# 28. Transactional Outbox

## OUTBOX-001

When a business transaction must both modify database state and publish an integration message, the system shall use the transactional outbox pattern.

## OUTBOX-002

The business state change and outbox event shall participate in the same database transaction.

## OUTBOX-003

A timer-triggered Azure Function shall be capable of draining pending outbox messages.

## OUTBOX-004

Publishing an outbox record shall be idempotent.

## OUTBOX-005

An outbox message shall preserve relevant trace and business correlation metadata.

## OUTBOX-006

The system shall provide observable metrics for:

- Pending outbox messages

- Failed publications

- Retry attempts

- Oldest unpublished message age

---

# 29. Error Handling

## ERROR-001

APIs shall return consistent error responses.

## ERROR-002

Internal exception details and stack traces shall not be returned to external callers in production.

## ERROR-003

Validation failures shall be distinguishable from:

- Authentication failures

- Authorization failures

- Missing resources

- Concurrency conflicts

- Internal system failures

## ERROR-004

Every unexpected API error shall remain traceable through the distributed Trace ID.

## ERROR-005

User-facing errors should include a safe support/reference identifier that can be correlated with telemetry.

---

# 30. API Design

## API-001

HTTP APIs shall follow consistent REST-oriented conventions.

## API-002

Resources shall use consistent routes.

Examples:

```text
/api/clients
/api/clients/{clientId}

/api/clients/{clientId}/projects
/api/projects/{projectId}

/api/projects/{projectId}/tasks
/api/tasks/{taskId}
```

The final route design shall respect service boundaries.

## API-003

HTTP methods shall use their conventional meanings:

- GET — retrieve

- POST — create

- PUT/PATCH — update

- DELETE — archive/delete where explicitly supported

## API-004

HTTP response codes shall be used consistently.

## API-005

Collection endpoints shall provide pagination.

## API-006

API contracts shall be documented through OpenAPI.

## API-007

API contracts shall be versionable without exposing implementation details.

---

# 31. Performance

Project Chicago is not intended to optimize prematurely for massive enterprise CRM workloads, but common workflows must remain responsive.

## PERF-001

Normal interactive API requests should target a server-side response time below 500 ms at the 95th percentile under expected production load, excluding intentionally asynchronous work.

## PERF-002

Common indexed searches should normally return within interactive response expectations.

## PERF-003

No API shall retrieve an unlimited collection of Clients, Projects, Tasks, or audit records.

## PERF-004

Database queries shall avoid unnecessary N+1 query behavior.

---

# 32. Reliability

## REL-001

Transient infrastructure failures shall use controlled retry policies where retrying is safe.

## REL-002

Retries shall use bounded attempts and appropriate backoff.

## REL-003

Non-transient failures shall not be continuously retried.

## REL-004

The system shall avoid cascading failures by applying appropriate timeouts and resilience controls to remote dependencies.

## REL-005

Services shall expose health information appropriate for their hosting environment.

---

# 33. Data Storage

## DATA-030

Project Chicago shall use Microsoft SQL Server / Azure SQL for relational business data.

## DATA-031

Each independently deployed bounded service shall own its database.

## DATA-032

One service shall not directly query another service's database.

## DATA-033

Cross-service communication shall occur through defined APIs or integration events.

## DATA-034

Database schemas shall support migrations through controlled application deployment practices.

---

# 34. Privacy and Sensitive Data

## PRIV-001

Only information necessary to operate the CRM shall be collected.

## PRIV-002

Sensitive data shall not be copied unnecessarily between services.

## PRIV-003

Logs and telemetry shall minimize exposure of personally identifiable information.

## PRIV-004

Access to sensitive Client information shall respect authorization policies.

## PRIV-005

Data retention policies shall be documented before production deployment.

---

# 35. User Experience

## UX-001

The application shall prioritize simple workflows over feature density.

## UX-002

Users should normally be able to reach:

- A Client

- A Project

- An assigned Task

within a small number of navigation actions.

## UX-003

Forms shall clearly display:

- Required fields

- Validation errors

- Successful saves

- Failed operations

## UX-004

Destructive or irreversible operations shall require explicit user intent.

## UX-005

Loading, empty, failure, and unauthorized states shall be deliberately designed.

## UX-006

The user interface shall be responsive and usable on common desktop and tablet display sizes.

---

# 36. Accessibility

## ACCESS-001

The frontend shall target WCAG 2.2 AA accessibility practices.

## ACCESS-002

Interactive functionality shall be keyboard accessible.

## ACCESS-003

Inputs shall have accessible labels.

## ACCESS-004

Validation messages shall be programmatically associated with their controls.

## ACCESS-005

Color alone shall not communicate application state.

---

# 37. Application Design System

## DESIGN-001

Project Chicago shall use the Project Chicago Design System (PCDS) for reusable application UI components and visual tokens.

## DESIGN-002

Feature implementations shall not independently recreate components that already exist within PCDS.

## DESIGN-003

Application styling shall use shared design tokens for:

- Typography

- Spacing

- Color

- Borders

- Elevation

- States

- Layout

## DESIGN-004

Accessibility behavior shall be incorporated into PCDS components where possible.

---

# 38. Testing Requirements

## TEST-001

Business rules shall have automated tests.

## TEST-002

Authorization rules shall have automated tests.

## TEST-003

API endpoints shall have integration tests covering important successful and unsuccessful flows.

## TEST-004

Database behavior shall be tested against Microsoft SQL-compatible infrastructure rather than relying exclusively on incompatible in-memory substitutes.

## TEST-005

Message consumers shall have tests for:

- Normal processing

- Duplicate messages

- Invalid messages

- Retry conditions

- Permanent failures

## TEST-006

Audit generation shall be testable.

## TEST-007

Distributed tracing instrumentation shall be verified for representative synchronous and asynchronous request paths.

---

# 39. Deployment and Environment Requirements

## DEPLOY-001

Application configuration shall support multiple environments without modifying application code.

Expected environments should include:

- Local Development

- Test

- Production

Additional environments may be introduced.

## DEPLOY-002

Secrets shall not be stored in source control.

## DEPLOY-003

Production secrets shall be maintained through an approved secrets-management mechanism such as Azure Key Vault.

## DEPLOY-004

Azure Functions shall be compatible with Azure Functions Flex Consumption hosting.

## DEPLOY-005

Services shall expose consistent OpenTelemetry resource information across environments.

---

# 40. Operational Health

## OPS-001

Operators shall be able to determine whether each service is healthy.

## OPS-002

Health checks shall distinguish, where useful:

- Application process health

- Dependency health

- Readiness to serve requests

## OPS-003

The system shall expose enough telemetry to detect:

- Increasing error rates

- Increasing latency

- Database failures

- Service Bus failures

- Function failures

- Authentication anomalies

- Dead-letter accumulation

- Outbox backlog

## OPS-004

Operational alert thresholds shall be configurable without changing business code.

---

# 41. Application Activity

Project Chicago shall provide lightweight activity visibility without creating a separate social-style activity subsystem.

## ACTIVITY-001

Recent activity may be derived from business audit events.

## ACTIVITY-002

A Client activity view should show significant events such as:

- Client created

- Client lifecycle changed

- Project created

- Project status changed

- Task created

- Task assigned

- Task completed

## ACTIVITY-003

Activity display shall use user-friendly descriptions while retaining links to underlying audit information where authorized.

---

# 42. Entity Relationship Summary

The initial business model shall remain intentionally simple.

```text
CLIENT
│
├── Client information
├── Lifecycle
├── Owner
│
└── PROJECTS
     │
     ├── Project information
     ├── Status
     ├── Owner
     │
     └── TASKS
          ├── Assignment
          ├── Priority
          ├── Status
          └── Due Date
```

Audit, identity, telemetry, outbox, and integration records are supporting technical constructs rather than additional CRM business entities.

---

# 43. Core User Journeys

## Journey 1 — Add a Client

```text
User signs in
→ Opens Clients
→ Selects Add Client
→ Enters Client information
→ System validates input
→ System checks authorization
→ Client is created
→ Audit event is written
→ Distributed request telemetry is recorded
→ User sees Client detail
```

## Journey 2 — Create a Project

```text
User opens Client
→ Selects Add Project
→ Enters Project information
→ System validates input
→ Project is associated with Client
→ Project is created
→ Audit event is written
→ User sees Project detail
```

## Journey 3 — Create and Assign a Task

```text
User opens Project
→ Selects Add Task
→ Enters task information
→ Selects assignee
→ Task is created
→ Assignment is recorded
→ Audit event is written
→ Assignee can see Task
```

## Journey 4 — Complete a Task

```text
User opens Task
→ Marks Task Completed
→ Authorization is validated
→ Completion timestamp recorded
→ Audit event written
→ Project task status is refreshed
```

## Journey 5 — Investigate a Production Failure

```text
User reports failure
→ Support obtains request reference / Trace ID
→ Operator opens Application Insights
→ Finds gateway request
→ Follows distributed trace
→ Identifies API operation
→ Identifies SQL/HTTP/Service Bus dependency
→ Follows asynchronous Function execution if applicable
→ Reviews correlated structured logs
→ Determines failure origin
```

This operational journey is a first-class Project Chicago requirement.

---

# 44. Definition of Done for Every API Feature

No API-backed feature shall be considered production-complete unless all applicable requirements below are satisfied.

For every endpoint:

- Authentication is implemented.

- Authorization is implemented.

- Input validation is implemented.

- API contract is documented.

- Appropriate HTTP responses are implemented.

- Errors use the standard error format.

- Distributed tracing is present.

- Structured logs correlate to the active trace.

- Sensitive information is not logged.

- Business mutation creates the appropriate audit event.

- Database changes are transactional where required.

- Integration messages use the transactional outbox where required.

- Automated tests exist.

- Observability has been verified.

- The feature can be diagnosed through the centralized observability platform.

---

# 45. Definition of Done for Every Azure Function

No Azure Function shall be considered production-complete unless:

- Trigger configuration is externalized appropriately.

- OpenTelemetry instrumentation is enabled.

- Trace context is consumed or linked where available.

- Structured logging is implemented.

- Errors are observable.

- Retry behavior is deliberate.

- Duplicate delivery is considered.

- Idempotency is implemented when required.

- Poison message handling is defined.

- Authentication/service identity requirements are satisfied.

- Sensitive data is not logged.

- Automated tests cover the processing behavior.

---

# 46. Initial Non-Goals

The initial Project Chicago product shall not assume the existence of:

- Sales opportunities

- Sales forecasting

- Quotes

- Invoices

- Payments

- Marketing campaigns

- Email marketing

- Customer support ticketing

- Inventory

- Products/catalogs

- Time tracking

- Resource planning

- Contract management

- Document management

- Complex workflow designers

- AI assistants

- External customer portals

These capabilities may be introduced later through explicit requirements.

Their absence should prevent the initial implementation from becoming an unnecessarily large CRM platform.

---

# 47. Requirement Priorities

Requirements shall use the following classification when converted into implementation prompts.

### P0 — Mandatory Foundation

Examples:

- Authentication

- Authorization

- Clients

- Projects

- Tasks

- SQL persistence

- Auditability

- Distributed tracing

- OpenTelemetry

- Error handling

- API security

- Transactional integrity

### P1 — Required Product Experience

Examples:

- Dashboard

- Search

- Filtering

- Lifecycle management

- Task assignment

- Overdue Task identification

- Activity views

### P2 — Enhancement

Examples:

- Advanced dashboards

- Expanded reporting

- Additional lifecycle automation

- Optional notification behaviors

- Additional search capabilities

A P2 feature must not complicate or delay a P0 architectural requirement.

---

# 43a. Notification Service Requirements

Project Chicago shall provide event-driven notifications to keep users informed of meaningful CRM events.

## NOTIF-001

The system shall evaluate CRM and identity events against configured notification rules.

## NOTIF-002

Initial notification triggers shall include:

- Task assigned to user
- Task due date approaching (24 hours)
- Project status changed to Active
- Project status changed to Completed
- Project approaching target completion date
- Client lifecycle changed
- User account created or activated
- Recurring activity digest (summary of recent changes)

## NOTIF-003

Notifications shall be sent through initially supported channels:

- In-app inbox (stored in Notification service database)
- Email (via configured mail provider)
- Webhook (for external integrations)

## NOTIF-004

Users shall be able to configure notification preferences per event type and channel.

## NOTIF-005

Notification delivery failures shall be retried with bounded attempts and backoff.

## NOTIF-006

Notification history shall be queryable (by user, date range, event type, delivery status).

---

# 43b. Search Service Requirements

Project Chicago shall provide full-text search and advanced filtering across Clients, Projects, and Tasks.

## SEARCH-001

The Search Service shall maintain a denormalized, eventually-consistent read-model of CRM entities.

## SEARCH-002

Search shall support queries on:

- Client name, contact name, email, phone, website, notes
- Project name, description, owner, status, priority, dates
- Task title, description, assignee, status, priority, due date

## SEARCH-003

Search results shall return paginated, sortable, and filterable collections.

## SEARCH-004

Search results shall respect user authorization scope (read-only users see their authorized subset).

## SEARCH-005

Search index shall synchronize from CRM events with acceptable latency (eventual consistency model acceptable).

## SEARCH-006

Archived Clients and Projects shall be excluded from default search results unless explicitly included.

---

# 43c. Workflow Automation Requirements

Project Chicago shall support automation rules that react to CRM events and orchestrate actions across service boundaries.

## WORKFLOW-001

The system shall allow administrators to define workflow automation rules.

## WORKFLOW-002

Workflow rules shall be triggered by CRM and Identity events.

## WORKFLOW-003

Initial rule actions shall include:

- Create Task
- Update Project status
- Update Client lifecycle status
- Assign Task
- Send Notification
- Publish integration event

## WORKFLOW-004

Rules shall support conditions such as:

- Event type matching
- Entity property matching (e.g., Project status = "Active")
- Time-based conditions (e.g., X days since Project created)
- Actor role or user matching

## WORKFLOW-005

Workflow execution shall be auditable; every action triggered by a rule shall be recorded with the workflow execution ID.

## WORKFLOW-006

Failed workflow actions shall be logged and made observable; the system shall support manual retry or compensation.

## WORKFLOW-007

Workflow rules shall be versionable; template changes shall not affect in-flight executions.

## WORKFLOW-008

Workflow execution history shall be queryable (by rule, date range, status, error logs).

---

# 48. Requirements Governance

These requirements shall act as the functional source of truth used when producing Project Chicago SCRUB prompts.

SCRUB implementation prompts shall:

1. Reference specific requirement IDs.

2. Implement only the requirements identified by the prompt.

3. Avoid inventing additional business requirements.

4. Preserve the architecture and security constraints.

5. Preserve audit and telemetry requirements even when implementing small features.

6. Prefer microsteps where each prompt makes one logically reviewable change.

7. Require automated verification before considering a microstep complete.

8. Prevent implementation agents from bypassing architectural boundaries for convenience.

Where the requirements do not define behavior, the implementation prompt shall not silently create a new business rule.

A missing business decision shall instead be documented as an unresolved requirement.

---

# 49. Project Chicago Quality Bar

Project Chicago is considered successful when the application remains functionally simple while providing the engineering characteristics normally expected from a much larger enterprise system.

A user should experience:

> **Clients → Projects → Tasks**

An engineer or operator should see:

> **Identity → Authorization → Request → Trace → Business Operation → Database Transaction → Audit → Integration Event → Function → Telemetry**
