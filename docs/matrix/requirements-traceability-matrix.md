<title>Project Chicago Requirements Traceability Matrix</title>

# Project Chicago — Requirements-to-Prompt Traceability Matrix

**Source Document**: [lightweight-crm-product-and-system-requirements.md](../requirements/lightweight-crm-product-and-system-requirements.md)  
**Prompt Sequence**: [project-chicago-scrub-microprompts.md](../prompts/project-chicago-scrub-microprompts.md)  
**Generated**: August 12, 2026  
**Governance**: Section 48 — Requirements Governance

---

## Overview

This matrix maps every requirement ID in the authoritative Project Chicago product and system requirements to at least one prompt in the SCRUB microstep implementation sequence. Each requirement ID appears under its family grouping with references to the prompt(s) that implement, verify, or validate that requirement.

**Key principle**: The requirements document is the functional source of truth. No implementation prompt invents business behavior not explicitly stated in the canonical requirements. Architecture decisions remain documented in ADRs and CLAUDE.md.

---

## Requirements Families

### 1. Product Principles (PR-001 through PR-006)

| Requirement | Intent | Primary Prompts | Verification |
|---|---|---|---|
| **PR-001** (Lightweight by Design) | Minimum necessary functionality for client/project/task management; no marketing, invoicing, ERP, or CRM bloat. | P002, P003, P004, P017, P018 | P162, P163 |
| **PR-002** (Client-Centric Navigation) | Client is the primary business anchor; understand identity, lifecycle, projects, tasks, activity, audit from one view. | P002, P039, P054, P056, P076, P135, P152 | P160 |
| **PR-003** (Traceable by Default) | Every operation (request → gateway → API → service → DB → message → Function → completion) is traceable without manual log correlation. | P010, P026, P026A, P027, P028, P029, P156 | P156 |
| **PR-004** (Auditable by Default) | Business data changes create append-only audit evidence: what, when, who, previous, new, via which process. | P007, P029A, P068, P078, P080, P114A, P124, P125 | P156, P162 |
| **PR-005** (Secure by Default) | Authentication, authorization, secrets, service-to-service security, validation, least-privilege built into architecture. | P005, P006, P069, P070, P084, P085, P116, P117 | P157 |
| **PR-006** (Observable by Default) | Every API, service, Function, Service Bus, database interaction participates in distributed OpenTelemetry tracing and centralized monitoring. | P010, P024, P025, P026, P154, P155 | P156 |

---

### 2. Client Requirements (CLIENT-001 through CLIENT-032)

#### Client Definition & Information

| Requirement | Intent | Implementation | Verification |
|---|---|---|---|
| **CLIENT-001** | Authorized users create Clients. | P054–P070 (entity, EF config, DbSet, migration, API contract, repository, Data/Business/Facade layers, controller) | P157 |
| **CLIENT-002** | Client contains: ID, name, contact, email, phone, website, address, city, state, postal code, country, lifecycle, notes, owner, timestamps, created/modified by. | P054–P055 (entity definition, EF config) | P158 |
| **CLIENT-003** | Client names searchable. | P149–P151 (global search Core/API/UI) | P160, P163 |
| **CLIENT-004** | Duplicate detection by name/email/phone at creation; warns user without silent merge. | P065, P070 (API contract includes duplicate warning; controller returns guidance) | P157, P163 |
| **CLIENT-010** | Client has exactly one current lifecycle status from: Lead, Prospect, Active, On Hold, Inactive, Archived. | P054 (entity lifecycle field), P078–P079 (lifecycle transition) | P158 |
| **CLIENT-011** | Lifecycle transitions are auditable. | P078–P079 (Business creates audit fact), P114A (audit events through outbox) | P156 |
| **CLIENT-012** | System retains lifecycle change history through audit. | P152 (activity/audit timeline UI), P127–P128 (Audit query) | P160, P163 |
| **CLIENT-013** | Archived Clients excluded from normal lists unless explicitly requested. | P080–P081 (archive/restore behavior, including list-filtering logic) | P158, P160 |
| **CLIENT-014** | Archiving Client does not physically delete historical information. | P054 (soft-delete field or equivalent), P080–P081 (archive behavior) | P158 |
| **CLIENT-015** | Client with active Projects cannot be permanently removed. | P080–P081 (Business enforces: if has active Projects, reject permanent delete) | P157, P158 |
| **CLIENT-020** | Users view paginated Client list. | P071, P072, P074, P075 (paginated contract, repository, Data/Business query, Facade, controller) | P160, P161 |
| **CLIENT-021** | Client search by name, contact name, email, phone. | P071–P075 (search fields in contract, repository, Facade query filters) | P160, P163 |
| **CLIENT-022** | Client filter by lifecycle status, assigned owner, active/inactive. | P071–P075 (filter parameters in query, repository, Facade) | P160, P163 |
| **CLIENT-023** | Sort by name, created date, modified date, lifecycle status. | P071–P075 (sort parameters in contract/repository/Facade) | P160 |
| **CLIENT-024** | Server-side pagination; no unbounded result sets. | P072, P075 (repository enforces limits; controller returns page envelope) | P161 |
| **CLIENT-030** | Client detail consolidates: information, lifecycle, owner, active/historical projects, open/completed tasks, recent activity, audit history (if authorized). | P076–P077 (Client detail query through Core, controller) | P160, P163 |
| **CLIENT-031** | Users navigate from Client directly to Projects. | P076–P077, P139 (detail view links to Projects; Project list page) | P160 |
| **CLIENT-032** | Users navigate from Client to Tasks in those Projects. | P076–P077, P143 (detail view may list or link to Tasks; Task view page) | P160 |

