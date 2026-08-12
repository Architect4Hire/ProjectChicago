# Integration Event Contracts

`ProjectChicago.Contracts` is the shared wire boundary, not a shared domain model.

## Envelope metadata
At minimum:
- Event/MessageId,
- contract type,
- contract version,
- occurred-at UTC,
- CorrelationId,
- CausationId,
- approved actor metadata.

## Contract rules
- immutable/simple serialization-friendly records,
- explicit version,
- no EF/navigation types,
- no secrets,
- avoid unnecessary PII,
- status values serialized through a deliberately stable representation.

## Evolution
Consumers must know what versions they support. Breaking semantic changes require a new contract version rather than silently changing old payload meaning.

## Testing
Round-trip known contracts, verify metadata, reject unsupported version according to policy, and test old-version compatibility when retained.
