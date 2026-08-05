---
name: update-angular-api-client
description: >
  Regenerate the Angular TypeScript API client from the repository's OpenAPI document after a backend
  contract change, verify deterministic output, summarize contract drift, compile the frontend, and stop
  before adapting handwritten UI code.
---
# Update the Angular API client

Generated transport code is a build artifact with a reproducible source: the API OpenAPI document. Never
hand-edit it.

## Discovery gate

Before changing code, discover the actual solution/project paths, namespaces, target frameworks, package versions, AppHost resource names, SQLDB connection name, DbContext, migrations assembly, test conventions, and feature location. Never treat example names as repository facts. Stop without editing when a required value cannot be proven. Aspire is required and is the supported source of local SQLDB connection information.

## Preconditions

- Backend contract change is complete and builds.
- OpenAPI generation method is known and deterministic.
- Working tree changes in generated-client folders are understood.
- Package lock is present.

## Procedure

1. Read the repository generation script/config and generated-client README/header.
2. Build the API.
3. Produce the OpenAPI document using the checked-in command; do not scrape a manually started local URL
   unless that is the repository's canonical method.
4. Save/compare the document if the repository tracks it.
5. Run the package script for generation.
6. Do not edit generated output.
7. Review the diff and classify:
   - added/removed operations;
   - path/method or operation-ID changes;
   - request/response property changes;
   - required/optional/nullability changes;
   - enum changes;
   - generated method-name changes;
   - broad formatting-only churn suggesting nondeterministic tool/version drift.
8. Run TypeScript/Angular production build.
9. Stop before changing facades, stores, components, or tests; those are separate microsteps.

## Guardrails

- Pin generator/package versions through the repository's package management.
- Do not upgrade the generator during a feature regeneration.
- Do not add custom handwritten methods inside generated files.
- Put auth headers, base URL, correlation IDs, and error normalization in supported interceptors/config, not
  generated code.
- A removed or renamed operation is a breaking change even if TypeScript currently has no caller.
- Unexpected changes outside the intended operation must be explained before acceptance.

## Verification

Typical commands, adjusted to repository scripts:

```bash
npm ci
npm run api:generate
git diff -- src/web/src/app/api
npm run build
```

Run the generator twice when determinism is in doubt; the second run should produce no diff.

## Completion checklist

- [ ] OpenAPI source came from the current backend build.
- [ ] Canonical generation script and pinned version were used.
- [ ] Generated files were not hand-edited.
- [ ] Contract changes are summarized precisely.
- [ ] Unexpected broad churn is absent or explained.
- [ ] Angular build passes.
- [ ] Handwritten caller adaptation is deferred.
