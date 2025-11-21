# Prepared

A real-time call processing and intelligence extraction system that demonstrates enterprise-grade software engineering practices. This application processes incoming phone calls, performs live transcription, extracts actionable insights using AI, and visualizes location data on an interactive map—all in real-time.

## Demo

### Video Walkthrough
Watch the complete process in action:

https://preparedproduction.blob.core.windows.net/public/Demo.mp4

### Screenshot

![Screenshot after receiving a call](https://preparedproduction.blob.core.windows.net/public/Demo.jpg)

## What It Does

Prepared is a full-stack application designed to handle emergency dispatch scenarios or call center operations. When a call comes in:

1. **Call Reception**: Twilio receives the call and establishes a WebSocket connection to stream audio in real-time
2. **Live Transcription**: Audio chunks are buffered and sent to OpenAI's Whisper API for real-time transcription
3. **Real-Time Updates**: Transcripts are pushed to connected web clients via SignalR as they're generated
4. **Intelligence Extraction**: Once sufficient context is gathered, the system uses GPT to extract:
   - Location information (addresses, coordinates)
   - Incident summaries
   - Key findings and urgency indicators
5. **Visualization**: Extracted locations are automatically plotted on an interactive Google Maps interface

The entire pipeline operates with sub-second latency, making it suitable for time-sensitive applications where immediate information extraction is critical.

## Architecture

This project follows **Clean Architecture** principles with clear separation of concerns:

```
┌─────────────────────────────────────────────────────────┐
│  Prepared.Client (ASP.NET Core MVC)                     │
│  - Controllers, Views, SignalR Hubs                     │
│  - WebSocket handlers for Twilio Media Streams          │
│  - Middleware (Security, Rate Limiting, CORS)           │
└──────────────────┬──────────────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────────────┐
│  Prepared.Business (Business Logic Layer)               │
│  - TwilioService: Call handling & TwiML generation      │
│  - MediaStreamService: WebSocket audio processing       │
│  - WhisperTranscriptionService: Real-time transcription │
│  - UnifiedInsightsService: AI-powered extraction        │
│  - Repository interfaces                                │
└──────────────────┬──────────────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────────────┐
│  Prepared.Data (Data Access Layer)                      │
│  - Azure Table Storage repositories                     │
│  - Entity models and data services                      │
└──────────────────┬──────────────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────────────┐
│  Prepared.Common (Shared Domain Models)                 │
│  - DTOs, Enums, Interfaces, Utilities                   │
└─────────────────────────────────────────────────────────┘
```

### Key Design Patterns

- **Repository Pattern**: Abstracted data access for testability and flexibility
- **Dependency Injection**: Full DI container usage throughout
- **Interface Segregation**: Small, focused interfaces following SOLID principles
- **Strategy Pattern**: Pluggable services (transcription, location extraction)
- **Observer Pattern**: SignalR hubs for real-time event broadcasting

## Technology Stack

### Backend
- **.NET 10.0** - Latest framework with performance improvements
- **ASP.NET Core MVC** - Web application framework
- **SignalR** - Real-time bidirectional communication
- **Azure Data Tables** - NoSQL storage for call records and transcripts
- **.NET Aspire** - Cloud-native orchestration and service defaults

### External Services & APIs
- **Twilio** - Voice call handling and Media Streams (WebSocket-based audio streaming)
- **OpenAI Whisper API** - Real-time speech-to-text transcription
- **OpenAI GPT API** - Natural language understanding for location extraction and summarization
- **Google Maps JavaScript API** - Interactive mapping and geocoding

### Frontend
- **Vanilla JavaScript** - Modern ES6+ with async/await patterns
- **Webpack 5** - Module bundling and asset pipeline
- **Babel** - ES6+ transpilation for browser compatibility
- **SignalR JavaScript Client** - Real-time connection management

### Testing & Quality
- **xUnit** - Unit testing framework
- **FluentAssertions** - Readable test assertions
- **Moq** - Mocking framework for isolated unit tests
- **Coverlet** - Code coverage collection
- **FluentValidation** - Input validation with strong typing

### DevOps & Infrastructure
- **Docker** - Containerization support
- **Sentry** - Error tracking and performance monitoring
- **PowerShell Scripts** - Automated test coverage reporting

## Technical Skills Demonstrated

### Real-Time Systems
- **WebSocket Programming**: Handling Twilio Media Streams with binary audio data (μ-law encoding)
- **Audio Buffering**: Intelligent chunking strategies to balance latency vs. transcription accuracy
- **Concurrent Processing**: Managing multiple simultaneous calls with async/await patterns
- **SignalR Integration**: Real-time push notifications to multiple connected clients

### AI/ML Integration
- **API Integration**: Consuming OpenAI's REST APIs with proper error handling and retry logic
- **Prompt Engineering**: Structured prompts for consistent JSON extraction from GPT responses
- **Cost Optimization**: Unified API calls to reduce token usage and latency
- **Model Selection**: Support for different GPT models (including reasoning models like o1) based on use case

### Cloud Architecture
- **Azure Integration**: Azure Table Storage for scalable, serverless data persistence
- **Service Orchestration**: .NET Aspire for cloud-native application hosting
- **Configuration Management**: Environment-based configuration with validation

### Security & Performance
- **Webhook Security**: Twilio signature validation to prevent unauthorized requests
- **CSRF Protection**: Anti-forgery tokens and secure cookie policies
- **Rate Limiting**: Custom middleware for API endpoint protection
- **Security Headers**: Comprehensive HTTP security headers (CSP, HSTS, X-Frame-Options, etc.)
- **CORS Management**: Configurable cross-origin resource sharing
- **Response Caching**: Static asset caching with proper cache-control headers
- **Async Patterns**: Non-blocking I/O throughout for scalability

### Software Engineering Practices
- **Clean Architecture**: Layered architecture with dependency inversion
- **SOLID Principles**: Single responsibility, dependency injection, interface segregation
- **Test-Driven Development**: Comprehensive unit test coverage across all layers
- **Error Handling**: Graceful degradation and comprehensive error logging
- **Observability**: Structured logging, health checks, and error tracking integration
- **Code Quality**: Nullable reference types, input validation, and defensive programming

### Frontend Development
- **Modern JavaScript**: ES6+ features, async/await, modules
- **Build Tooling**: Webpack configuration for development and production builds
- **Real-Time UI**: Dynamic DOM updates based on SignalR events
- **Map Integration**: Google Maps API with custom markers and info windows

## Project Structure

### Solution Projects
- **Prepared.Client**: ASP.NET Core MVC web application (Frontend + API endpoints)
- **Prepared.Business**: Business logic layer with services and interfaces
- **Prepared.Data**: Data access layer with Azure Table Storage repositories
- **Prepared.Common**: Shared models, enums, interfaces, and utilities
- **Prepared.ServiceDefaults**: .NET Aspire service defaults and configuration
- **Prepared.AppHost**: Aspire application host for orchestration

### Test Projects
- **Prepared.Client.Tests**: MVC controllers, middleware, SignalR hubs
- **Prepared.Business.Tests**: Business logic services and extensions
- **Prepared.Data.Tests**: Repository implementations and data access
- **Prepared.Common.Tests**: Shared utilities and models

### Supporting Files
- **`scripts/`**: PowerShell scripts for test coverage and automation
- **`docs/`**: Additional documentation (coverage guides, architecture notes)
- **`.runsettings`**: Test execution and coverage configuration

## Getting Started

### Prerequisites
- .NET 10.0 SDK
- Node.js and npm (for frontend build)
- Azure account (for Table Storage)
- API keys for: Twilio, OpenAI, Google Maps

### Setup

**1. Install Frontend Dependencies:**
```bash
cd Prepared.Client
npm install
```

**2. Build Frontend Assets:**
```bash
# Development (watch mode)
npm run dev

# Production build
npm run build
```

**3. Configure API Keys:**

Set the following in `appsettings.Development.json` or user secrets:

```json
{
  "GoogleMaps": {
    "ApiKey": "your-google-maps-api-key"
  },
  "Twilio": {
    "AccountSid": "your-account-sid",
    "KeySid": "your-api-key-sid",
    "SecretKey": "your-api-secret",
    "AuthToken": "your-auth-token",
    "WebhookUrl": "https://your-domain.com/api/twilio/webhook"
  },
  "Whisper": {
    "ApiKey": "your-openai-api-key"
  },
  "OpenAI": {
    "ApiKey": "your-openai-api-key",
    "DefaultModel": "gpt-4o-mini"
  }
}
```

**4. Run the Application:**
```bash
dotnet run --project Prepared.AppHost
```

Navigate to `/Calls` to view the real-time call monitoring dashboard.

### Running Tests

**Visual Studio:**
- Right-click solution → **"Analyze Code Coverage for All Tests"**
- Or: **Test** → **Analyze Code Coverage** → **All Tests**

**Command Line:**
```powershell
.\scripts\test-coverage.ps1
```

See [docs/COVERAGE.md](docs/COVERAGE.md) for detailed coverage instructions.

**Frontend Development:**
See [Prepared.Client/README.md](Prepared.Client/README.md) for webpack setup and development workflow.

## Engineering Standards

This codebase demonstrates production-ready engineering practices:

- ✅ **Clean Architecture** - Separation of concerns with clear layer boundaries
- ✅ **SOLID Principles** - Maintainable, extensible, and testable code
- ✅ **Comprehensive Testing** - Unit tests with high coverage across all layers
- ✅ **Security First** - Security headers, rate limiting, CSRF protection, input validation, webhook signature verification
- ✅ **Performance Optimization** - Caching, efficient data access, async/await patterns, audio buffering strategies
- ✅ **Observability** - Structured logging, error tracking (Sentry), health checks
- ✅ **Code Quality** - Consistent patterns, documentation, nullable reference types
- ✅ **Production Ready** - Error handling, resilience patterns, monitoring, graceful degradation
- ✅ **Best Practices** - Dependency injection, configuration management, middleware patterns

## Why This Project

This project showcases the ability to build complex, real-time systems that integrate multiple external services while maintaining code quality, testability, and production readiness. It demonstrates:

- **Full-Stack Capability**: From low-level WebSocket handling to high-level AI integration
- **Real-World Complexity**: Handling edge cases, error scenarios, and performance considerations
- **Modern Practices**: Using latest .NET features, cloud-native patterns, and industry-standard tooling
- **Production Mindset**: Security, observability, and maintainability are built in from the start

The architecture is designed to scale, the code is designed to be maintained, and the system is designed to be reliable.
