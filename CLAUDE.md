# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

`TrackSubmittedTime` is a .NET 10 ASP.NET Core minimal API solution. Currently in its initial scaffold state — the goal is to build a service for tracking submitted time entries.

## Commands

```bash
# Run the service (from solution root or TrackingService/)
dotnet run --project TrackingService

# Build
dotnet build

# Watch mode (auto-reload on file changes)
dotnet watch --project TrackingService run
```

The service runs on `http://localhost:5228` (HTTP) or `https://localhost:7163` (HTTPS) in Development.

OpenAPI docs are available at `/openapi/v1.json` when running in Development.

## Architecture

Single project solution (`TrackingService`) using .NET 10 minimal API pattern (`Program.cs` as entry point, no controllers). Nullable reference types and implicit usings are enabled.

- `Program.cs` — service setup and route registration
- `appsettings.json` / `appsettings.Development.json` — environment configuration
- `TrackingService.http` — HTTP request scratch file for manual endpoint testing
