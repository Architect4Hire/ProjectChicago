---
paths:
  - "src/web/**"
---
# React 19 + PCDS frontend rules

Project Chicago's UI is a client-side React 19 application. PCDS is the design-system authority.

## Stack

- React 19
- TypeScript with strict type checking
- Vite
- Tailwind CSS v4
- React Router as established by the project/PCDS baseline
- the **local copied PCDS source** checked into Project Chicago

Do not introduce Next.js, SSR/server components, Angular, another Tailwind component kit, or a parallel design system without an explicit decision.

## PCDS architecture

Preserve the design-system layering documented by PCDS:

```text
Primitive tokens
  -> Semantic tokens
    -> Tailwind recipes / typed variants
      -> React primitives
        -> composed patterns
          -> feature pages
```

Current PCDS primitives include patterns such as:

- `Button`
- `Surface`
- `Card`
- `Field`
- `Input`
- `Stack`
- `Cluster`
- `Grid`
- `Tabs`

The copied PCDS source is expected under `src/web/src/design-system/` with its tokens/theme CSS in the web application. Treat the checked-in files as authoritative. Before creating UI, inspect the local `recipes`, barrel exports, primitives, patterns, token definitions and theme mechanism. If the actual copied path differs, discover it and update references rather than creating a second design-system folder.

## Styling discipline

- If a Tailwind class bundle appears repeatedly or represents a semantic visual role, move/use it through the PCDS recipe/token layer rather than copying it across components.
- Feature pages may use Tailwind for feature-specific layout, but common controls/surfaces/statuses belong in the design system.
- Use PCDS `cx()`/merge utilities and typed variants for conditional styles.
- Never hardcode a new brand color in a feature component when a semantic token exists or should exist.
- Support light and dark mode through the PCDS theme mechanism.

## Feature structure

Recommended direction:

```text
src/
├── app/               # routing/providers/app shell
├── api/               # gateway client, generated/typed contracts, errors
├── design-system/     # copied PCDS source; authoritative local design system
└── features/
    └── <feature>/
        ├── components/
        ├── hooks/
        ├── api/
        ├── models/
        └── pages/
```

Do not create one global `components/` dumping ground for feature-specific UI.

## Data access

- Browser talks to exactly one backend origin/base URL: YARP gateway. Authentication/account traffic follows the same rule.
- Components do not know service names/ports.
- Centralize HTTP concerns in typed API/client modules: base URL, auth token behavior, ProblemDetails/error mapping, correlation headers if client-generated/returned, cancellation.
- Avoid raw `fetch` scattered through components. A feature hook/API module may call the shared gateway client.
- No `any` for API contracts. Keep frontend types synchronized with public gateway/API contracts.

## Authentication UI

- ASP.NET Core Identity is the server-side identity framework. React owns presentation/state only; it does not implement password hashing, token minting, lockout or account-security rules.
- Login, logout, registration/recovery/profile-security screens must use local PCDS fields, buttons, validation/error surfaces and layout primitives.
- Do not choose cookie vs bearer-token storage silently. Follow the security/auth transport selected by the solution.
- Never persist passwords, refresh tokens or other long-lived secrets in browser storage.
- All account HTTP calls go through the YARP gateway typed API client.

## React 19 behavior

- Prefer straightforward React state/data-flow before adding state libraries.
- Keep side effects explicit and scoped; do not use effects to derive values that can be calculated during render.
- Make loading, empty, error and success states first-class using PCDS patterns.
- Avoid premature memoization. Use memoization when profiling or stable identity requirements justify it.
- Use route-level/code splitting where it improves the application without obscuring simple flows.

## Accessibility

Required for every feature:

- semantic HTML before ARIA;
- labels and descriptions for inputs;
- visible keyboard focus;
- keyboard-operable interactive controls;
- correct tab roles/arrow-key behavior when using tabs;
- dialog focus containment, initial focus and Escape behavior;
- status announcements for asynchronous state when needed;
- reduced-motion support;
- no color-only status meaning.

Use PCDS primitives first because accessibility behavior should be centralized there.

## Forms

- Use PCDS `Field`/`Input`/control primitives.
- Server validation errors map to the relevant field or form-level error surface.
- Disable/guard duplicate submit appropriately without making the interface appear frozen.
- Preserve unsaved-change behavior only when the feature warrants it; don't add global complexity by default.

## Tests / verification

For a UI change:

- run `pnpm lint` and `pnpm build` (or the repo's chosen equivalent if package strategy changes);
- run focused tests if configured;
- check desktop and narrow/mobile layout;
- check light and dark mode;
- keyboard through the interaction;
- verify loading, empty, error and populated states;
- inspect `/design-system` or the current design-system catalog when adding/changing a shared primitive.
