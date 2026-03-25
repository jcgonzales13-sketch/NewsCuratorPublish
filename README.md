# AI News Curator for LinkedIn

A .NET 8 application that collects AI and developer news from RSS feeds, stores it in SQLite, curates it with AI-assisted workflows, generates LinkedIn-ready drafts in English, and supports both manual and automatic publishing modes.

## Structure

- `src/AiNewsCurator.Api`: internal API endpoints, healthcheck, LinkedIn OAuth callback, and the `/ops` dashboard
- `src/AiNewsCurator.Worker`: scheduled background execution for collection and curation flows
- `src/AiNewsCurator.Application`: pipeline orchestration and editorial/business rules
- `src/AiNewsCurator.Domain`: entities, enums, interfaces, and shared rules
- `src/AiNewsCurator.Infrastructure`: SQLite persistence, RSS collection, AI integrations, LinkedIn publishing, and image enrichment
- `tests/`: unit and integration test suites

## Current Flow

1. Initialize the SQLite schema and seed default RSS sources.
2. Collect recent items from active RSS sources.
3. Normalize URLs and content hashes for deduplication.
4. Evaluate relevance and generate an editorial LinkedIn draft in English.
5. Validate the draft text and store editorial review notes if needed.
6. Save the draft for manual review or auto-approve it when confidence and validation thresholds allow it.
7. Publish the draft to LinkedIn on demand or automatically, including the original article URL in the final post text.

## Key Features

- English-only editorial generation and operational UI
- LinkedIn OAuth callback flow with access-token refresh support
- Automatic retry path for expired LinkedIn access tokens
- Draft review workflow with `Approve`, `Reject`, `Dismiss`, `Reopen`, and `Retry publish`
- Draft editing in `/ops`
- Source creation, editing, activation, and deactivation in `/ops`
- Search and pagination for drafts, news, sources, and runs
- Failure classification and retry guidance for failed LinkedIn publishes
- Automatic inclusion of `Original article: ...` in LinkedIn post text
- Legacy-draft repair at publish time so older drafts still receive the article URL before posting
- Manual image URL fallback in `/ops` when no image was found during collection or enrichment

## Default Sources

The application seeds default RSS sources, including:

- `OpenAI News`
- `MIT AI News`
- `.NET Blog`
- `C# Category - .NET Blog`
- `JetBrains .NET Tools Blog`
- `InfoQ .NET`
- `Visual Studio Magazine`

Default seeding is additive by URL, so missing defaults can be inserted even when the database already contains existing sources.

## Configuration

Use `.env.example` as the main reference for environment variables.

Important settings include:

- `DATABASE_PATH`: path to the SQLite database file
- `PUBLISH_MODE`: `Manual` or `Automatic`
- `INTERNAL_API_KEY`: required for `/internal/*` endpoints and `/ops` login
- `AI_PROVIDER`: `Heuristic` or `OpenAI`
- `AI_API_KEY`: required when `AI_PROVIDER=OpenAI`
- `AI_MODEL_NAME`: model name used for OpenAI Responses API requests
- `LINKEDIN_CLIENT_ID`: LinkedIn OAuth client id
- `LINKEDIN_CLIENT_SECRET`: LinkedIn OAuth client secret
- `LINKEDIN_REDIRECT_URI`: LinkedIn OAuth callback URL
- `LINKEDIN_ACCESS_TOKEN`: optional fallback access token
- `LINKEDIN_MEMBER_URN`: optional fallback member URN

For local execution with secrets outside the repository:

- copy [.env.local.example](/c:/PublishNews/.env.local.example) to `.env.local`
- fill in the required values
- use [run-api-local.sh](/c:/PublishNews/scripts/run-api-local.sh)

## Local Run

```bash
dotnet build PublishNews.sln
dotnet test PublishNews.sln
dotnet run --project src/AiNewsCurator.Api
```

In another terminal:

```bash
dotnet run --project src/AiNewsCurator.Worker
```

## Ops Dashboard

`GET /ops` provides the internal operational dashboard. It uses the same `INTERNAL_API_KEY`, but through a simple login screen.

The dashboard supports:

- running daily, collect, curate, and normalize flows
- reviewing drafts in editorial or feed preview mode
- editing draft title and post text
- approving, rejecting, dismissing, reopening, publishing, and retrying failed drafts
- viewing LinkedIn validation and refresh actions
- searching drafts, news, and sources
- paginating drafts, news, sources, and runs independently
- managing sources
- adding a manual image URL when a news item has no captured image
- preserving current filter/page context after actions

