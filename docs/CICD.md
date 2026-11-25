# CI/CD

## Flow

```
developer ── v0 branch ── open PR into main ──► [CI] build + test + docker build + smoke test
                                                   │ red → merge is blocked
                                                   ▼ green
                                       merge the PR into main
                                                   │
                                                   ▼
                       [CD] test → build image → push to ghcr.io → (deploy: not set up yet)
```

Pipeline file: `.github/workflows/ci.yml`. Image recipe: `Dockerfile` (multi-stage, non-root).

## Jobs

| Job | Runs on | What it does |
|---|---|---|
| `build-and-test` | PR, push to `main` | restore, Release build, all unit and integration tests (SQL Server through Testcontainers) |
| `docker-build` | PR, push to `main` | builds the image, starts it and calls `/health/live` |
| `publish` | push to `main` only | pushes `ghcr.io/<owner>/<repo>:latest` and `:<commit-sha>` |

The smoke test uses throwaway settings and only the `live` endpoint, so it needs no database. `/health/ready` needs SQL Server and is for the real environment.

## One-time GitHub setup

1. Settings → Branches → rule for `main`: require a pull request, require status checks `build-and-test` and `docker-build`. Without this a red check does not block a merge.
2. Nothing else for `publish`: it uses the token GitHub gives every run. The package is private by default; pulling it needs `docker login ghcr.io`.

## Running the published image

```bash
docker run -p 8080:8080 \
  -e Security__Jwt__SecurityKey="<at least 32 bytes, from a secret store>" \
  -e ConnectionStrings__BankAppDb="<SQL Server connection string>" \
  ghcr.io/<owner>/<repo>:latest
curl http://localhost:8080/health/ready
```

## Not done yet (before a real deployment)

- **Deploy job.** Pick a host, store its token in GitHub Secrets, add a `deploy` job after `publish` that ends with `curl -f https://<address>/health/ready`.
- **Database migrations.** The app migrates the database only in Development. In production run EF migrations as an explicit deployment step (`scripts/ef.sh`), before the new image starts.
- **Secrets.** The JWT key and the connection string come from the host's secret store, never from a file in the repository. Do not set the `Security__BootstrapStaff__*` or `Mailing__LogBody` settings in production (see README, section 6 and 5a).
- **Scanning.** Dependency and secret scanning are not part of the pipeline yet.
