---
paths:
  - "src/web/**/*.{ts,html,scss,css}"
---
# Frontend rules

- Use standalone components and lazy-loaded feature routes.
- Keep feature state in a feature facade/store. Components render state and emit intent.
- Use signals for synchronous view state and RxJS for HTTP/event streams.
- Use typed reactive forms. Centralize validation messages and server-error mapping.
- API access goes through generated clients or typed feature services; never call `HttpClient` directly from components.
- Every data screen must define loading, empty, error, stale, and permission-denied states.
- Tables require server-side paging, sorting, and filtering once the dataset can grow beyond a small bounded list.
- Use design tokens/CSS custom properties for palette, spacing, radius, typography, elevation, and lifecycle colors.
- Lifecycle stage color is supplemental; always show text or an icon so meaning is not color-only.
- Add accessible names to icon-only controls and preserve visible keyboard focus.
