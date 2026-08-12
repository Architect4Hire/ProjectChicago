# ADR-0012 — Local Project Chicago Design System

- **Status:** Accepted
- **Requirements:** DESIGN-001..004, ACCESS-001..005

## Context
Project Chicago requires a consistent production UI while avoiding repeated Tailwind class bundles and design drift.

## Decision
Copy PCDS into the repository and treat the local `src/web/src/design-system` source as authoritative for reusable tokens, recipes and primitives. Feature code consumes PCDS rather than rebuilding equivalent styles/components.

React 19 + TypeScript + Vite + Tailwind CSS v4 is the client baseline.

## Consequences
- Design-system changes are versioned with application code.
- Accessibility belongs in shared primitives where possible.
- Feature components should remain semantic/business-oriented.
- Light/dark theming uses shared tokens.

## Alternatives considered
- Repeated raw Tailwind recipes per feature: rejected.
- Runtime dependency on an external PCDS repository: rejected for the initial model.
- Another component framework layered over PCDS: not selected.

## Validation
Frontend review scans for duplicated recipes/tokens; accessibility and production build tests run at release gate.
