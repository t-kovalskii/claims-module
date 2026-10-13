# 02 — Frontend shell + auth

**What to build:** the Angular application shell every feature screen plugs into — routing, the HTTP interceptor that attaches the Bearer token automatically, a login screen against the mock auth endpoint, and a shell that reflects the logged-in user's identity and role.

**Blocked by:** 01

**Status:** ready-for-agent

- [ ] Logging in through the UI (against the real backend login endpoint from Ticket 01) stores the returned token and navigates into the application shell.
- [ ] Every outgoing API call automatically carries the Bearer token via an HTTP interceptor — no component or service attaches it manually.
- [ ] The shell displays the logged-in user's name and role, and offers a way to switch between the seeded handler/supervisor/manager accounts for testing.
- [ ] Routing is set up with lazy-loaded feature module stubs for the claims dashboard and claim detail (empty placeholders are fine — filled in by later tickets).
- [ ] A custom Angular Material theme (primary colour palette) is applied consistently across the shell.
- [ ] A failed API call (e.g. expired/invalid token) surfaces as a user-friendly snackbar notification, not a silent failure or raw error dump.
