---
name: add-angular-feature
description: >
  Add one routed Angular CRM feature or one reusable UI capability using standalone components, typed
  generated API clients, signals for local view state, RxJS for async composition, reactive forms,
  centralized design tokens, accessibility, responsive behavior, and focused unit/Playwright tests.
---
# Add an Angular feature

Build one user-visible capability at a time. A feature may be a routed page, one page section, one dialog,
one form, or one reusable component. Do not implement an entire CRM module in one invocation.

## Discovery gate

Before changing code, discover the actual solution/project paths, namespaces, target frameworks, package versions, AppHost resource names, SQLDB connection name, DbContext, migrations assembly, test conventions, and feature location. Never treat example names as repository facts. Stop without editing when a required value cannot be proven. Aspire is required and is the supported source of local SQLDB connection information.

## Inspect first

Read root `CLAUDE.md`, `.claude/rules/frontend.md`, `.claude/rules/accessibility.md`, and one comparable
feature. Determine:

- Angular version and package manager.
- Standalone component and routing conventions.
- State conventions: signals, stores, facades, RxJS.
- Generated API client location and regeneration command.
- Shared design system, tokens, typography, icons, table, dialog, toast, and form controls.
- Authentication/authorization guards.
- Unit-test runner and Playwright conventions.

Do not add NgRx, a component library, CSS framework, chart library, or form abstraction that is not already
used without a separate decision.

## Target shape

```text
src/web/src/app/features/<feature>/
├── <feature>.routes.ts
├── pages/<page>/
│   ├── <page>.component.ts
│   ├── <page>.component.html
│   └── <page>.component.scss
├── components/<component>/
├── data-access/
│   ├── <feature>.facade.ts
│   ├── <feature>.models.ts       # UI-only models, never copies of generated contracts without reason
│   └── <feature>.mappers.ts
└── testing/
```

Reuse repository layout rather than moving existing code solely to match this example.

## Responsibilities

- **Page/container**: route parameters, permission state, facade orchestration, layout.
- **Presentation component**: inputs/outputs, rendering, user interaction; no direct API calls.
- **Facade/data-access service**: generated-client calls, async state, request cancellation, error mapping.
- **Generated API client**: transport only; never hand-edit.
- **Reactive form**: local validation, dirty state, submit state, server error association.
- **Design system**: tokens and shared controls. Feature SCSS may compose them but may not duplicate them.

## Procedure

### 1. Specify the UX contract

State:

- Route or host page.
- Allowed roles/policies.
- Primary user task.
- API operations required.
- Initial loading, empty, populated, stale, error, forbidden, and offline/retry states.
- Desktop, tablet, and narrow-mobile behavior.
- Keyboard path and focus behavior.
- Whether the UI changes route/query-string state.
- Analytics/audit interaction expectations, if any.

### 2. Verify the API contract

- Use generated client types and operations.
- If an operation is absent, stop and request/perform the backend endpoint microstep first.
- Do not create handwritten interfaces that silently drift from OpenAPI.
- UI models are allowed only when they add display-specific state or combine contracts; map explicitly.
- Treat server Problem Details as the source for business/authorization failures.

### 3. Add routing or feature registration

- Lazy load routed features.
- Preserve deep links and browser refresh behavior.
- Put stable filter/sort/page state in query parameters when it should be shareable.
- Use route guards for coarse visibility, while remembering the API remains authoritative.
- Add titles/breadcrumb metadata through the repository-standard mechanism.

### 4. Implement async state

Use signals for synchronous view state and RxJS for transport/event streams unless the repository has a
stronger established pattern.

Represent at least:

```ts
interface LoadState<T> {
  status: 'idle' | 'loading' | 'success' | 'empty' | 'error';
  data: T | null;
  error: UiProblem | null;
}
```

- Cancel stale requests for search/filter changes with `switchMap` or equivalent.
- Debounce free-text search.
- Avoid nested subscriptions.
- Use `takeUntilDestroyed` for imperative subscriptions.
- Prevent duplicate submits.
- Do not keep server-authoritative lifecycle/opportunity state only in browser memory after a mutation;
  update from the mutation response or reload.

### 5. Build presentation components

