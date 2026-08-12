---
paths:
  - "src/ProjectChicago.Gateway/**"
---
# YARP gateway rules

The gateway is Project Chicago's **only** browser-facing HTTP edge. The React application knows the gateway, not the service topology or Function Apps.

## Responsibilities

- Route stable public paths to owning service API hosts.
- Resolve internal destinations from Aspire/configuration/resource discovery; never hardcode local ports.
- Apply truly edge-wide concerns such as correlation normalization, common security headers, request size policy, and coarse authentication plumbing where appropriate.
- Emit edge telemetry that can be correlated with service/event traces.

## Boundaries

- Do not put CRM lifecycle/business decisions in YARP transforms/middleware.
- Do not connect the gateway to SQL Server or Service Bus.
- Do not have the gateway call Functions as a shortcut for commands.
- Do not expose an internal service hostname/port to React.
- A public route is a contract. Renaming an internal service/resource must not force a browser route change.

## Authentication and authorization

- ASP.NET Core Identity is the application identity framework. The gateway participates only in the HTTP/authentication flow selected by the solution; it does not become a custom user store or CRM authorization engine.
- Gateway can enforce edge-wide authenticated-user requirements and propagate trusted authenticated context.
- Browser login/account calls still enter through YARP and route to the eventual service that owns ASP.NET Core Identity.
- The owning service performs resource/action authorization; do not rely only on an edge check.
- Do not trust arbitrary client-supplied user/tenant/role headers as identity. Derive trusted identity from validated ASP.NET Core authentication context.
- Cookie vs bearer/session transport is intentionally unresolved and requires an explicit security decision.

## Routing changes

When adding a route:

1. identify owning service;
2. define stable public path;
3. route through service resource discovery/config;
4. preserve correlation headers/context;
5. update API/client contract tests;
6. never use a direct Function endpoint as the browser API.
