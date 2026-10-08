# 05 — FNOL intake: frontend

**What to build:** the actual end-user experience for logging and browsing claims — the dashboard and the multi-step FNOL wizard — wired to the now-stable API from Ticket 04.

**Blocked by:** 02, 04

**Status:** done

- [x] The claims dashboard shows a paginated, filterable, searchable table matching the columns and colour-coded status badges specified in the FRS, with a "Log New Claim" action.
- [x] The FNOL wizard has at least three independently-validated steps: policy & loss details (with policy typeahead search), claimant & party details (at least one Claimant required before advancing), and a review summary before submission.
- [x] Submitting the wizard creates the claim via the Ticket 04 API, shows a success banner with the generated claim number, and navigates to claim detail.
- [x] Claim detail shows the persistent header (claim number, status, policy, loss date, cause, handler) plus an Overview tab (loss event, parties, risk objects) and an Audit Log tab (reverse-chronological, showing at least the creation event).
- [x] Inline validation messages and API-error snackbars are used consistently with the patterns established in Ticket 02.
- [x] Loading indicators appear during every async step of the wizard and detail screen.

## Comments

Implemented the claims dashboard (`claims-dashboard.component.*`), the three-step FNOL wizard
(`fnol-wizard.component.*`, routed at `/claims/new` — registered *before* `/claims/:id` in
`app.routes.ts` so the `:id` segment doesn't swallow `new`), and claim detail
(`claim-detail.component.*`), plus the typed service seam: `ClaimsService`/`PoliciesService`/
`ReferenceDataService` (`core/claims|policies|reference-data`) and the shared models
(`shared/models/*.model.ts`) mirroring Ticket 04's exact `CreateClaimRequest` contract
(nested `lossEvent`, `causeOfLossCodeId` as a Guid, enum fields as JSON strings) and Ticket 03's
`/api/reference`/`/api/policies` contracts. `ADR-0005` honoured throughout: no NgRx; the wizard's
own state is Reactive Forms (`FormGroup`/`FormArray` per step), dashboard/detail use signals +
plain injectable services. Frontend Testing Decision honoured: specs fake only the HTTP call
(`HttpClientTestingModule`) and drive components through their real services/forms.

**Claim detail tab structure** (read this before building Tickets 06/07/08/10 on top of it):
`claim-detail.component.html` is a `mat-tab-group` with four tabs in this fixed order —
0. **Overview** — loss event, parties, risk objects. Fully built, real data, not a placeholder.
1. **Reserves** — placeholder only; `claim.reserves` is always `[]` until Ticket 07/08 give it a
   real DTO. Replace the placeholder `<p>` block with real reserve summary cards + a transaction
   history table once that shape exists; keep the tab index/position.
2. **Documents** — placeholder only; `claim.documents` is always `[]` until Ticket 10. Replace
   with a real document list + upload control once that DTO exists; keep the tab index/position.
3. **Audit Log** — real data from `GET /api/claims/{id}/audit`, reverse-chronological, paginated
   (fetches page 1/50 on load; no "load more" yet — fine for now since audit volume is low until
   Tickets 06-10 start writing more entries, but flag if that changes).
The header's status badge is read-only; the status-*transition* control (FRS §11.3, Ticket 06)
is **not** built here — Ticket 06 should add its control next to `<app-status-badge>` inside
`.detail-header`, reusing the `claim()` signal and re-fetching after a successful transition.

**Judgement calls** (flagging for later tickets / reviewers):
- The dashboard's "assigned handler" filter (FRS/spec.md story 16) is implemented as an
  "Assigned to me" checkbox bound to the logged-in user's id, not a free-text/typeahead handler
  picker — there is no `/api/users` list-or-lookup endpoint in the backend to resolve a handler
  name to an id (or vice versa), only `/auth/login`. Documented inline via the checkbox's
  tooltip. If a user-directory endpoint is added later, this is a small, additive change to swap
  in a real picker.
- The claim detail header's "Handler" field renders `assignedHandlerUserId` as a raw GUID (or
  "Unassigned"), not a display name — same root cause (no id→name endpoint exists yet). The FNOL
  wizard itself never needs this problem since it only ever submits the *current* user's own id.
- `ClaimsFilter`/`GetClaimsQuery` has no `policyId` filter, matching Ticket 04's own documented
  gap (FRS §10.1 lists one, but Ticket 04's checklist — the primary authority — didn't), so it
  was correctly left off the frontend's filter bar too.
- `angular.json`'s initial bundle budget warning was raised from 500kB to 600kB to accommodate
  Angular Material's stepper/autocomplete/table modules now pulled into the initial chunk; actual
  initial bundle is 536kB, comfortably under the 1MB error threshold.

Reviewed via `/code-review` (Standards + Spec axes, parallel sub-agents) before committing; real
findings were addressed: `claim-detail.component.ts`'s `partyDisplayName` now takes the real
`ClaimParty` type instead of an ad-hoc inline shape; `StatusBadgeComponent`'s `status` input and
its colour-lookup table are now typed `ClaimStatus` instead of a bare `string` (regains
compile-time exhaustiveness); and `fnol-wizard.component.spec.ts`'s submit test now seeds
`localStorage` directly (matching `auth.service.spec.ts`'s own precedent) instead of
`spyOn`-ing `AuthService.currentUser`, so `AuthService` stays real rather than mocked. Two other
review findings were considered and rejected: a claimed "components use `inject()`, services use
constructor DI" convention turned out not to hold — Ticket 02's own `shell.component.ts` and
`login.component.ts` already use constructor DI, so the dashboard/wizard components introduce no
new inconsistency; and the wizard's own spec drives multi-step form state via `patchValue`/
`submit()` rather than full DOM interaction (typed input, clicked `mat-option`, clicked
`matStepperNext`) — accepted as a judgement call given the disproportionate cost/flakiness risk
of DOM-driving Material's stepper/autocomplete overlays in headless Karma, while the test still
exercises the real `FormGroup`/validators the template binds to and the real HTTP-translation
seam (`ClaimsService`) per spec.md's stated rationale for that seam.
