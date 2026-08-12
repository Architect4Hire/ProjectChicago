# ADR-0020 — Infrastructure as Code and Deployment Ownership

- **Status:** Proposed
- **Requirements:** DEPLOY-001..005, OPS-001..004, SEC-015..016

## Context
Aspire describes local orchestration but production resources, identities, networking, alerts and Flex Consumption deployments need a repeatable infrastructure lifecycle.

## Proposed decision
Select a single supported IaC approach for Azure resources and define ownership between application and platform deployment pipelines. The IaC must provision/configure, at minimum:

- Azure Functions Flex Consumption resources,
- Azure SQL topology,
- Azure Service Bus topology,
- managed identities and RBAC,
- Key Vault/secrets references,
- Azure Monitor/Application Insights,
- dashboards/alerts,
- networking required by the approved environment.

Do not store secrets in source or parameter defaults.

## Alternatives to evaluate
Bicep and Terraform are both viable candidates; the project should deliberately choose one instead of mixing ad hoc scripts.

## Validation
Production-like non-prod deployment from a clean environment, drift review and least-privilege validation.
