---
name: add-component
description: Add or change Project Chicago React 19 UI using the PCDS design system, typed gateway API access, accessible interaction, Tailwind v4 recipe discipline, route/state integration and tests. Use for pages, forms, tables, cards, dialogs, navigation, CRM lifecycle UI or shared components.
---
# Add a React 19 component with PCDS

Work under `src/web/`. The client knows the gateway and PCDS; it does not know internal services or invent a second design system.

Read `.claude/rules/frontend.md` first. For backend-connected UI, also read `.claude/rules/gateway.md` and use `api-contract-checker` after implementation.

## 1. Classify what you are building

Decide whether the requested UI is:

- a **design-system primitive** used broadly;
- a **composed PCDS pattern** such as page header/loading/empty/error;
- a **feature component** owned by one CRM feature;
- a **feature page/route**;
- a **data/API hook/module** with no visual output.

Do not promote feature-specific UI into PCDS just because it uses Tailwind. Conversely, if the same semantic control/surface/variant is being copied, extend PCDS rather than duplicating it.

## 2. Inspect PCDS before writing markup

Use the **copied local PCDS source in this repository**. Start with `src/web/src/design-system/` and the web token/theme CSS; if the repository has placed the copy elsewhere, locate that actual source first. Do not consult upstream PCDS as a substitute for inspecting the checked-in Project Chicago version.

Look for existing:

- token for the semantic color/spacing/radius/elevation;
- recipe variant;
- primitive (`Button`, `Surface`, `Card`, `Field`, `Input`, `Stack`, `Cluster`, `Grid`, `Tabs`, etc.);
- composed state pattern;
- accessible dialog/menu/tab/form behavior.

Prefer composition over custom replacement.

## 3. Define the component contract

For reusable components:

- typed props;
- clear controlled/uncontrolled behavior where relevant;
- semantic event names (`onSave`, `onStatusChange`), not DOM implementation leakage;
- no `any`;
- sensible defaults only when behavior is unambiguous.

For feature pages, identify URL/route params, query/filter state and navigation behavior before building the visual tree.

## 4. Data access through gateway only

If data-backed:

1. locate/create the feature API module;
2. use the shared gateway client/base URL;
3. define TypeScript types that mirror the **public API contract**;
4. centralize ProblemDetails/error mapping;
5. use the project-wide ASP.NET Core Identity auth/session behavior through the shared gateway client rather than hand-building auth headers in each feature;
6. expose cancellation/abort where long navigation/search operations benefit;
7. keep network code out of low-level presentational components.

Forbidden:

- `fetch("http://customers:...")`;
- service resource names/ports in browser code;
- Function HTTP endpoints as a UI backend;
- duplicating auth headers in every component.

## 5. Build visual hierarchy with PCDS

Use layout primitives before hand-assembling repeated utility strings.

Typical order:

```text
Page shell
 -> PageHeader pattern
 -> Surface/Card/Grid/Stack composition
 -> Field/Input/Button/Tabs primitives
 -> feature-specific content/layout
```

If a Tailwind class string represents a shared semantic pattern, put it in the design-system recipe layer and consume the typed variant.

## 6. State design is part of the component

Implement all applicable states deliberately:

- initial/loading;
- empty;
- populated;
- validation failure;
- server/domain error;
- saving/submitting;
- success feedback;
- permission/disabled behavior;
- stale/conflict state where CRM concurrency can surface.

Do not leave an empty `<div>` or spinner-only forever state as the entire error model.

## 7. React behavior

- Derive display values during render where possible instead of synchronizing them through `useEffect`.
- Keep effects for real external synchronization/subscriptions.
- Keep feature state as local as practical; do not introduce a global state library for one screen.
- Avoid premature `useMemo`/`useCallback` decoration unless identity/performance requirements justify it.
- Use stable keys derived from domain IDs, not array indexes for mutable CRM lists.
- Handle async race/cancellation for searches/filtering where stale responses could overwrite newer state.

## 8. Forms

For a CRM form:

- use PCDS fields/inputs;
- show clear labels and help/error text;
- map server validation errors to fields when possible;
- preserve typed input/output model;
- guard duplicate submit;
- make saving state visible;
- focus or announce the relevant error/success state appropriately;
- decide unsaved-change protection explicitly rather than globally.

## 9. Accessibility pass

Verify with keyboard only:

- all controls reachable/operable;
- visible focus;
- labels associated;
- dialog opens with intentional focus, traps focus and Escape closes when appropriate;
- tabs use established PCDS keyboard semantics;
- status/loading changes are announced when needed;
- no status depends on color alone;
- reduced-motion preference respected.

Use semantic elements before adding ARIA.

## 10. Responsive and theme pass

Check at least:

- narrow/mobile width;
- common desktop width;
- light mode;
- dark mode;
- long names/text;
- empty values;
- dense CRM table/list content.

Do not fix one mode by hardcoding color classes that bypass semantic tokens.

## 11. Tests / commands

Run the repository's configured equivalents, normally:

```bash
pnpm lint
pnpm build
```

Run focused component/unit/e2e tests if configured.

Test behavior that matters:

- renders critical state;
- user action calls typed feature API with correct public contract;
- validation/error state;
- loading/disabled behavior;
- key keyboard interaction for custom composite widgets.

## 12. Contract review

If API shape or route changed, run `api-contract-checker` to compare controller/gateway contract with TypeScript models and client calls.

## Completion checklist

- [ ] Component classified correctly (PCDS primitive/pattern vs feature UI).
- [ ] Local copied PCDS source inspected before adding or changing styling.
- [ ] Existing PCDS primitive/recipe reused where applicable.
- [ ] No repeated semantic Tailwind bundle introduced in feature code.
- [ ] Public typed gateway client used.
- [ ] No internal service/Function URL exposed.
- [ ] Loading/empty/error/success states handled.
- [ ] Keyboard/accessibility checked.
- [ ] Light/dark and responsive layout checked.
- [ ] `pnpm lint` and `pnpm build` pass (or chosen repo equivalents).
