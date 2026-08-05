---
name: add-dashboard-metric
description: >
  Add one CRM KPI or chart series with an explicit business definition, SQL-side aggregation, stable date
  and timezone semantics, authorization, response contract, caching/performance considerations, Angular
  presentation, accessibility, and boundary-focused tests. Implement each layer as separate microsteps.
---
# Add a dashboard metric

A metric is a business contract, not just a query. Define it before coding so two screens cannot calculate
the same label differently.

## Discovery gate

Before changing code, discover the actual solution/project paths, namespaces, target frameworks, package versions, AppHost resource names, SQLDB connection name, DbContext, migrations assembly, test conventions, and feature location. Never treat example names as repository facts. Stop without editing when a required value cannot be proven. Aspire is required and is the supported source of local SQLDB connection information.

## Required metric specification

Document:

- Metric name and one-sentence business meaning.
- Owning/reporting module.
- Grain: account, contact, opportunity, transition, activity, or snapshot.
- Numerator and denominator.
- Snapshot, cohort, flow, or transition basis.
- Date field used and why.
- Inclusive/exclusive date boundaries.
- Business timezone and UTC conversion rule.
- Current and comparison windows.
- Filters, security scope, exclusions, and deduplication.
- Zero denominator and no-data behavior.
- Currency/unit and rounding.
- Drill-down relationship, if any.
- Response contract and freshness/cache expectation.

Example ambiguity to resolve: “conversion rate” may mean customers currently in Decision divided by all
current customers, or unique customers who transitioned to Decision divided by those who entered the cohort.
These are not interchangeable.

## Procedure by microstep

1. **Definition only**: add/test metric specification; no query.
2. **Query only**: implement SQL-side aggregation in Reporting; no endpoint.
3. **Persistence test only**: verify boundaries, filters, timezone, duplicates, zero data.
4. **Endpoint only**: expose typed contract with policy/OpenAPI.
5. **Generated client only**: regenerate TypeScript.
6. **Facade/state only**: call operation and represent loading/empty/error.
7. **Presentation only**: KPI card or chart series using design tokens.
8. **UI tests only**: period label, empty state, accessible equivalent.

## Query rules

- Project/aggregate in SQL; do not load entities and aggregate in memory.
- Use transition history for flow/conversion metrics and current state for snapshots.
- Apply security/organization scope before aggregation.
- Use stable distinct keys when joins can duplicate facts.
- Parameterize date windows.
- Prefer one query per metric family rather than N+1 per card.
- Inspect query plan for large tables and add an index only with evidence.
- Cache only when freshness requirements permit; cache keys must include scope, filters, and time window.

## API contract

Return enough context to prevent mislabeling:

```json
{
  "metric": "lifecycleConversionRate",
  "value": 0.087,
  "unit": "ratio",
  "periodStartUtc": "...",
  "periodEndUtc": "...",
  "comparisonValue": 0.081,
  "comparisonPeriodStartUtc": "...",
  "comparisonPeriodEndUtc": "...",
  "definitionVersion": "v1"
}
```

Use the project's exact contract conventions. Avoid returning a naked number.

## Presentation and accessibility

- State period and comparison meaning in text.
- Do not imply causation from correlation.
- Charts need accessible summaries/data tables.
- Use semantic tokens; do not encode positive/negative solely by color.
- Distinguish “0” from “no data.”
- Show rounding in UI but preserve adequate precision in the API.

## Tests

- Exact lower/upper date boundary.
- Business timezone crossing UTC date.
- Duplicate-producing joins.
- Empty data and zero denominator.
- Authorization scope excludes other owners/organizations.
- Disabled/renamed lifecycle stages retain historical semantics.
- Comparison window calculation.
- Currency and rounding.
- UI announces metric, value, period, and trend without color dependence.

## Completion checklist

- [ ] Definition is unambiguous and versioned/documented.
- [ ] Query aggregates in SQL and applies security scope.
- [ ] Timezone and boundary semantics are tested.
- [ ] API returns context, not a naked number.
- [ ] 0 and no-data are distinct.
- [ ] UI is accessible and period-aware.
- [ ] Each layer was delivered in a separate microstep.
