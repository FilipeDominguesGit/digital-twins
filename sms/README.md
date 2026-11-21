# SMS twin

A behavioural clone of an SMS provider, for products' tests and previews. A product sends SMS to it as it would to a real provider. Tests read what arrived, and they can make sends fail on purpose. Everything is kept in memory.

- **Image:** `ghcr.io/filipedominguesgit/sms-twin:<version>`, public. Pin a version; there is no `latest`.
- **Port:** 8080 in the container.
- **Version:** 0.1.0, which is also returned by `GET /health`.
- **Configuration:** `TWIN_API_KEY`. When it is set, the send API needs `Authorization: Bearer <key>`. When it is unset or empty, the send API is open.

It is product-agnostic. The concrete failure cases a product must survive belong in its hidden scenarios, not here.

## Send API (what a product calls)

`POST /v1/messages`

```json
{ "to": "+351910000001", "from": "VetClinic", "body": "Hello" }
```

| Header | |
|---|---|
| `Authorization: Bearer <key>` | Required when `TWIN_API_KEY` is set. Missing or wrong: **401**. |
| `Idempotency-Key: <string>` | Optional. See below. |

Validation, **400** with `{ "error": "..." }`:
- the body is not a JSON object;
- `to` is not `+` followed by 8 to 15 digits;
- `from` is missing or empty;
- `body` is missing or empty, or longer than 1600 characters (UTF-16 code units).

Success, **202**:

```json
{ "id": "sms_…", "status": "accepted", "encoding": "GSM-7", "segments": 1 }
```

- **Encoding and segments**, as a provider bills them:
  - `GSM-7` when every character is in the GSM 03.38 basic or extension table. An extension character (`^{}\[~]|€` and form feed) counts as 2.
  - Otherwise `UCS-2`, counted in UTF-16 code units, so an emoji counts as 2.
  - One segment holds 160 GSM-7 or 70 UCS-2 units. Longer texts use parts of 153 or 67 units.
- **Idempotency:** the first request with a given key stores the message and remembers the response.
  - A later request with the same key and the same `to`, `from` and `body` gets the same 202 response, and nothing new is stored.
  - The same key with a different request gets **409**.

Fault rules (below) are applied before the idempotency check.

## Test API (no key needed)

| Request | Response |
|---|---|
| `GET /twin/messages` | 200, every stored message, in the order received |
| `GET /twin/messages?to=<number>` | 200, only that number's messages |
| `DELETE /twin/messages` | 204; clears the messages **and** the idempotency keys |
| `POST /twin/faults` | 201 with the rule; adds a fault rule |
| `GET /twin/faults` | 200, the rules still in use, with the `times` they have left |
| `DELETE /twin/faults` | 204; clears the rules |
| `GET /health` | 200, `{ "status": "ok", "version": "0.1.0" }` |

A message:

```json
{ "id": "sms_…", "to": "+351910000001", "from": "VetClinic", "body": "Hello",
  "encoding": "GSM-7", "segments": 1, "receivedAt": "2026-11-21T18:00:00.123+00:00", "idempotencyKey": "abc" }
```

`receivedAt` is the twin's own real clock, never the product's. `idempotencyKey` is `null` when the request had none.

### Fault rules

```json
{ "to": "+351910000001", "action": "error", "status": 503, "times": 2 }
```

| Field | |
|---|---|
| `action` | `error`, `delay` or `accept-then-error` (required) |
| `times` | how many send requests the rule applies to, at least 1 (required) |
| `to` | only send requests to this number (optional; every number when absent) |
| `status` | 100 to 599 (optional; for `error` the default is 503, for `accept-then-error` 500) |
| `delayMs` | 0 to 120000 (required for `delay`) |

A rule that does not follow this table gets **400**.

**How rules apply:**
- For each send request that passed authentication and validation, the first rule in creation order whose `to` matches is used once. When its `times` reach 0, it is removed.
- At most one rule applies per request.

**What each action does:**
- `error`: answers `status` with `{ "error": ... }` and stores nothing.
- `delay`: waits `delayMs`, then handles the request normally.
- `accept-then-error`: stores the message, as on success, then answers `status`. A retry with the same `Idempotency-Key` then gets the original 202, and nothing is duplicated.

## Running it

```
docker run --rm -p 8090:8080 -e TWIN_API_KEY=dev-sms-key ghcr.io/filipedominguesgit/sms-twin:0.1.0
```

Without Docker, with the .NET 10 SDK, from this folder:

```
dotnet run app.cs --urls http://localhost:8090
```

## Self-test

`selftest.cs` checks this contract against a running twin. It resets the twin's state as it goes.

```
dotnet run selftest.cs -- http://localhost:8090 <api key>
```

The twin must run with the same `TWIN_API_KEY`.

## Releasing

The workflow `.github/workflows/sms.yml` builds the image and runs the self-test on every change. To publish:
1. Bump `TwinVersion` in `app.cs` and the version in this README.
2. Push the tag `sms-v<version>`.

The workflow refuses a tag that differs from the twin's version, and it never overwrites a published version. Versions up to 0.1.0 were published from an earlier repository under the tag `sms-twin-v<version>`.
