# SMS twin: ideas for contract changes

Each of these changes what the twin accepts or answers, so it needs a new version, with `README.md`, the self-test and `Version` changed together. Clients that read the twin's error messages must be checked first.

## Standard body binding and validation

**Rejected (22 Nov 2026).** The twin must behave like a provider: authentication comes before the body is read (401 before 400), and the `{ "error": ... }` shape is the twin's own contract, which its clients' specs and test runners use. Binding first and ProblemDetails would make it a less faithful twin, for tidier code only. If tidier code is wanted, keep `JsonBody.Read<T>` and move the value rules into a validator per request record, with no contract change.

The idea, as it was proposed:

Today every endpoint reads its body with `JsonBody.Read<T>` instead of the minimal API's automatic binding (`[FromBody]`), because the binding answers differently from the contract:

| Case | Automatic binding | Contract today |
|---|---|---|
| `Content-Type` other than `application/json`, or none | 415 | read as JSON |
| Body that is not valid JSON | generic 400 | 400 `{ "error": "the body is not valid JSON" }` |
| A field of the wrong JSON type, such as `"to": 123` | generic 400, no field named | 400 with that field's message |
| Duplicate keys | accepted, the last one wins | 400 |
| No API key and a broken body | 400 (binding runs before endpoint filters) | 401 |

A validator such as FluentValidation runs after binding, so it does not change any of these rows.

Accept the standard behaviour in a new version:

- Answer 415 for other content types.
- Return errors as ProblemDetails, or `ValidationProblem` with the errors per field, instead of `{ "error": ... }`.
- Accept that a binding 400 comes before the 401.

Endpoints would then take the request record directly, and the value rules would move out of the endpoints. Those rules are `times` at least 1, `status` from 100 to 599, `delay` needing `delayMs`, and the number format. They would go to the built-in .NET validation (`AddValidation()` with attributes) or to a validator library.
