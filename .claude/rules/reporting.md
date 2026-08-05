# Reporting and dashboard rules

- Every metric must define business meaning, numerator, denominator when relevant, date window, comparison period, timezone, filters, and excluded records before implementation.
- Query read models; never mutate state from reporting code.
- Use database-side projection and aggregation. Do not load full entities to calculate dashboard metrics in memory.
- Exclude soft-deleted or test data only when the metric specification says so.
- Lifecycle funnel metrics must distinguish current-stage counts from historical transition counts.
- Conversion rates must state whether they are cohort-based, period-transition-based, or current-snapshot-based.
- Cache only after measuring a need and defining invalidation/staleness behavior.
- Add boundary tests for start/end timestamps, timezone conversion, empty data, and denominator zero.