### Draft Status Notes

- `PendingApproval`: waiting for review
- `Approved`: approved and ready to publish
- `Dismissed`: intentionally removed from the review queue without rejection
- `Rejected`: explicitly rejected
- `Failed`: publish attempt failed
- `Published`: successfully published

### LinkedIn Publish Notes

- Final post text includes `Source: ...`
- Final post text also includes `Original article: https://...`
- If an older draft was created before the article-link format existed, the publish pipeline repairs the draft text before sending it to LinkedIn
- If a news item has an image, the publisher attempts an image upload and falls back to text-only on upload failure

## Internal Endpoints

All `/internal/*` endpoints require `X-API-Key`.

- `GET /health`
- `POST /internal/run/daily`
- `POST /internal/run/collect`
- `POST /internal/run/curate`
- `POST /internal/run/publish/{draftId}`
- `GET /internal/drafts`
- `GET /internal/news`
- `GET /internal/news/{id}`
- `POST /internal/news/{id}/reprocess`
- `GET /internal/sources`
- `POST /internal/sources`
- `PUT /internal/sources/{id}`
- `POST /internal/sources/{id}/deactivate`
- `GET /internal/auth/linkedin/status`
- `POST /internal/auth/linkedin/start`
- `POST /internal/auth/linkedin/validate`
- `POST /internal/auth/linkedin/refresh`
- `GET /internal/auth/linkedin/callback`
- `POST /internal/drafts/{id}/approve`
- `POST /internal/drafts/{id}/reject`
- `POST /internal/drafts/{id}/dismiss`
- `POST /internal/drafts/{id}/reopen`
- `GET /internal/runs`

Example:

```bash
curl -X POST http://localhost:5138/internal/run/daily -H "X-API-Key: changeme"
```

To start local LinkedIn OAuth:

```bash
curl -X POST http://localhost:5138/internal/auth/linkedin/start -H "X-API-Key: changeme"
```

Open the returned URL in a browser. The expected local callback is:

```text
http://localhost:5138/internal/auth/linkedin/callback
```

After the callback completes, the application redirects the browser to `/ops`.

## Deploy on Render

The repository includes [render.yaml](/c:/PublishNews/render.yaml) with the recommended SQLite MVP topology: a single web service hosting both the API and the internal scheduler in the same process.

Why this is recommended:

- on Render, persistent disks are attached per service
- with SQLite, separating API and worker into different services can lead to each process seeing a different filesystem context

Recommended steps:

1. Create the service from `render.yaml`.
2. Keep `ENABLE_SCHEDULER=true` so the daily routine runs inside the web service.
3. Configure secrets such as `INTERNAL_API_KEY`, `AI_API_KEY`, `LINKEDIN_CLIENT_ID`, `LINKEDIN_CLIENT_SECRET`, and `LINKEDIN_REDIRECT_URI`.
4. Confirm the persistent disk mount path, for example `/var/data/ainews`.
5. Validate the healthcheck on `/health`.

Do not use a separate Render cron job against a local SQLite file for this topology.

## Docker

The repository includes [Dockerfile](/c:/PublishNews/Dockerfile) and [.dockerignore](/c:/PublishNews/.dockerignore) for containerized deployment of the API.

Build locally:

```bash
docker build -t ai-news-curator .
```

Run locally:

```bash
docker run --rm -p 8080:8080 \
  -e INTERNAL_API_KEY=changeme \
  -e DATABASE_PATH=/var/data/ainews/ainews.db \
  ai-news-curator
```

## Test Coverage

The project includes both unit and integration coverage for important flows, including:

- LinkedIn token refresh behavior
- LinkedIn credential validation retry after expired token
- editorial post formatting and article-link parsing
- RSS summary sanitization with image extraction
- collection, curation, publishing, dismiss/reopen, retry publish, and source repository flows
- manual image persistence on news items

Run the full suite with:

```bash
dotnet test PublishNews.sln
```

## Current Limitations

- SQLite is still the persistence layer, so horizontal scaling is intentionally limited
- AI curation defaults to the local heuristic provider unless `AI_PROVIDER=OpenAI`
- semantic similarity and richer duplicate detection are still basic
- LinkedIn publishing currently posts text plus optional uploaded image, not a full external article card/embed
- existing stored news items with old HTML-heavy summaries may need `Normalize news` or reprocessing to clean previously ingested content
