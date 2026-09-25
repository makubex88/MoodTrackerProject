# Test inventory

Every automated test, by file. Ids (MS-03, IT-04 …) appear in the tests' display names, so
`dotnet test --logger "console;verbosity=normal"` and the Karma reporter print the same labels.

**Run:** `dotnet test` from the repository root (Docker needed for the integration section), and
`npm run test:ci` in `Client/web-client`.

## Backend — `MoodTrackerProject.Tests` (79 cases)

### `Services/MoodServiceTests.cs` — the once-per-day rule (17)

| ID | Case | Proves |
|----|------|--------|
| MS-01 | First entry of the day | Saved with the **team-local** date: 23:14 UTC on the 22nd is stored as the 23rd (09:14 AEST) |
| MS-02 | Second entry, same day | Refused; the existing row is returned; the original is never overwritten; row count stays 1 |
| MS-02b | Two participants, same day | Both accepted |
| MS-04 ×3 | Rating 0, 5, 255 | Rejected before the database is touched |
| MS-05 | Comment of 500, then 501 characters | 500 accepted, 501 rejected |
| MS-06 ×6 | `null`, `""`, spaces, tab/newline, padded text, multi-line | Whitespace-only → no comment; text is trimmed; newlines survive |
| MS-07 | 23:59 then 00:01 team-local | Different days — both accepted |
| MS-08 | Crossing UTC midnight without crossing Melbourne midnight | Same team day — refused. The boundary is team-local by design, not by accident |
| — | `GetToday` before and after logging | Nothing, then the entry |
| — | `GetToday` for another participant | Not mine |
| — | `GetToday` the next day | Yesterday's entry is not today's |

### `Services/MoodServiceRaceTests.cs` — the constraint is the guarantee (3)

| ID | Case | Proves |
|----|------|--------|
| MS-03 | A competing row is inserted *between* the service's read-check and its write (a `SaveChangesInterceptor` plays the second tab) | The unique index refuses the write; the service maps it to "already logged" and returns the row that won; exactly one row exists |
| DB-01 | Two rows, same participant and day, straight into the context | The index throws, and the duplicate-key detector recognises it |
| DB-02 | Same participant on other days; other participants on the same day | Allowed |

### `Services/MoodServiceListTests.cs` — the admin list (8)

| ID | Case | Proves |
|----|------|--------|
| MS-09 | Rows inserted out of order | Returned strictly newest-first by instant |
| — | Page size 2 over 7 rows | Counts by mood cover every row, not just the page; they sum to the total |
| — | Empty database | No rows and four zero counts |
| — | Pages 1, 2, 3 and 9 of five rows | Pages continue correctly; past the end is empty with the same total |
| — ×3 | page 0 / −3; pageSize 0 / 5000 | Clamped to 1 and 1..200 |
| — | Two rows with the same instant | Tie-broken by id, so the order is stable |

### `Services/TeamClockTests.cs` — "today" is the team's day (11)

| ID | Case | Proves |
|----|------|--------|
| — ×4 | UTC instants either side of 14:00 UTC (Melbourne midnight in winter) | The day flips at team midnight |
| — ×2 | UTC instants either side of 13:00 UTC after the October switch | Daylight saving is honoured; a fixed +10 offset would be wrong |
| — | `UtcNow` | Passed through unchanged, `Kind = Utc` |
| — ×4 | `Australia/Melbourne`, `UTC`, `Mars/Olympus_Mons`, `""` | The start-up guard accepts real ids and rejects nonsense |

### `Identity/ParticipantCookieMiddlewareTests.cs` — identity without authentication (8)

| ID | Case | Proves |
|----|------|--------|
| ID-01 | No cookie | A GUID is minted and set `HttpOnly; SameSite=Lax; Path=/; Max-Age=1 year`; no `Secure` over http; no `Domain` |
| ID-01b | Over HTTPS | `Secure` is added |
| ID-02 | Existing cookie | Reused; no new cookie issued |
| ID-03 ×4 | `not-a-guid`, empty, 32-digit form, braced form | Replaced rather than trusted |
| — | Upper-case GUID | Accepted |

### `Auth/AdminAuthTests.cs` — the admin credential (9)

| ID | Case | Proves |
|----|------|--------|
| — ×6 | Exact, case-changed, truncated, padded, empty, both empty | Only the exact string matches |
| — | `null` supplied | Never matches |
| — | The admin principal | Authenticated, in the `Admin` role, correct scheme |
| — | Session properties | Expire `SessionHours` after issue |

### `Controllers/MoodsControllerTests.cs` — the HTTP layer (5)

| ID | Case | Proves |
|----|------|--------|
| — | Created | 201 with the entry, a `Location` header, and a UTC-kind timestamp |
| — | Already logged | 409 problem details: title, detail, `entryDate`, and `application/problem+json` |
| — | Participant id | Comes from the cookie context, never from the body |
| — | `GET today`, not logged | `logged:false`, no entry, team date and zone present |
| — | `GET today`, logged | `logged:true` with the entry |

