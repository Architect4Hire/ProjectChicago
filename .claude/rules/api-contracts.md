# API contract rules

- OpenAPI is the canonical browser/API contract. Every endpoint has a stable operation ID.
- Request and response DTOs are immutable, typed, and separate from EF entities.
- Use ProblemDetails consistently for errors; validation errors use a predictable property-to-messages shape.
- Use explicit nullability. Do not use null to represent omitted, unknown, empty, and unauthorized simultaneously.
- Use stable string enum serialization when enums cross HTTP boundaries, or use explicit lookup DTOs for administrator-configurable values.
- Collection endpoints use a shared pagination envelope and explicit sort/filter defaults.
- Date/time values are ISO-8601 UTC instants unless a field is explicitly a local date.
- Breaking changes require an explicit decision and coordinated Angular client regeneration.
- Generated Angular client code is never hand-edited.