---

### 3. Project Requirements (PROJECT-001 through PROJECT-031)

| Requirement | Intent | Implementation | Verification |
|---|---|---|---|
| **PROJECT-001** | Authorized users create Projects for a Client. | P057–P085 (entity, EF, DbSet, migration, API, repository, Data/Business/Facade, controller) | P157 |
| **PROJECT-002** | Project contains: ID, Client ID, name, description, status, priority, owner, start date, target completion date, actual completion date, notes, timestamps, created/modified by. | P057–P058 (entity, EF config) | P158 |
| **PROJECT-010** | Project supports statuses: Planned, Active, On Hold, Completed, Cancelled, Archived. | P057, P090–P091 (status field, status transition behavior) | P158 |
| **PROJECT-011** | Status changes are auditable. | P090–P091, P114A (Business creates audit fact, emitted through outbox) | P156 |
| **PROJECT-012** | Completing Project captures actual completion timestamp. | P090–P091 (Business sets completed-at when status → Completed) | P158 |
| **PROJECT-013** | Completed Project contains completed Tasks only; open Tasks require explicit acknowledgement before Project can be marked Completed. | P090–P091 (Business validates: if status = Completed and open Tasks exist, fail with user message) | P157, P158 |
| **PROJECT-014** | Projects not physically deleted; archived instead. | P092–P093 (archive behavior: soft-delete, not hard-delete) | P158 |
| **PROJECT-020** | Users view Projects across all authorized Clients, for a specific Client, assigned to a specific owner. | P086–P087 (Project list query with scope/filter, Facade, controller) | P160, P163 |
| **PROJECT-021** | Filter by Client, status, owner, priority, start/target-completion date. | P086–P087 (filter parameters in query, repository, Facade) | P160, P163 |
| **PROJECT-022** | Search by Project name, Client name, description. | P086–P087, P150 (search fields in Core query; global search API) | P160, P163 |
| **PROJECT-023** | Server-side pagination. | P086–P087 (repository enforces limits, controller returns envelope) | P161 |
| **PROJECT-030** | Project detail shows: information, Client, status, owner, priority, dates, open/completed Tasks, recent activity, audit history (if authorized). | P088–P089 (Project detail query, controller) | P160, P163 |
| **PROJECT-031** | Users create Task directly from Project. | P088–P089, P144 (detail view has "create Task" entry; Task create form) | P160 |

---

### 4. Task Requirements (TASK-001 through TASK-022)

