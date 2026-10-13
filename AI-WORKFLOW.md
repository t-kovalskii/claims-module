# AI-Assisted Workflow

## Tools

**Claude Code** (Anthropic), running Claude's Sonnet model family, used as the sole implementation
agent for this entire project — spec authoring, every ticket's implementation, infrastructure
provisioning, and this documentation itself. Claude Code's **skill system** structured the
workflow rather than ad hoc prompting: a repo-local `AGENTS.md` and `docs/agents/*.md` define this
project's own conventions (where issues/specs live, the triage-label vocabulary), and a set of
process skills (`brainstorming`/`grilling`, `tdd`, `code-review`, `implement`) were invoked
explicitly by name at each stage rather than relying on default behaviour.

## Workflow

**Context loading.** Every session starts from the same fixed points, not a conversation history:
`AGENTS.md` at the repo root points to `docs/agents/issue-tracker.md` (issues/specs live as
markdown under `.scratch/`), `docs/agents/triage-labels.md`, and `docs/agents/domain.md`.
`CONTEXT.md` carries the ubiquitous-language glossary (entity names, what to avoid and why). This
means a fresh agent with zero conversation history can pick up exactly where a previous session
left off by reading the same four or five files a human contributor would.

**Prompt sequencing**, repeated per feature area:

1. **Spec and ADR authoring, via grilling.** Before any ticket existed, the two source documents
   (`DICEUS_Fullstack_Technical_Assessment.docx`, `Claims_Module_Candidate_Specification.docx`)
   were turned into `.scratch/claims-module/spec.md` and one ticket file per feature area, with
   architecturally significant decisions recorded as ADRs under `docs/adr/` — produced by
   adversarial back-and-forth (the `grilling` skill: "grill the user relentlessly about a plan,
   decision, or idea"), not accepted on the first pass. This is where both corrections described
   below were actually caught, before any code existed to retrofit.
2. **Implementation, one ticket at a time.** `/implement next ticket in spec` picks the next
   unblocked ticket, builds it test-first at the seams `spec.md`'s own Testing Decisions section
   defines (HTTP integration tests as the primary backend seam, narrow in-memory unit tests only
   for `Claim`'s combinatorial domain logic), running typechecking and single-file tests
   continuously rather than only at the end.
3. **Review.** `/code-review since <fixed point>` runs two parallel sub-agents — one checking the
   diff against documented repo standards plus a fixed Fowler smell baseline, one checking it
   against the originating ticket — reported separately so one axis can't mask the other. Findings
   get fixed and re-verified before committing, not just logged.
4. **Commit**, with the ticket file itself updated to `Status: done` and a `## Comments` section
   recording what was built, judgement calls made, and anything the review caught — so the next
   session (or a human reviewer) doesn't have to re-derive any of it from the diff.

**Representative prompts** (the actual recurring invocations, not a one-off transcript):

| Prompt | Purpose |
|---|---|
| `/grilling` (over a specific decision, e.g. the reserve idempotency-key schema) | Pressure-test a design decision against the source spec *before* committing to it in code, surfacing gaps a single pass would miss. |
| `/implement next ticket in spec` | The single, repeated entry point for every feature ticket (01 through 12) — picks the next unblocked item from `.scratch/claims-module/issues/`, implements it test-first, and runs review before committing. |
| `/code-review since 3f90c62 (before "Apply code review follow-ups to SLA job and error interceptor")` | Pin a review to an exact commit range — not "the recent changes" — so Standards and Spec sub-agents review precisely what will be committed. |

**Iteration.** Within a ticket, the loop is: write/adjust the test, run it, fix, re-run — not
"write everything, then test once." Across tickets, each one lands as its own commit (or a small
cluster of commits when a ticket surfaced a pre-existing bug worth fixing separately, as Ticket 11
did — see below), so the git history itself is a record of the iteration, not just the final
state.

## Where AI output was wrong, and how it was corrected

**1. Reserve idempotency key placed on the wrong entity (ADR-0006 → ADR-0007).** The first pass at
the reserve-posting idempotency design put the `IdempotencyKey` column on `ClaimAuditLog`
(ADR-0006), reasoning that the audit log already recorded "what happened" and didn't need a
parallel ledger concept. This was based on the Assessment document's condensed appendix table
alone. Pulling the full FRS (§9.5/§9.6) during grilling showed the FRS already defines
`IdempotencyKey`, `PostingStatus`, and `PostingJobId` as columns on `ReserveHistory` itself, as
part of a fully event-sourced reserve model the condensed appendix hadn't made clear.
ADR-0006 was marked superseded and ADR-0007 written in its place — caught before any
`ReserveComponent`/`ReserveHistory` code existed, which mattered: retrofitting event-sourcing onto
an already-built mutable-row design would have been expensive, as ADR-0007 itself notes.

**2. Reserve component type names taken from the wrong source document.** The Assessment
document's appendix table lists reserve component types as `Indemnity`/`Expense`/`Recovery`/
`Litigation`. The FRS defines exactly four, differently: `Indemnity`, `Expense` (investigation/
expert fees), `ALAE` (attorney/court costs — distinct from `Expense`), `SubrogationRecoverable`
(the only one that may go negative). Where the two source documents conflicted, the FRS was
designated authoritative (it's explicitly self-contained and far more detailed) — this correction
is recorded as a standing "avoid" note in `CONTEXT.md`'s `ReserveComponent` entry, so the wrong
names can't silently resurface in a later ticket that only skims the Assessment appendix.

**3. (Ticket 11) GitHub Actions silently drops masked job outputs.** Every Bicep deployment
output (resource names, hostnames) was masked with `::add-mask::` before being published as a
pipeline job output, on the reasoning that "mask anything that came out of a secrets-bearing
deployment step" is a safe default. It isn't: GitHub Actions silently drops any job output
containing a masked value ("Skip output ... since it may contain secret"), which broke every
downstream job consuming `backendAppName`/`staticWebAppName`/etc. (`deploy-backend` failed with
`app-name is a required input.`). Caught by reading the actual failed run's logs rather than
assuming the masking was harmless; fixed by masking only the one value that's genuinely secret
(the Static Web App deployment token, consumed within the same job rather than across the
job-output boundary).

**4. (Ticket 11) An ordering fix that looked right but wasn't, verified by re-deploying.** Azure
auto-enables the backend App Service's built-in Authentication ("Easy Auth") with no identity
provider configured — a side effect of linking a Static Web App backend — returning 401 on every
route. The first fix added a `Microsoft.Web/sites/config` resource to the App Service's own Bicep
module to disable it. Re-deploying from scratch showed `platform.enabled` flip back to `true`:
the Static Web App module's linked-backend resource has no Bicep dependency edge forcing it to
deploy before the App Service module, so it could run *after* the disable and silently re-enable
it. The fix wasn't assumed correct from reading the template — it was verified by actually
re-running the deployment and checking the live resource's config, which is what caught the
ordering bug; moving the disable to depend explicitly on the Static Web App module and re-deploying
again confirmed the fix held.
