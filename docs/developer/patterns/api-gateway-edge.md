# API Gateway Edge

YARP is the only browser-facing backend edge.

## Responsibilities
- stable public route prefixes,
- service discovery/routing,
- approved authentication/session edge mechanics,
- correlation normalization,
- standard edge security headers/policies where appropriate.

## Non-responsibilities
- CRM business validation,
- EF/database access,
- Service Bus publication,
- domain authorization rules hidden only at the edge.

## Route principle
Public paths describe product resources, not internal implementation names. Internal host/port discovery stays configuration-driven.

## Validation
Gateway tests route to expected service; React tests/config contain only the gateway base URL.
