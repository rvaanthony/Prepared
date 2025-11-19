# Prepared

## Overview

Prepared is a real-time call processing application that receives phone calls via Twilio, transcribes audio in real-time, extracts location information, and visualizes it on an interactive map.

> **Note**: This project is built to **Principal Engineer standards**, demonstrating enterprise-grade architecture, best practices, clean code principles, comprehensive testing, and production-ready implementation patterns. Every aspect of this codebase reflects the quality and technical excellence expected at the highest engineering levels.

## High-Level Architecture Flow

```
Caller dials → Twilio handles call → Streams audio to your app → 
Transcribe in real time → Update browser with transcript →
Summarize + extract location → Drop pin on map in UI
```

### Process Flow

1. **Caller dials** → Twilio receives the call
2. **Twilio handles call** → Routes to your application
3. **Streams audio to your app** → Real-time audio stream processing
4. **Transcribe in real time** → Live transcription of the audio
5. **Update browser with transcript** → Real-time UI updates via WebSocket/SSE
6. **Summarize + extract location** → Process transcript to find location data
7. **Drop pin on map in UI** → Visualize location on an interactive map

## Project Structure

### Solution Projects
- **Prepared.Client**: ASP.NET Core MVC web application (Frontend)
- **Prepared.Business**: Business logic layer with services and interfaces (class lib)
- **Prepared.Data**: Data access layer with repositories and entities (class lib)
- **Prepared.Common**: Shared models, enums, interfaces, and utilities (class lib)
- **Prepared.ServiceDefaults**: Aspire service defaults
- **Prepared.AppHost**: Aspire application host

### Test Projects
- **Prepared.Client.Tests**: Tests for the MVC web application
- **Prepared.Business.Tests**: Tests for business logic layer
- **Prepared.Data.Tests**: Tests for data access layer
- **Prepared.Common.Tests**: Tests for shared components

### Project Organization
- **`scripts/`**: Build, test, and utility scripts
- **`docs/`**: Additional documentation (coverage guides, architecture docs, etc.)
- **`.runsettings`**: Test and coverage configuration

## Technology Stack

- .NET 10.0
- ASP.NET Core MVC
- xUnit (Testing)
- Twilio (Call handling and audio streaming)
- Real-time transcription
- Interactive mapping

## Engineering Standards

This project adheres to **Principal Engineer-level** standards:

- ✅ **Clean Architecture** - Separation of concerns with clear layer boundaries
- ✅ **SOLID Principles** - Maintainable, extensible, and testable code
- ✅ **Comprehensive Testing** - Unit tests, integration tests, and test coverage
- ✅ **Security First** - Security headers, rate limiting, CSRF protection, input validation
- ✅ **Performance Optimization** - Caching, efficient data access, async/await patterns
- ✅ **Observability** - Structured logging, error tracking (Sentry), health checks
- ✅ **Code Quality** - Consistent patterns, documentation, and maintainability
- ✅ **Production Ready** - Error handling, resilience patterns, monitoring
- ✅ **Best Practices** - Dependency injection, configuration management, middleware patterns

## Code Coverage

This project includes comprehensive code coverage reporting to ensure quality and maintainability.

### Quick Start

**Visual Studio 2026:**
- Right-click solution → **"Analyze Code Coverage for All Tests"**
- Or: **Test** → **Analyze Code Coverage** → **All Tests**
- View results in the **Code Coverage Results** window

**Command Line:**
```powershell
.\scripts\test-coverage.ps1
```

See [docs/COVERAGE.md](docs/COVERAGE.md) for detailed instructions and troubleshooting.