# SMS twin

A behavioural clone of an SMS provider, for products' tests and previews. A product sends SMS to it as it would to a real provider. Tests read what arrived, and they can make sends fail on purpose. Tests can also simulate an incoming SMS (a reply), which the twin delivers to the product's webhook, signed. Everything is kept in memory.

- **Image:** `ghcr.io/filipedominguesgit/sms-twin:<version>`, public. Pin a version; there is no `latest`.
- **Port:** 8080 in the container.
- **Version:** 0.3.0, which is also returned by `GET /health`. History; each version keeps everything in the one before it:
  - 0.3.0 adds the optional `bodyContains` filter to fault rules.
  - 0.2.0 adds incoming messages.
  - 0.1.0: the send, test and fault APIs.
- **Configuration:**
  - `TWIN_API_KEY`. When it is set, the send API needs `Authorization: Bearer <key>`. When it is unset or empty, the send API is open.
  - `TWIN_WEBHOOK_URL`: where incoming messages are delivered, an absolute `http` or `https` URL. Optional; an invalid value stops the twin at startup.
  - `TWIN_WEBHOOK_SECRET`: the key that signs deliveries. Optional; without it, deliveries are not signed.

It is product-agnostic. The concrete failure cases a product must survive belong in its hidden scenarios, not here.

## Send API (what a product calls)

`POST /v1/messages`

```json
{ "to": "+351910000001", "from": "ExampleCo", "body": "Hello" }
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
| `POST /twin/inbound` | 201; simulates an incoming SMS and delivers it (below) |
| `POST /twin/inbound/{id}/redeliver` | 200; delivers it again. 404 for an unknown id |
| `GET /twin/inbound` | 200, every incoming message, in order, with its delivery attempts |
| `DELETE /twin/inbound` | 204; clears the incoming messages |
| `GET /health` | 200, `{ "status": "ok", "version": "0.3.0" }` |

A message:

```json
{ "id": "sms_…", "to": "+351910000001", "from": "ExampleCo", "body": "Hello",
  "encoding": "GSM-7", "segments": 1, "receivedAt": "2026-11-21T18:00:00.123+00:00", "idempotencyKey": "abc" }
```

`receivedAt` is the twin's own real clock, never the product's. `idempotencyKey` is `null` when the request had none.

### Fault rules

```json
{ "to": "+351910000001", "bodyContains": "Your order", "action": "error", "status": 503, "times": 2 }
```

| Field | |
|---|---|
| `action` | `error`, `delay` or `accept-then-error` (required) |
| `times` | how many send requests the rule applies to, at least 1 (required) |
| `to` | only send requests to this number (optional; every number when absent) |
| `bodyContains` | only send requests whose `body` contains this text, a non-empty string (optional, 0.3.0; every body when absent). Ordinal and case-sensitive |
| `status` | 100 to 599 (optional; for `error` the default is 503, for `accept-then-error` 500) |
| `delayMs` | 0 to 120000 (required for `delay`) |

A rule that does not follow this table gets **400**. The `201` answer and `GET /twin/faults` show each rule with its `id` and every field above; `to`, `bodyContains`, `status` and `delayMs` are `null` when they were not given.

**How rules apply:**
- For each send request that passed authentication and validation, the first rule in creation order that matches is used once. When its `times` reach 0, it is removed.
- A rule matches when its `to` matches (or it has none) **and**, when it has `bodyContains`, the request's `body` contains that text. A rule that does not match is skipped and keeps its `times`, so the next rule in order can match.
- At most one rule applies per request.

**What each action does:**
- `error`: answers `status` with `{ "error": ... }` and stores nothing.
- `delay`: waits `delayMs`, then handles the request normally.
- `accept-then-error`: stores the message, as on success, then answers `status`. A retry with the same `Idempotency-Key` then gets the original 202, and nothing is duplicated.

## Incoming messages (0.2.0)

`POST /twin/inbound`

```json
{ "from": "+351910000001", "to": "ExampleCo", "body": "YES" }
```

Validation, **400** with `{ "error": "..." }`: `from` is not `+` followed by 8 to 15 digits; `to` is missing or empty; `body` is not a string, or is longer than 1600 characters. An empty `body` is allowed.

The twin gives the message an id (`in_…`) and a `receivedAt`, stores it, delivers it, and only then answers **201** with the message:

```json
{ "id": "in_…", "from": "+351910000001", "to": "ExampleCo", "body": "YES",
  "receivedAt": "2026-11-21T18:00:00.123+00:00",
  "attempts": [ { "at": "2026-11-21T18:00:00.140+00:00", "status": 204, "error": null } ] }
