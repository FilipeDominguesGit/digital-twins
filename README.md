# Digital twins

Behavioural clones of external services, for tests and preview environments. A product talks to a twin as it would to the real service. Its tests can read what arrived and make the twin fail on purpose. Twins keep everything in memory and know nothing about any product.

| Twin | Image | What it does |
|---|---|---|
| [`sms`](sms/README.md) | `ghcr.io/filipedominguesgit/sms-twin` | An SMS provider: a send API with idempotency keys and GSM-7/UCS-2 segments, an inspection API, fault injection (`error`, `delay`, `accept-then-error`), and incoming SMS delivered to a signed webhook |

Each twin has its own folder with:
- `app/`: the twin, a small .NET project with a folder per area;
- `Dockerfile`;
- `README.md`: the contract;
- `selftest/`: a .NET project that checks the contract against a running twin.

Every twin follows the same build rules, at the repo root (`Directory.Build.props`, `.editorconfig`, `stylecop.json`, `global.json`): one top-level type per file, named after it, and braces required, all as build errors; warnings are errors; the SDK is pinned. `DigitalTwins.slnx` holds every project, and CI checks it with `dotnet format --verify-no-changes`.

## Rules
- **Versioned images.** Images are public and versioned, with no `latest`. Consumers pin a version.
- **One commit per contract change.** A change to a twin's contract updates, in the same commit:
  - its `README.md`;
  - its self-test;
  - its version (`Version` in the twin's `app/*.csproj`).
- **Published versions never change.**
- **Releasing:** push the tag `<twin>-v<version>`, for example `sms-v0.2.0`. The twin's workflow builds the image, runs the self-test, checks that the tag matches the project's version, and publishes the image.
- **Fault APIs are generic.** The concrete failure cases a product must survive belong in that product's own tests and scenarios, not here.

## License
MIT
