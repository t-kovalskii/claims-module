# 12 — Architecture & AI-workflow documentation

**What to build:** the documentation deliverables that let a reviewer assess the design and the AI-assisted workflow without re-deriving it from the code.

**Blocked by:** 11

**Status:** done

- [x] `ARCHITECTURE.md` describes the solution structure, the data model and relationships, the CQRS flow from API to persistence, which domain events exist and how they're handled, the Hangfire job design and idempotency strategy, the Azure architecture, and the key decisions and tradeoffs — drawing directly on the ADRs already written during grilling rather than re-deriving them.
- [x] `AI-WORKFLOW.md` names the AI tools used, describes the workflow (context loading, prompt sequencing, iteration), includes at least three representative prompts with their purpose, and documents at least two concrete cases where AI output was wrong and how it was corrected — the ADR-0006→ADR-0007 correction from grilling is one ready-made example.
- [x] `README.md` gives complete prerequisites, local setup, environment variable/configuration documentation, migration and seeding instructions, and a description of how to reproduce the Azure deployment.
- [x] Both documents and the README are cross-checked against the actual deployed system from Ticket 11, not written from assumptions.

## Comments

All three files live at the repo root: `ARCHITECTURE.md`, `AI-WORKFLOW.md`, `README.md`.

**Source material**: all nine ADRs (`docs/adr/0001`–`0009`), `CONTEXT.md`, `spec.md`'s
Implementation/Testing Decisions and Further Notes sections, and the actual source code (domain
entities/events, event handlers, Hangfire jobs, `DispatchDomainEventsInterceptor`,
`ApplicationDbContext`'s query filters, `infra/` Bicep templates, `.github/workflows/deploy.yml`)
— read directly rather than taken on faith from the ADRs' own prose, to catch any drift between
what an ADR says and what the code actually does. None found; code matched every ADR.

**AI-WORKFLOW.md's required examples**: used the two ready-made ones spec.md's own Further Notes
section names (ADR-0006→ADR-0007's idempotency-key relocation, and the reserve component type
names where the Assessment appendix and FRS disagreed), plus two additional ones from this
session's own Ticket 11 work that I lived through directly rather than reconstructed from written
artifacts (the masked-GitHub-Actions-output bug, and the Easy Auth dependsOn-ordering bug caught
only by actually re-deploying and checking the live resource, not by reading the template).
Deliberately did not fabricate "representative prompts" from sessions outside this one's context —
used only prompts with direct evidence: the `/implement`/`/code-review` invocations from this
session, and `/grilling` as a named pattern (evidenced by spec.md's own text, not quoted verbatim
since the actual historical grilling transcript isn't in this session's context).

**Cross-checking against the deployed system** (not just written from the Bicep/pipeline source):
re-curled the live Swagger UI and frontend (both HTTP 200) and re-confirmed `handler1`/
`supervisor1` logins work, immediately before finalizing `README.md`'s live-deployment links and
`ARCHITECTURE.md`'s Azure architecture section, rather than relying on earlier-in-session
verification that could have gone stale.

**Judgement call**: `README.md`'s Azure-deployment-reproduction section gives generic
`<owner>`/`<repo>`/`<subscription-id>` placeholders rather than this specific deployment's actual
values (`t-kovalskii/claims-module`, etc.) — the point is a reader standing up *their own*
instance, not literally this one, and the actual values are already recorded in Ticket 11's own
Comments section for anyone who needs them.