```

**Delivery:** one `POST <TWIN_WEBHOOK_URL>` with `Content-Type: application/json` and this body:

```json
{ "id": "in_…", "from": "+351910000001", "to": "ExampleCo", "body": "YES", "receivedAt": "2026-11-21T18:00:00.123+00:00" }
```

- When `TWIN_WEBHOOK_SECRET` is set, the request has `X-Sms-Signature: sha256=<hex>`: the lowercase hex HMAC-SHA256 of the exact body bytes sent, keyed by the UTF-8 bytes of the secret. There is no timestamp in the signature; a receiver can use `id` to spot repeats.
- Timeout 10 seconds. There is no automatic retry, whatever the answer.
- An attempt records the webhook's HTTP status, or `status: null` with an `error`: `timeout`, `connection`, or `not-configured` when `TWIN_WEBHOOK_URL` is unset.
- `POST /twin/inbound/{id}/redeliver` sends the same body bytes and signature again, and adds an attempt. Use it to test duplicate delivery.
- `receivedAt` and `at` are the twin's own real clock.

Fault rules apply only to the send API, never to incoming messages.

## Running it

```
docker run --rm -p 8090:8080 -e TWIN_API_KEY=dev-sms-key ghcr.io/filipedominguesgit/sms-twin:<version>
```

Without Docker, with the .NET SDK that `global.json` pins, from the repo root:

```
dotnet run --project sms/app -- --urls http://localhost:8090
```

To build the image locally, also from the repo root: `docker build -f sms/Dockerfile -t sms-twin .`

## Self-test

The project `selftest/` checks this contract against a running twin. It runs named check groups; each one resets the twin's state first, and a failure names its group.

```
dotnet run --project sms/selftest -- http://localhost:8090 <api key> [<webhook secret> <listen prefix>]
```

The twin must run with the same `TWIN_API_KEY`.
- With the webhook arguments, the self-test listens on `<listen prefix>` (for example `http://localhost:8091/`). The twin must run with `TWIN_WEBHOOK_URL` pointing at its path `/hook` (`http://localhost:8091/hook`) and the same `TWIN_WEBHOOK_SECRET`. A twin in a container reaches a listener on the host through `host.docker.internal` (on Linux, with `--add-host host.docker.internal:host-gateway`), and the listener must accept every host (`http://*:8091/`).
- Without them, the twin must run with no `TWIN_WEBHOOK_URL`, and the self-test checks `not-configured`.

## Code

- `app/`: the twin, `SmsTwin.csproj`, with a folder per area: `Messages/` (the send API and the stored messages), `Faults/`, `Inbound/` (incoming messages and the webhook), `Sms/` (numbers and GSM encoding), `Http/` (strict JSON reading, errors, the API key filter) and `Health/`.
- `selftest/`: the self-test, with a typed client (`Client/`), the webhook listener (`Webhook/`) and the check groups (`Groups/`).
- The build rules are shared by every twin, at the repo root: one top-level type per file, named after it, and braces required, all as build errors; warnings are errors; the SDK is pinned in `global.json`. CI also runs `dotnet format DigitalTwins.slnx --verify-no-changes`.

## Releasing

The workflow `.github/workflows/sms.yml` checks the build rules, builds the image and runs the self-test on every change. To publish:
1. Bump `Version` in `app/SmsTwin.csproj` and the version in this README.
2. Push the tag `sms-v<version>`.

The workflow refuses a tag that differs from the project's version, or an image that reports another version, and it never overwrites a published version. Versions up to 0.1.0 were published from an earlier repository under the tag `sms-twin-v<version>`.