- Keep API access out of presentation components.
- Use explicit typed inputs/outputs.
- Use `ChangeDetectionStrategy.OnPush` if not already the standalone default/convention.
- Track list rows by stable ID.
- Avoid methods with nontrivial computation in templates; use computed signals.
- Do not hide permission failures solely with CSS; remove/disable actions based on authorization data and
  rely on the API for enforcement.

### 6. Build forms

- Use reactive forms.
- Align client lengths/ranges with OpenAPI/server rules without duplicating business rules.
- Normalize only when semantics are clear; do not silently trim meaningful note content.
- Show field errors after touch/submit according to existing convention.
- Map field-level server errors to controls and preserve general Problem Details for the form summary.
- On successful create/update, use the server response as canonical state.
- Warn about unsaved changes only when the feature convention supports it and the form is actually dirty.

### 7. Apply the CRM design system

Use centralized CSS variables/tokens from the project. Do not hardcode lifecycle colors in feature files.
Use semantic tokens such as:

```css
var(--color-surface)
var(--color-text-primary)
var(--color-border)
var(--color-action-primary)
var(--lifecycle-awareness)
var(--lifecycle-interest)
var(--lifecycle-consideration)
var(--lifecycle-decision)
var(--lifecycle-onboarding)
var(--lifecycle-loyalty)
var(--lifecycle-advocacy)
```

Color must never be the only meaning. Pair stage/risk colors with text, icon, pattern, or accessible label.
Do not invent colors for states that already have semantic tokens.

### 8. Accessibility

- Prefer native elements over role-heavy divs.
- Ensure every input has a programmatic label.
- Keep visible focus indicators.
- Maintain logical heading order.
- Announce async save/load errors and success when needed.
- Move focus into dialogs and restore it on close.
- Support Escape only when it does not discard work without warning.
- Tables need proper headers; responsive card transformations must retain labels.
- Charts require a text/table equivalent or accessible summary.
- Test at 200% zoom and narrow viewport without two-dimensional scrolling except true data grids.

### 9. CRM-specific interaction rules

- Lifecycle transitions require a confirmation only when consequential; display current and destination
  stages by label, not color alone.
- Timeline entries are chronological facts. Preserve timezone display rules and distinguish system events
  from user-authored activities.
- Risk and health indicators must explain why they are shown when the API provides reasons.
- Dashboard metric labels must state period/comparison context.
- Destructive actions must name the record and consequence.

### 10. Test

**Component/facade tests**
- Loading -> success and loading -> empty.
- Transport/Problem Details failure.
- Permission-hidden/disabled state.
- Form validation and server error mapping.
- Duplicate-submit prevention.
- Route/filter parameter synchronization.
- Keyboard interaction for custom controls.

**Playwright** for the core journey when requested or materially changed:
- Navigate by route.
- Complete the primary task.
- Verify persisted result after reload.
- Verify forbidden role behavior.
- Verify one narrow viewport.

Prefer user-visible selectors: role, label, accessible name. Avoid brittle CSS selectors.

### 11. Verify

Use repository scripts, typically:

```bash
npm ci
npm run lint
npm test -- --watch=false
npm run build
```

Run Playwright only for the scoped workflow. Report warnings separately; do not fix unrelated lint debt in
this microstep.

## Common failure modes

- Direct API calls from many components.
- Handwritten copies of generated request/response types.
- One boolean pair (`isLoading`, `hasError`) representing impossible combinations.
- Subscribing inside subscribing.
- Hardcoded stage colors.
- Optimistically changing lifecycle state without handling 409 conflicts.
- Rendering large lists without server pagination.
- Using placeholders as labels.
- Creating a generic component abstraction before two real use cases exist.

## Completion checklist

- [ ] One user outcome and route/host are defined.
- [ ] Generated API client is used unchanged.
- [ ] Async states include loading, empty, error, and retry behavior.
- [ ] Components and data access are separated.
- [ ] Form/server validation mapping is complete.
- [ ] Design tokens and color-independent meaning are used.
- [ ] Keyboard, focus, labels, zoom, and responsive behavior are covered.
- [ ] Focused unit tests pass.
- [ ] Production build passes.
- [ ] Playwright coverage is added only where justified.
