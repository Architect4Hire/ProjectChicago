# ADR-0019 — Production SQL Hosting Topology

- **Status:** Proposed
- **Requirements:** DATA-030..034, DEPLOY-001..005, REL-001..005

## Context
SQL Server/Azure SQL compatibility and one database per service are fixed. The production Azure resource topology is not.

## Proposed decision
Choose an Azure SQL deployment model that preserves logical database ownership for each bounded service, supports backup/restore, private networking, managed identity where supported, operational monitoring and cost goals.

Candidates may include separate Azure SQL databases on shared or separate logical servers/pools. The decision is operational; it must not reintroduce cross-service table access.

## Open items
- database vs elastic-pool placement,
- region/HA/DR targets,
- backup retention,
- private endpoints/networking,
- migration identity/permissions,
- cost envelope.

## Validation
Architecture/operations review plus IaC proof before production.
