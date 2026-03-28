# AI News Curator System Documentation

## Purpose

AI News Curator is a .NET 8 system that collects AI and developer news from curated sources, evaluates relevance, turns selected items into LinkedIn-ready editorial drafts, and supports manual or automatic publishing workflows.

The system is designed for:

- internal editorial operations through `/ops`
- machine-triggered orchestration through `/internal/*`
- lightweight deployment with SQLite
- mixed coverage of `General AI` and `.NET / C#` editorial lanes

## High-Level Architecture

The solution is split into five main projects:

- `src/AiNewsCurator.Api`
  Internal HTTP API, LinkedIn OAuth callback endpoints, middleware, and the `/ops` operational dashboard.
- `src/AiNewsCurator.Application`
  Orchestration logic, editorial formatting/refinement, validation, and scheduler behavior.
- `src/AiNewsCurator.Domain`
  Core entities, enums, interfaces, and normalization/hash rules.
- `src/AiNewsCurator.Infrastructure`
  SQLite persistence, RSS collection, AI integrations, LinkedIn integrations, and image enrichment.
- `src/AiNewsCurator.Worker`
  Background host used for scheduled execution outside the API process when needed.

## End-to-End Lifecycle

The normal lifecycle of a news item is:

1. Source configuration is loaded from SQLite.
2. Active sources are collected by registered collectors.
3. URLs are normalized and duplicate checks are applied.
4. Collected items are stored as `NewsItem` rows.
5. Candidate items are evaluated by the AI curation layer.
6. Relevant items become `CurationResult` rows.
7. Draft text is generated, refined, validated, and stored as `PostDraft`.
8. The draft is either:
   - left in `PendingApproval`
   - auto-promoted to `Approved`
   - or later moved to `Published`, `Rejected`, `Dismissed`, or `Failed`
9. A LinkedIn publication attempt records a `Publication` row with request/response payloads.

## Main Runtime Flows

### 1. Source Collection

Collection starts from active `Source` records.

Important responsibilities:

- choose the correct `INewsCollector`
- enforce `MaxItemsPerRun`
- normalize canonical URLs
- compute `TitleHash` and `ContentHash`
- avoid duplicates already seen by canonical URL

Current default collector:

- `RssNewsCollector`

### 2. Curation

`NewsPipelineService` asks `IAiCurationService` to evaluate candidates.

Two curation providers are supported:

- `HeuristicAiCurationService`
  Local rule-based fallback and default low-dependency path.
- `OpenAiResponsesAiCurationService`
  Structured JSON generation through OpenAI Responses API.

Both providers now support source-aware editorial profiles:

- `ai`
- `dotnet`

Profile resolution uses:

- source name
- source URL
- source tags
- source include keywords
- item title/summary/content

### 3. Draft Generation

Drafts are normalized into a shared editorial structure:

- `Headline`
- `Hook`
- `WhatHappened`
- `WhyItMatters`
- `StrategicTakeaway`
- `SourceLabel`
- `Hashtags`
- `OriginalArticleUrl`
- `Signature`

Key services involved:

- `LinkedInEditorialPostFormatter`
- `LinkedInEditorialRefiner`
- `LinkedInEditorialQualityAnalyzer`
- `PostQualityValidator`

Generated post text includes:

- structured editorial sections
- AI-generated or heuristic hashtags
- original article link
- attribution/signature

### 4. Approval and Publishing

Publishing is controlled by `PUBLISH_MODE`:

- `Manual`
  Drafts remain in the review queue until approved and published.
- `Automatic`
  High-confidence valid drafts can be auto-approved and then auto-published.

Publishing behavior:

- validates LinkedIn credentials first
- repairs legacy drafts missing `Original article: ...`
- records request and response payloads
- persists success or failure in `Publication`
- updates `PostDraft.Status`

If LinkedIn validation fails:

- publication is recorded as failed
- the draft moves to `Failed`

If image upload fails:

- the publisher falls back to text-only post publication

## Operational Dashboard

`/ops` is the main human workflow surface.

Current capabilities:

- login with email-based one-time codes for approved ops users
- run daily / collect / curate / normalize actions
- inspect LinkedIn connection state
- trigger LinkedIn validation and token refresh
- review drafts in editorial or feed preview mode
- edit draft title and post text
- approve, reject, dismiss, reopen, regenerate, publish, and retry drafts
- inspect publication audit payloads
- inspect publication attempt history
- search drafts, news, and sources
- paginate drafts, news, sources, and runs independently
- filter drafts and news by editorial profile
- manage sources
- manually add image URLs
- preserve current page/filter context after actions
- show loading feedback during POST actions

### Draft States

- `Generated`
  Internal initial state before review routing.
- `PendingApproval`
  Waiting for operator review.
- `Approved`
  Approved for publication.
- `Rejected`
  Explicitly not accepted.
- `Dismissed`
  Removed from the queue without final rejection.
- `Failed`
  Publish attempt failed.
- `Published`
  Successfully published.

## Internal API

The internal API exists for orchestration, automation, and machine-facing operations.

Security model:

- all `/internal/*` routes require `X-API-Key`
- exception: LinkedIn OAuth callback path is allowed through middleware

Main endpoint groups:

- run control
- draft actions
- news reprocessing
- source management
- LinkedIn auth and validation
- runs/news/drafts listing

Source responses now expose:

- the raw `Source`
- `editorialProfile`
- `editorialProfileLabel`

`GET /internal/sources` also supports:

- `?profile=all`
- `?profile=ai`
- `?profile=dotnet`

## Security Model

### Internal API Security

`InternalApiKeyMiddleware` protects `/internal/*`.

Behavior:

- callback path bypass for LinkedIn OAuth
- all other internal routes require exact `X-API-Key`
- unauthorized requests receive `401` JSON response

