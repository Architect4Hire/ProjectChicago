---
name: add-aspire-resource
description: Add or wire a Project Chicago local distributed resource in Aspire: SQL Server database, Azure Service Bus entity/resource, per-service Azure Functions project, API host, gateway, cache, React/Vite app or an explicitly approved new bounded service. Uses narrow WithReference/WaitFor wiring and no hardcoded infrastructure.
---
# Add an Aspire resource

Everything local-development infrastructure needs should be modeled once in `ProjectChicago.AppHost` and injected into the workload that consumes it.

Read `.claude/rules/aspire.md`; also read the corresponding database/messaging/functions/frontend rule.

## 0. Verify the current API

Aspire integrations move quickly. Before adding package names/method calls, verify the **current official Aspire documentation** for the resource. Architectural invariants in this skill are stable; exact APIs/packages are not assumed forever.

## 1. Identify resource and consumers

Write down:

- resource type;
- owning service if any;
- exact workloads needing access;
- readiness/start ordering;
- local emulator/container vs external cloud dependency;
- whether production topology is already defined.

Grant the resource to the smallest consumer set.

## 2. SQL Server database

Default local model:

```text
one Aspire SQL Server resource
  -> <serviceA>db
  -> <serviceB>db
  -> ...
```

For a service DB:

1. add database to SQL Server resource;
2. reference it from that service API host if HTTP operations use it;
3. reference it from that service Functions project if triggers/outbox relay use it;
4. do not reference it from another bounded service/gateway/web;
5. register owning DbContext through current Aspire/EF Core SQL Server integration;
6. keep migrations in owning `.Core`.

Do not add Postgres/Npgsql resources/packages.

## 3. Azure Functions project

When a bounded service needs async triggers and lacks a `.Functions` project:

1. scaffold current supported .NET isolated Functions project;
2. reference its own `.Core`, Contracts, Shared only as needed;
3. add it to AppHost using current Aspire Azure Functions integration;
4. give it its service DB and Service Bus references;
5. apply `WaitFor`/readiness wiring for local dependencies;
6. do not add a hosted-worker substitute to the API host.

A new Functions project is not a new bounded service; it is an entry-point workload for the existing bounded context.

## 4. Service Bus resource/entity

- Use the current Aspire Azure Service Bus hosting/emulator integration where supported.
- Entity topology follows the project's approved convention.
- Publishers/relays get send access/configuration; consumer Function apps get receive access/configuration.
- API hosts should not receive bus credentials simply because they write outbox rows.
- Keep entity names/configuration centralized.
- Model consumer subscriptions explicitly so local development reflects deployed behavior as closely as practical.

If a real Azure Service Bus resource is required for a feature the emulator cannot represent, surface that as an environment decision rather than silently making local development cloud-dependent.

## 5. Cache

Add Redis/cache only for a measured/use-case-specific reason.

- reference only services that cache;
- cache policy belongs in Facade/application behavior, not AppHost;
- cache is not a source of truth;
- never cache security-sensitive CRM data without explicit TTL/invalidation/data-classification reasoning.

## 6. API host

For an existing service, AppHost should model the thin HTTP project and its owned dependencies.

For a **new service**, stop unless the service boundary is explicitly approved. If approved, scaffold all expected pieces together:

```text
ProjectChicago.<Service>
ProjectChicago.<Service>.Core
ProjectChicago.<Service>.Functions   # if async work is part of service; recommended baseline can be empty only if project standards allow
<service>db
Gateway route(s)
Tests
```

Do not create a shared database as a shortcut.

## 7. Gateway

- add/reference API host resources it routes to;
- no SQL/Service Bus references;
- no business logic;
- public route remains stable even if internal resource name changes.

## 8. React/Vite app

- add JavaScript/Vite app through current Aspire integration;
- use the project package-manager command;
- inject/reference gateway base URL only;
- do not give browser code internal service resource names.

## 9. Secrets/config

- use Aspire parameters, user secrets, Azure configuration/managed identity as chosen;
- never commit SQL passwords, Service Bus SharedAccessKey, storage AccountKey or access tokens;
- do not put production secrets in AppHost source.

## 10. Validate

- AppHost builds;
- Aspire dashboard shows expected resource graph;
- SQL databases become healthy;
- Functions start under local orchestration;
- Service Bus emulator/entity config is usable;
- gateway resolves service resource;
- React resolves gateway and nothing internal;
- restart behavior does not depend on manually chosen ports.

## Completion checklist

- [ ] Current official Aspire API verified.
- [ ] Resource declared once.
- [ ] Least-privilege consumer references.
- [ ] SQL Server, not PostgreSQL.
- [ ] Functions modeled as Functions projects, not background workers.
- [ ] No hardcoded host/port/credential.
- [ ] Public browser path remains gateway-only.
