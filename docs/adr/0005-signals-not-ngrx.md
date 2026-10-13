# Signals + services for state management, not NgRx

The spec leaves state management open ("NgRx, signals, services"). We chose Angular signals combined with plain injectable services, scoped per feature module (claims list, claim detail, FNOL form) — no NgRx.

Nothing in this app has state that's genuinely shared across unrelated features, has complex derived-state graphs, or needs time-travel debugging — it's mostly "fetch a list, fetch a detail, run a command, refetch," scoped to one screen at a time. NgRx's actions/reducers/effects/selectors ceremony solves a problem (deeply shared, cross-cutting state) this app doesn't have, and would add boilerplate without improving correctness. Signals are also Angular's own current idiomatic direction (18+). The FNOL multi-step form's internal state is handled by Reactive Forms (`FormGroup` per step), independent of this decision.
