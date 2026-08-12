# Exception Handling and Error Shape

## Public contract
Use consistent ProblemDetails-style responses with safe error code/title/detail, validation errors and trace/support reference.

## Categories
- 400 validation,
- 401 unauthenticated,
- 403 forbidden,
- 404 missing,
- 409 concurrency/business conflict where appropriate,
- 5xx unexpected/internal.

## Logging
Unexpected exceptions are logged once at the handling boundary with trace context. Avoid logging the same stack at every layer.

## Functions
Do not translate unexpected Service Bus consumer failures into successful invocations. Function/platform retry must observe failure.

## Security
Never return stack traces, SQL statements, connection strings or broker internals to production callers.
