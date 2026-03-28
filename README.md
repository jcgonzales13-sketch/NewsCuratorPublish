# AI News Curator for LinkedIn

A .NET 8 application that collects AI and developer news from RSS feeds, stores it in SQLite, curates it with AI-assisted workflows, generates LinkedIn-ready drafts in English, and supports both manual and automatic publishing modes.

For full system documentation, see [docs/system-overview.md](/c:/PublishNews/docs/system-overview.md).

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
- Editorial profile hints for sources in `/ops` and the internal source API
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
- `INTERNAL_API_KEY`: required for `/internal/*` endpoints
- `OPS_AUTH_MODE`: set to `EmailCode` for `/ops`
- `OPS_BOOTSTRAP_EMAIL`: first approved email seeded into `OpsUsers`
- `OPS_BOOTSTRAP_NAME`: display name for the bootstrap ops user
- `OPS_SESSION_COOKIE_NAME`: cookie name used for the `/ops` browser session
- `OPS_LOGIN_CODE_TTL_MINUTES`: one-time login code lifetime
- `OPS_LOGIN_MAX_VERIFY_ATTEMPTS`: maximum verification tries per code
- `SMTP_HOST`, `SMTP_PORT`, `SMTP_SENDER_NAME`, `SMTP_SENDER_EMAIL`, `SMTP_USERNAME`, `SMTP_PASSWORD`, `SMTP_USE_STARTTLS`: Gmail SMTP delivery settings for ops login codes
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

`GET /ops` provides the internal operational dashboard. It now uses email-based one-time login codes for approved users, backed by a secure cookie session.

Ops auth flow:

1. Open `/ops` or `/ops/login`
2. Enter an approved email address
3. Receive a 6-digit login code by email
4. Verify the code
5. Access `/ops` with an authenticated cookie session for up to 8 hours

Important auth notes:

- only approved emails in `OpsUsers` can sign in
- one bootstrap ops user can be created automatically from `OPS_BOOTSTRAP_EMAIL`
- raw login codes are never stored
- `/internal/*` continues to use `X-API-Key`
- logout is handled by `POST /ops/auth/logout`

The dashboard supports:

- running daily, collect, curate, and normalize flows
- reviewing drafts in editorial or feed preview mode
- editing draft title and post text
- approving, rejecting, dismissing, reopening, publishing, and retrying failed drafts
- viewing LinkedIn validation and refresh actions
- searching drafts, news, and sources
- filtering drafts and news by editorial profile
- paginating drafts, news, sources, and runs independently
- managing sources
- selecting `General AI`, `.NET / C#`, or `Auto detect` when creating or editing sources
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

Ops auth endpoints:

- `GET /ops/login`
- `POST /ops/auth/request-code`
- `POST /ops/auth/verify-code`
- `POST /ops/auth/logout`

Example:

```bash
curl -X POST http://localhost:5138/internal/run/daily -H "X-API-Key: changeme"
```

Create a source with an explicit editorial lane:

```bash
curl -X POST http://localhost:5138/internal/sources \
  -H "X-API-Key: changeme" \
  -H "Content-Type: application/json" \
  -d '{
    "editorialProfile": "dotnet",
    "name": ".NET Blog",
    "type": "Rss",
    "url": "https://devblogs.microsoft.com/dotnet/feed/",
    "language": "en",
    "isActive": true,
    "priority": 8,
    "maxItemsPerRun": 10,
    "includeKeywords": ["blazor"],
    "excludeKeywords": [],
    "tags": ["official"]
  }'
```

Update a source and keep the profile explicit:

```bash
curl -X PUT http://localhost:5138/internal/sources/1 \
  -H "X-API-Key: changeme" \
  -H "Content-Type: application/json" \
  -d '{
    "editorialProfile": "ai",
    "name": "OpenAI News",
    "type": "Rss",
    "url": "https://openai.com/news/rss.xml",
    "language": "en",
    "isActive": true,
    "priority": 10,
    "maxItemsPerRun": 10,
    "includeKeywords": ["workflow"],
    "excludeKeywords": [],
    "tags": ["official"]
  }'
```

`editorialProfile` accepts:

- `ai`
- `dotnet`
- `auto`

The API and `/ops` use this as a non-destructive hint. It merges profile-specific tags and include-keywords into whatever you already send instead of overwriting custom source settings.

Source endpoints now return a response shape like this:

```json
{
  "source": {
    "id": 7,
    "name": ".NET Blog",
    "type": "Rss",
    "url": "https://devblogs.microsoft.com/dotnet/feed/",
    "language": "en",
    "isActive": true,
    "priority": 8,
    "maxItemsPerRun": 10,
    "includeKeywordsJson": "[\"blazor\",\".net\",\"dotnet\",\"c#\",\"asp.net core\",\"runtime\",\"sdk\"]",
    "excludeKeywordsJson": "[]",
    "tagsJson": "[\"official\",\"dotnet\",\"csharp\"]",
    "createdAt": "2026-03-27T12:00:00.0000000+00:00",
    "updatedAt": "2026-03-27T12:00:00.0000000+00:00"
  },
  "editorialProfile": "dotnet",
  "editorialProfileLabel": ".NET / C#"
}
```

`GET /internal/sources` returns a list of that same object shape.

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
3. Configure secrets such as `INTERNAL_API_KEY`, `OPS_BOOTSTRAP_EMAIL`, `SMTP_USERNAME`, `SMTP_PASSWORD`, `AI_API_KEY`, `LINKEDIN_CLIENT_ID`, `LINKEDIN_CLIENT_SECRET`, and `LINKEDIN_REDIRECT_URI`.
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
  -e OPS_AUTH_MODE=EmailCode \
  -e OPS_BOOTSTRAP_EMAIL=ops@example.com \
  -e SMTP_HOST=smtp.gmail.com \
  -e SMTP_PORT=587 \
  -e SMTP_SENDER_NAME="AI News Curator" \
  -e SMTP_SENDER_EMAIL=youraccount@gmail.com \
  -e SMTP_USERNAME=youraccount@gmail.com \
  -e SMTP_PASSWORD=your_google_app_password \
  -e SMTP_USE_STARTTLS=true \
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
- internal source API create/update behavior for editorial profile presets

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
