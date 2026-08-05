---
name: trace-request
description: >
  Trace one CRM request through Angular, MVC Controller, Facade validation/cache, Business translation/rules,
  Data EF Core/SQL Server work, audit/timeline effects, and telemetry. Uses evidence and does not modify code.
---
# Trace One Request

## Discovery gate

Before changing code, discover the actual solution/project paths, namespaces, target frameworks, package versions, AppHost resource names, SQLDB connection name, DbContext, migrations assembly, test conventions, and feature location. Never treat example names as repository facts. Stop without editing when a required value cannot be proven. Aspire is required and is the supported source of local SQLDB connection information.

## Path

```text
Angular -> HTTP/OpenAPI client -> Controller -> Facade -> Business -> Data -> SQL Server
                                      |          |          |          |
                                   HTTP map   validate/   rules/map  EF/SQL/tx
                                              cache
```

## Procedure

1. Identify route, method, controller, action name, and generated-client method.
2. Confirm controller dependencies contain Facade only.
3. Trace API request -> Facade request mapping and HTTP context values.
4. Trace Facade validation, record authorization, cache key/scope/TTL, hit/miss, and invalidation behavior.
5. Trace Facade -> Business request mapping and Business outcome mapping.
6. Trace Business invariants, lifecycle decisions, concurrency expectations, audit/timeline facts, and Data request translation.
7. Trace Data query/command, SQL projection, transaction, concurrency write, and provider-error translation.
8. Confirm Data result -> Business -> Facade -> API response translations.
9. Correlate logs/traces using correlation ID and inspect SQL Server evidence when available.
10. Report the first layer where observed behavior diverges from expected behavior.

## Boundary violations to flag

- Controller calls Business/Data or accesses cache/EF.
- Facade calls Data/EF.
- Business uses HTTP/cache provider/EF.
- Data references upper layers.
- One shared DTO passes through multiple seams.
- Cache key leaks data across user/tenant/authorization boundaries.
- Failed transaction leaves success audit/timeline evidence.

Do not edit code. Provide evidence, likely cause, smallest repair microstep, and verification command.