### Ops Security

`OperationsAccessMiddleware` protects `/ops`.

Behavior:

- `/ops/login` is always reachable
- `/ops/auth/*` endpoints stay reachable for request/verify/logout
- authenticated ops access uses a secure cookie session created after email-code verification
- unauthorized requests are redirected to login with `returnUrl`

Ops auth rules:

- only approved emails from `OpsUsers` may sign in
- login codes are 6 digits, single-use, and hashed with SHA-256 before storage
- default code lifetime is 10 minutes
- issuing a new code invalidates any previous unused code for that user
- one bootstrap user can be seeded from `OPS_BOOTSTRAP_EMAIL` and `OPS_BOOTSTRAP_NAME`

## LinkedIn Integration

LinkedIn behavior is split into:

- `LinkedInAuthService`
  OAuth, token persistence, callback handling, current auth status.
- `LinkedInPublisher`
  validation, refresh token flow, media upload, and publish requests.

Important behavior:

- callback always returns the browser to `/ops`
- access-token refresh is supported
- validation can retry after refresh when needed
- publication audit data is stored and visible in `/ops`

## Source Profiles

The system supports source-aware editorial lanes.

### Supported lanes

- `General AI`
- `.NET / C#`

### Where profile data is used

- AI prompt guidance
- heuristic relevance scoring
- category framing
- ops filtering
- internal source API responses
- source create/edit presets

### Source profile hinting

Source create/edit supports:

- `ai`
- `dotnet`
- `auto`

This is a non-destructive hint:

- it merges profile-specific tags and include keywords into the current request
- it does not automatically preserve all historical tags from older saved versions during update
- the resolved profile may still remain `.NET / C#` based on source name/URL even if the latest saved tags are more generic

## Data Model Overview

### `Source`

Represents an ingest source.

Important fields:

- `Name`
- `Type`
- `Url`
- `IsActive`
- `Priority`
- `MaxItemsPerRun`
- `IncludeKeywordsJson`
- `ExcludeKeywordsJson`
- `TagsJson`

### `NewsItem`

Represents a collected news story.

Important fields:

- source linkage
- canonical URL
- optional image URL/origin
- summary/content
- title/content hashes
- current status

### `CurationResult`

Represents a relevance decision and editorial metadata for a news item.

Important fields:

- relevance
- confidence
- category
- rationale
- key points
- prompt/model metadata

### `PostDraft`

Represents the editorial LinkedIn draft.

Important fields:

- title suggestion
- post text
- tone
- status
- validation errors
- approval metadata

### `Publication`

Represents a publish attempt.

Important fields:

- platform
- platform post id
- request payload
- response payload
- status
- error message

### `ExecutionRun`

Represents an orchestrated collection/curation/publish run.

Important fields:

- trigger type
- counts for collect/curate/publish/errors
- status
- timestamps

## Current Testing Strategy

The project currently uses two layers:

### Unit tests

Focused on pure logic and lightweight web-layer behavior.

Examples include:

- URL normalization
- RSS summary sanitization
- OpenAI response parsing
- editorial formatter/parsing
- editorial quality analyzer
- source input mapper
- auth cookie logic
- ops view models
- controllers and middleware
- `NewsPipelineService`

### Integration tests

Use real SQLite-backed repositories to verify end-to-end pipeline and API behavior.

Examples include:

- collect + curate + publish flows
- draft state transitions
- source API profile behavior
- retry publish behavior
- article-link repair before publish

## Deployment Model

The recommended MVP deployment is a single service with SQLite and scheduler enabled in the same process environment.

Why:

- SQLite is file-based
- splitting writer processes across services creates risk around storage semantics and coordination

Recommended production shape today:

- one Render service
- persistent disk mounted to a stable path
- scheduler enabled
- internal API key configured
- LinkedIn OAuth secrets configured

## Configuration Summary

Most important runtime settings:

- `DATABASE_PATH`
- `PUBLISH_MODE`
- `INTERNAL_API_KEY`
- `AI_PROVIDER`
- `AI_API_KEY`
- `AI_MODEL_NAME`
- `LINKEDIN_CLIENT_ID`
- `LINKEDIN_CLIENT_SECRET`
- `LINKEDIN_REDIRECT_URI`
- `LINKEDIN_ACCESS_TOKEN`
- `LINKEDIN_MEMBER_URN`
- `ATTRIBUTION_FOOTER_LINE`

## Extension Points

The easiest ways to extend the system are:

- add more `INewsCollector` implementations
- add new editorial profiles
- expand `/ops` filters and analytics
- improve image validation and preview
- add richer publish targets beyond LinkedIn
- replace SQLite with another persistence layer if horizontal scale becomes necessary

## Known Limitations

- not every file has dedicated unit tests yet
- SQLite still limits horizontal scale
- similarity detection is still basic
- no browser-level UI automation currently exists for `/ops`
- LinkedIn publishing is text-plus-optional-uploaded-image, not a native external article card
- some source-profile behavior is intentionally additive rather than destructive on update

## Recommended Reading Order

For a new developer or operator:

1. [README.md](/c:/PublishNews/README.md)
2. [system-overview.md](/c:/PublishNews/docs/system-overview.md)
3. [NewsPipelineService.cs](/c:/PublishNews/src/AiNewsCurator.Application/Services/NewsPipelineService.cs)
4. [OperationsController.cs](/c:/PublishNews/src/AiNewsCurator.Api/Controllers/OperationsController.cs)
5. [InternalRunsController.cs](/c:/PublishNews/src/AiNewsCurator.Api/Controllers/InternalRunsController.cs)
6. [LinkedInPublisher.cs](/c:/PublishNews/src/AiNewsCurator.Infrastructure/Integrations/LinkedIn/LinkedInPublisher.cs)
