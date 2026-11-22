# Working on the digital twins

This public repo holds digital twins: product-agnostic clones of external services, published as versioned public images on GHCR.

- One folder per twin: `app/` (a small .NET project, a folder per area), `Dockerfile`, `README.md` (the contract), `selftest/` (a .NET project with named check groups).
- Build rules are shared at the repo root (D-028): one top-level type per file, named after it (StyleCop SA1402 and SA1649), braces required, warnings as errors, the SDK pinned in `global.json`. Check with `dotnet build DigitalTwins.slnx` and `dotnet format DigitalTwins.slnx --verify-no-changes`.
- Never mention or depend on a specific product. No real personal data: synthetic numbers and names only.
- A change to a twin's contract updates its `README.md`, its self-test and `Version` in `app/*.csproj` in the same commit. A published version is never changed.
- Before committing, run the twin and its self-test from the repo root: `dotnet run --project <twin>/app -- --urls http://localhost:8090` (with `TWIN_API_KEY` set), then `dotnet run --project <twin>/selftest -- http://localhost:8090 <key>`. A refactor first adds checks that lock what the twin accepts and refuses today.
- Release by pushing the tag `<twin>-v<version>`; the workflow publishes the image.
