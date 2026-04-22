# Time Tracking Submission — Design Spec

**Date:** 2026-04-22
**Status:** Approved

## Overview

A minimal API endpoint that records the latest UTC timestamp when a user submits data, backed by a Redis sorted set for efficient interval and range queries.

## Architecture

Single project: `TrackingService` (.NET 10 minimal API). Add `StackExchange.Redis` as the Redis client, registered as a singleton. Redis logic is isolated behind `ITimeTrackingRepository` / `RedisTimeTrackingRepository`. Routes registered in `Program.cs`.

## Redis Data Model

**Key:** `last_submitted` (global sorted set)
**Member:** `userId` (string)
**Score:** Unix timestamp (UTC, seconds)

Each user has exactly one entry — `ZADD` updates the score on re-submission.

## Endpoints

### `POST /submissions`
Records the current UTC time for a user.

**Request body:**
```json
{ "userId": "string" }
```

**Responses:**
- `204 No Content` — success
- `400 Bad Request` — missing or empty `userId`

### `GET /submissions/{userId}`
Returns the latest submission timestamp for a specific user.

**Response (200):**
```json
{ "userId": "string", "lastSubmittedAt": "2026-04-22T10:30:00Z" }
```
- `404 Not Found` — user has no submission recorded

### `GET /submissions`
Returns all users who submitted within an optional time interval.

**Query params:** `from` (ISO 8601, optional), `to` (ISO 8601, optional). Omitting both returns all users.

**Response (200):**
```json
[
  { "userId": "string", "lastSubmittedAt": "2026-04-22T10:30:00Z" }
]
```
- `400 Bad Request` — invalid `from`/`to` format

## Data Flow

- `POST /submissions` → parse userId → `ZADD last_submitted <unix_ts> <userId>` → 204
- `GET /submissions/{userId}` → `ZSCORE last_submitted <userId>` → ISO 8601 or 404
- `GET /submissions` → `ZRANGEBYSCORE last_submitted <from|-inf> <to|+inf>` → array of results

## Error Handling

- Missing/empty `userId` → 400
- Invalid date format on query params → 400
- Redis connection failure → propagate as 500 (no silent swallowing)

## Out of Scope

- Authentication/authorization
- Persisting the submitted payload (endpoint only records the timestamp)
- Unit tests (integration testing via `.http` file is sufficient for this phase)
