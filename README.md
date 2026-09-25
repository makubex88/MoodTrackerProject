# Mood Tracker

One question a day — *How are you feeling today?* — four answers, an optional comment, and no second
answers until tomorrow. An admin page lists every entry, newest first.

Built for the JAVLN take-home on the supplied template (ASP.NET Core 8 · Angular 18 · MySQL 8.4 ·
Docker Compose), renamed from `InterviewProjectTemplate` to `MoodTrackerProject`.

1. [Run it](#1-run-it) · 2. [Tests](#2-tests) · 3. [How it's built](#3-how-its-built) ·
4. [What I'd do next, and better](#4-what-id-do-next-and-better) · [API](#api)

---

## 1. Run it

You'll probably want to see it working before reading anything else. With Docker running:

```bash
docker compose build
docker compose up
```

| | |
|---|---|
| Log a mood | http://localhost:4200 |
| Admin page | http://localhost:4200/admin — password `change-me-locally` (a local default, on purpose) |
| Fresh database | `docker compose down -v`, then the two commands again |

The schema creates itself on first start; there's nothing to seed. To watch the one rule in action,
post twice with the same cookie — the second is refused:

```bash
curl -c jar.txt http://localhost:4200/api/moods/today
curl -b jar.txt -c jar.txt -H "Content-Type: application/json" -d "{\"rating\":3}" http://localhost:4200/api/moods   # 201
curl -b jar.txt -H "Content-Type: application/json" -d "{\"rating\":4}" http://localhost:4200/api/moods              # 409
```

Outside Docker: `dotnet run` in `MoodTrackerProject/` (needs a MySQL on 3306) and `npm start` in
`Client/web-client/` (Node 20; it proxies `/api` to the backend, so it behaves exactly like the
container). Settings live in `appsettings.json` and are overridden by the variables in
`docker-compose.yml`; nothing secret is committed.

---

## 2. Tests

```bash
dotnet test                       # backend — 79 cases; needs Docker for the 18 integration cases
dotnet test --filter "FullyQualifiedName!~Integration"   # the 61 that don't

cd Client/web-client && npm install && npm run test:ci   # frontend — 35 specs, headless
```

CI (`.github/workflows/ci.yml`) runs both, then the two compose commands from the brief and a health
check through nginx — the same thing you just did, on a machine that isn't mine.

If it looks like a lot of tests for a small app: the rule "once per day" is the whole assignment, so
most of the effort went into proving it holds under the conditions where it's easiest to get wrong —
midnight, daylight saving, two tabs clicking at once.

| Area | Cases | What it proves, in plain terms |
|------|:-----:|--------------------------------|
| **Backend — unit** | | |
| The once-a-day rule | 17 | First entry saved; second refused without overwriting; the day boundary is the team's midnight in Melbourne, not UTC; comments trimmed, blank ones stored as no comment at all, 500 allowed and 501 refused |
| The race | 3 | Two tabs submitting at the same instant: the database's unique index refuses the second even when the code's own check has already let it through. A test deliberately stages that collision. |
| The admin list | 8 | Newest first regardless of insert order; counts cover every row; paging behaves at the edges |
| The team clock | 11 | The day flips at Melbourne midnight, including after the October daylight-saving switch; the app refuses to start on a bad time-zone name |
| The identity cookie | 8 | Minted once, reused after, replaced if garbage; the exact flags that keep it out of reach of scripts |
| Admin password | 9 | Only the exact password matches, compared in constant time |
| The HTTP layer | 5 | 201 with the entry; 409 as a proper problem-details error with the date; the identity comes from the cookie, never the request body |
| **Backend — integration** *(real MySQL in Docker)* | | |
| Participant journey | 11 | First visit → cookie; 201 then 409 for real; two browsers both succeed; two simultaneous posts produce exactly one row; bad input is a 400; an emoji survives the round trip |
| Admin journey | 7 | 401 without signing in, even with a participant cookie; wrong password refused; right password → ticket → list; a tampered ticket is rejected; sign-out works; being admin doesn't log a mood |
| **Frontend** | | |
| The four options | 3 | Exactly the brief's four labels, verbatim, in order |
| The mood screen | 12 | Submit stays disabled until a mood is chosen; the counter; 501 blocks; a 409 shows the message and locks the view; network failure keeps the form usable; a double-click can't post twice |
| Admin sign-in and list | 9 | Wrong password shows a message; right one navigates; the list keeps the server's order, shows Melbourne times and the four counts |
| Plumbing | 11 | Calls go to the configured API path; no identity in requests; the route guard; the time-zone formatter; the app shell |

Every individual case is listed in [`docs/tests.md`](docs/tests.md), by file, with what each one
proves. Test names carry stable ids (MS-03, IT-04 …) so you can go from either table to the code.

---

## 3. How it's built

```
Browser ──► nginx :4200 ──► Angular bundle
                 └── /api ──► ASP.NET Core :8080 ──► MySQL
```

The browser sees one origin. nginx serves the app and forwards `/api` to the backend, so the identity
cookie is an ordinary first-party cookie and there's no CORS to reason about.

Backend: thin controllers, one service that owns the rule, EF Core underneath. Frontend: Angular 18
standalone components; one screen with three states (form, logged, refused) plus the admin pages.

### Five decisions, and why

| Decision | Why |
|----------|-----|
| **The database enforces "once per day", not the code.** A unique index on (participant, day). | Two requests can arrive in the same millisecond and both pass a code check. The index can't be fooled; the code turns its refusal into the same friendly 409. |
| **"Today" is Melbourne's day, not UTC.** | A UTC boundary rolls over at 10–11am Melbourne time — someone could log at 9 and again at noon. The app won't even start if the time zone can't be resolved, rather than quietly falling back to UTC. |
| **Identity is a browser cookie, not a login.** The brief forbids authentication for participants. | The server issues an opaque id the browser can't read or forge. It identifies a *browser*, not a person — see §4 for what that costs. |
| **The admin page has a real sign-in.** | The brief says admin *account*, and this page shows everyone's data. One configured password, checked in constant time, exchanged for an encrypted ticket cookie. No user database — for one admin it would be machinery for nothing, and real accounts would slot in at one place. |
| **Errors are standard problem details.** | The client decides what to show from the status code and a `detail` message; it never parses prose. |

### Practices, briefly

Typed configuration validated at start-up · time is injected (never `DateTime.Now`) so tests can sit
the clock at 23:59 · migrations run at boot with a retry while MySQL warms up · warnings are errors
in the Docker build · `.editorconfig` + format check in CI · native form controls for accessibility
(keyboard and screen readers work without custom code) · light and dark themes · one place for the
API path, one place for the four labels · small commits under one author · no secrets in the repo.

Three things worth knowing if you read the code: the participant id is a `Guid` because the MySQL
driver treats any `char(36)` column as one (mapping it as text throws on read — I learned that on the
first run); the duplicate-key check is provider-specific and kept behind one interface so tests can
swap it; and the EF provider is Pomelo rather than the template's, for cleaner date handling — the
database is unchanged.

---

## 4. What I'd do next, and better

**Next, in order:** real accounts (the participant id becomes the user id at one seam, the schema
doesn't change) · a "possible duplicate" flag on the admin page from a daily-salted IP+browser hash —
a hint for a human, never a block, because fingerprinting an office full of identical laptops would
refuse honest people · a seven-day trend on the admin page · a short window to correct a mis-tap ·
paging controls · a browser-level journey test.

**Honestly, what I'd flag myself:**

- **The first run failed** on the `char(36)`/`Guid` mismatch above. I found it by using the app,
  not by a test. The integration suite covers that path now — right place, wrong order.
- **One build sent the 409 with the wrong media type.** A class-level attribute silently overrode
  it, past where a unit test can see. The integration test caught it — which is the best argument in
  this repo for having both layers.
- **The migration was written by hand**, not generated, because the tooling wasn't to hand. It runs
  correctly (the integration tests prove the schema), but the next generated migration should be
  checked for a spurious diff.
- **The frontend was built fresh on Angular 18** rather than upgraded from the template's Angular 15
  shell — there was nothing in it to preserve. You won't find upgrade commits; that's why.
- **Identity is per browser.** Clear cookies, open incognito, use your phone: you're a new
  participant. Every fix without authentication is either wrong for a shared office or worse for
  privacy, so I kept the mechanism honest and easy to replace.
- **The admin is one shared password**, not a user store, and can't be revoked per person.

---

## API

| | | |
|---|---|---|
| `GET /api/moods/today` | Has this browser logged today? Issues the cookie if absent | `200 { logged, entry, today, timeZone }` |
| `POST /api/moods` | `{ rating: 1–4, comment? }` | `201` · `400` · `409` problem+json |
| `POST /api/admin/login` | `{ password }` → ticket cookie | `204` · `400` · `401` |
| `GET /api/admin/me` | Signed in as admin? | `204` · `401` |
| `POST /api/admin/logout` | Clears the ticket | `204` |
| `GET /api/admin/moods` | Every entry, newest first; `?page=1&pageSize=50` | `200 { items, total, page, pageSize, countsByRating, timeZone }` · `401` |
| `GET /api/health` | Liveness | `200` |

`docs/dataflow.html` steps through both journeys and shows where the data is at each moment —
browser, request, service, SQL, table, response. `MoodTrackerProject/MoodTrackerProject.http` has
the requests above ready to send.
