# Working on the digital twins

This public repo holds digital twins: product-agnostic clones of external services, published as versioned public images on GHCR.

- One folder per twin: `app.cs` (.NET 10 single-file app), `Dockerfile`, `README.md` (the contract), `selftest.cs`.
- Never mention or depend on a specific product. No real personal data: synthetic numbers and names only.
- A change to a twin's contract updates its `README.md`, its self-test and `TwinVersion` in the same commit. A published version is never changed.
- Before committing, run the twin and its self-test: `dotnet run <twin>/app.cs --urls http://localhost:8090` (with `TWIN_API_KEY` set), then `dotnet run <twin>/selftest.cs -- http://localhost:8090 <key>`.
- Release by pushing the tag `<twin>-v<version>`; the workflow publishes the image.
