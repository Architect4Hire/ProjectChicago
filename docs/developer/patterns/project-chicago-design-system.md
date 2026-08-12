# Project Chicago Design System (PCDS)

PCDS is the local UI source of truth.

## Layering
- tokens: color, typography, spacing, radius, elevation, motion,
- recipes/primitives: shared button/input/card/table/dialog/status patterns,
- feature components: Client/Project/Task semantics built from primitives.

## Rule
If PCDS already has a component/recipe, feature code uses it rather than repeating a long Tailwind class list.

## Accessibility
Shared primitives should carry accessible labels/roles/focus/disabled/error behavior where possible. Feature code still owns semantic context.

## Theme
Light/dark and semantic states use tokens; feature code does not hard-code arbitrary palette values for equivalent concepts.

## Evolution
Because PCDS is copied into this repository, improvements are reviewed/versioned with application code. Avoid parallel design-system folders.