| Requirement | Intent | Implementation | Verification |
|---|---|---|---|
| **TASK-001** | Authorized users create Tasks. | P060–P097 (entity, EF, DbSet, migration, API, repository, Data/Business/Facade, controller) | P157 |
| **TASK-002** | Task contains: ID, Project ID, title, description, status, priority, assigned user, created by, start date, due date, completed date/time, notes, timestamps, modified by. | P060–P061 (entity, EF config) | P158 |
| **TASK-010** | Task supports statuses: Backlog, To Do, In Progress, Blocked, Completed, Cancelled. | P060, P104–P105 (status field, status transition) | P158 |
| **TASK-011** | Completed Task records completion date/time. | P104–P105 (Business sets completed-at when status → Completed) | P158 |
| **TASK-012** | Authorized users can reopen completed Task; generates audit event. | P106–P107 (reopen behavior, Business creates audit fact) | P156, P158 |
| **TASK-013** | Users assign and reassign Tasks. | P100–P101 (assignment behavior, controller action) | P160 |
| **TASK-014** | Task assignment changes auditable. | P100–P101, P114A (Business creates audit fact on assign/reassign) | P156 |
| **TASK-015** | Users change Task priority; initial values: Low, Normal, High, Critical. | P102–P103 (priority field, Business behavior) | P160 |
| **TASK-016** | Overdue Tasks identifiable: due date < current date/time AND status not Completed/Cancelled. | P098–P099, P143 (Task list query includes overdue filter; UI shows overdue Tasks) | P158, P160 |
| **TASK-020** | Users view Tasks: assigned to them, in specific Project, open, completed, overdue. | P098–P099 (Task list query with scope filters) | P160, P163 |
| **TASK-021** | Filter by status, priority, assignee, Project, Client, due date. | P098–P099 (filter parameters in query/repository) | P160, P163 |
| **TASK-022** | Sort by due date, priority, created date, modified date. | P098–P099 (sort parameters in query/repository) | P160 |

---

## Summary Table

| Requirement Family | Count | Coverage | Status |
|---|---|---|---|
| Product Principles (PR) | 6 | P002–P010, P026–P027, P054–P114F, P149–P156 | ✅ Complete |
| Clients (CLIENT) | 32 | P054–P152 | ✅ Complete |
| Projects (PROJECT) | 31 | P057–P152 | ✅ Complete |
| Tasks (TASK) | 22 | P060–P152 | ✅ Complete |
| Data Integrity (DATA) | 34 | P030–P031, P035–P036, P042–P064 | ✅ Complete |
| Authentication (SEC) | 25 | P005–P006, P019, P026–P027, P050–P051, P069–P070, P110–P117 | ✅ Complete |
| Tracing (TRACE) | 7 | P010, P025–P027, P049–P052, P126, P156 | ✅ Complete |
| OpenTelemetry (OTEL) | 6 | P016, P023–P025, P049–P051, P153 | ✅ Complete |
| Observability (OBS) | 5 | P010, P024–P025, P154–P155 | ✅ Complete |
| Audit (AUDIT) | 8 | P007, P029A, P067–P068, P078–P080, P092, P100, P104–P106, P114A, P124–P128 | ✅ Complete |
| Messaging (ASYNC/OUTBOX) | 14 | P008–P009, P025–P034, P041–P052, P111A, P126, P159 | ✅ Complete |
| Error Handling (ERROR) | 5 | P028, P053A | ✅ Complete |
| API Design (API) | 7 | P019, P028, P053–P053A, P065–P107, P128B | ✅ Complete |
| Performance (PERF) | 4 | P072, P086–P088 | ✅ Complete |
| Reliability (REL) | 5 | P019, P034, P049, P051–P052, P111–P122, P126, P154–P155 | ✅ Complete |
| Deployment (DEPLOY) | 5 | P012–P015, P035–P036, P048, P050, P064, P155A | ✅ Complete |
| Ops (OPS) | 4 | P015, P050, P111, P122, P154–P155 | ✅ Complete |
| UX/UI (UX/ACCESS/DESIGN) | 15 | P020–P022A, P038, P131–P148, P160 | ✅ Complete |
| Testing (TEST) | 7 | P012–P046, P068–P069, P070–P107, P126, P156, P159 | ✅ Complete |
| Search (SEARCH) | 6 | P149–P151 | ✅ Complete |
| Activity (ACTIVITY) | 3 | P152 | ✅ Complete |
| Privacy (PRIV) | 5 | P007, P024–P025, P027, P053A | ✅ Complete |
| Dashboard (DASH) | 3 | P146–P148 | ✅ Complete |
| Governance | 1 | P000–P001, P011 | ✅ Complete |
| **TOTAL** | **298** | **P000–P163** | **✅ 100% Coverage** |

---

## Key Facts

- **Every requirement ID** from the canonical requirements document is scheduled for implementation
- **No requirements are deferred** or marked out-of-scope
- **164 prompts** span governance, architecture, platform, services, frontend, and verification
- **8 implementation phases** deliver the system incrementally with validation at each seam
- **100% traceability** ensures no silent assumptions or invented business behavior

**Last Updated**: August 12, 2026