### `Integration/ParticipantApiTests.cs` — end to end against real MySQL (11)

| ID | Case | Proves |
|----|------|--------|
| IT-00 | First contact | `HttpOnly` cookie issued; `logged:false`; time zone reported |
| IT-01 | Same browser posts twice | 201 then 409; the 409 is `application/problem+json` with `entryDate`; the first entry is untouched |
| IT-03 | Two browsers, same day | Both 201 |
| IT-04 | Two simultaneous posts from one browser | Exactly one 201 and one 409 — the real MySQL error 1062 path |
| — ×4 | Rating 0 / 5 / missing / non-numeric | 400 with a field error naming `rating` |
| — | 501-character comment | 400, and nothing was written |
| — | Newlines, curly quotes, an emoji | Round trip intact (utf8mb4) |
| — | Garbage cookie | A fresh identity is issued; nothing is trusted |

### `Integration/AdminApiTests.cs` — the admin gate (7)

| ID | Case | Proves |
|----|------|--------|
| AD-01 | No ticket, or only a participant cookie | 401 on `/me` and on the list; no rows in the body |
| AD-02 | Wrong password | 401; no admin ticket issued |
| AD-02b | Missing password | 400, not 401 |
| AD-03 | Right password, then the list | 204 with an `HttpOnly` ticket; list is 200, newest-first, counts sum to the total, ids are GUIDs |
| AD-04 | Tampered ticket (one character flipped) | Rejected as anonymous — Data Protection's integrity check |
| — | Sign out | The next `/me` is 401 |
| — | Admin browser uses the participant flow | Signing in as admin logs no mood and borrows no identity |

## Frontend — Jasmine / Karma (35 specs)

### `core/mood-options.spec.ts` (3)

| ID | Case | Proves |
|----|------|--------|
| FE-01 | The option list | Exactly the brief's four labels, verbatim, worst to best, values 1–4 |
| — | `moodLabel` | Maps a value to its label; unknown values are "Unknown" |
| — | Comment limit | Agrees with the API (500) |

### `core/mood.service.spec.ts` (3)

| ID | Case | Proves |
|----|------|--------|
| FE-04 | `getToday` | Calls `environment.apiUrl/moods/today` |
| FE-04 | `create` | Posts `{ rating, comment }` with `null` for no comment |
| — | Identity | No identity header or field in the request — the cookie is the browser's job |

### `core/admin.guard.spec.ts` (2)

| ID | Case | Proves |
|----|------|--------|
| FE-08 | `/api/admin/me` → 204 | Navigation allowed |
| FE-08 | `/api/admin/me` → 401 | Redirected to `/admin/login` |

### `core/team-time.pipe.spec.ts` (5)

| ID | Case | Proves |
|----|------|--------|
| — | 23:14Z in Melbourne | Shows 09:14 the next day |
| — | Date mode | Shows the team-local date |
| — | After the October switch | Daylight saving honoured (+11) |
| — | Empty / unparseable | Empty string |
| — | Unknown zone | Falls back rather than throwing |

### `features/mood/mood-page.component.spec.ts` (12)

| ID | Case | Proves |
|----|------|--------|
| — | Initial load | Loading state; no form until the server answers |
| FE-01 | Rendered options | Four native radios sharing one name, inside a fieldset with a legend, labels in order |
| FE-02 | Submit button | Disabled until a mood is chosen |
| FE-03 | Comment | Optional; counter tracks length; 500 fine; 501 blocks and marks the counter |
| — | Submit | Sends the numeric rating and a trimmed comment; swaps to the locked view |
| — | Blank comment | Sent as `null`, not `""` |
| FE-05 | 409 | Shows the server's message and locks the view with the winning entry |
| FE-06 | Already logged on load | Locked view; the form never renders |
| FE-07 | Network failure | Retry message; form stays usable |
| — | 400 | Shows the API's first validation message |
| — | In flight | Submit disabled and reads "Saving…"; a second click never posts twice |
| — | Server unreachable on load | Unavailable state with a working retry |

### `features/admin/admin-login.component.spec.ts` (3)

| ID | Case | Proves |
|----|------|--------|
| — | Empty field | Submit disabled |
| — | Right password | Posts it; navigates to `/admin` |
| — | Wrong password | Message shown; field cleared; no navigation |

### `features/admin/admin-page.component.spec.ts` (6)

| ID | Case | Proves |
|----|------|--------|
| FE-09 | Request and order | Asks for page 1 of 50; renders rows in the server's order without re-sorting |
| FE-09 | Formatting | Melbourne timestamps; ids shortened to `8f3c…7c21` with the full id on hover |
| — | Counts | The four totals, in the brief's order |
| — | 401 | Sent to the sign-in gate |
| — | Other failure | Retry state, no rows |
| — | Empty log | A sentence, not an empty table |

### `app.component.spec.ts` (1)

| ID | Case | Proves |
|----|------|--------|
| — | The shell | Renders with its two primary links |
