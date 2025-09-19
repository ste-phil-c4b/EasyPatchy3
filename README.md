# EasyPatchy3

## Project Overview

EasyPatchy3 is a web-based application for generating and managing binary patches between different versions of application builds. It provides an efficient way to create differential updates, reducing download sizes and enabling seamless version management.

## Core Features

### 1. Version Upload & Management
- Web form for selecting folders containing application builds
- Version naming and metadata tracking
- Storage of uploaded artifacts on local disk
- Historical version browsing and management

### 2. Patch Generation
- Automatic diff generation between versions using [HDiffPatch](https://github.com/sisong/HDiffPatch)
- Efficient binary patching algorithm for minimal patch sizes
- Support for both upgrade and downgrade patches
- Patch storage and organization on local disk

### 3. Patch Distribution
- Download functionality for stored artifacts
- Version selection interface for patch generation
- Direct patch download between any two versions
- Patch metadata and size information display

## Technology Stack

### Backend
- **Framework**: ASP.NET Core Blazor
- **Language**: C# (.NET)
- **Database**: PostgreSQL
- **Patching Library**: HDiffPatch (via P/Invoke or CLI wrapper)

### Frontend
- **UI Framework**: Blazor (Server-side or WebAssembly)
- **Component Library**: MudBlazor (Material Design components)
- **Styling**: MudBlazor theming system

### Infrastructure
- **Database**: PostgreSQL in Docker container
- **Application**: Blazor app with HDiffPatch bundled in Docker container
- **Storage**: Local file system for artifacts and patches (mounted as Docker volume)
- **Orchestration**: Docker Compose for multi-container management

## Project Structure

```
EasyPatchy3/
├── EasyPatchy3.App/          # Main Blazor application
│   ├── Data/                 # Entity Framework models and DbContext
│   ├── Services/             # Business logic services
│   ├── Pages/                # Blazor pages and components
│   ├── Shared/               # Shared UI components
│   ├── wwwroot/              # Static web assets
│   ├── Migrations/           # EF Core database migrations
│   ├── Program.cs            # Application entry point
│   ├── EasyPatchy3.csproj    # Project file
│   └── appsettings.json      # Configuration
├── EasyPatchy3.Tests/        # Unit and integration tests
│   ├── TestData/             # Test version samples
│   ├── VersionServiceTests.cs # Version management tests
│   ├── StorageServiceTests.cs # File storage tests
│   └── README.md             # Test documentation
├── Dockerfile                # Docker build configuration
├── docker-compose.yml        # Multi-container orchestration
├── EasyPatchy3.sln          # Visual Studio solution file
└── README.md                 # This file
```

## Architecture

### Data Model
- **Versions**: Store version metadata (name, timestamp, size, hash)
- **Artifacts**: Reference to uploaded build folders
- **Patches**: Store patch information (source version, target version, patch file location, size)
- **Audit Log**: Track upload, patch generation, and download activities

### Key Components

1. **Upload Service**
   - Handle folder selection and upload
   - Version validation and deduplication
   - Artifact storage management

2. **Patch Service**
   - Integration with HDiffPatch
   - Patch generation queue management
   - Bidirectional patch creation (upgrade/downgrade)

3. **Storage Service**
   - File system organization
   - Artifact and patch retrieval
   - Storage cleanup and maintenance

4. **Download Service**
   - Artifact serving
   - Patch serving
   - Download tracking

## User Workflow

1. **Upload New Version**
   - User selects folder containing application build
   - Provides version name and optional metadata
   - System stores artifact and registers version

2. **Generate Patches**
   - System automatically generates patches to/from existing versions
   - Or user manually requests specific version patches
   - Patches are stored for future use

3. **Download Artifacts/Patches**
   - Browse available versions
   - Select source and target versions for patching
   - Download complete artifacts or differential patches

## Development Setup

### Prerequisites
- Docker and Docker Compose
- .NET SDK (for local development only)

### Docker Architecture
The application uses a multi-container setup with Docker Compose:

1. **App Container** (easypatch3-app)
   - ASP.NET Core Blazor application
   - HDiffPatch binary included in container image
   - Exposed on port 8080 (configurable)
   - Volume mounts for artifact/patch storage

2. **Database Container** (easypatch3-db)
   - PostgreSQL latest version
   - Persistent volume for data
   - Internal network communication with app

3. **Docker Compose Configuration**
   - Service definitions for app and database
   - Network configuration for container communication
   - Volume definitions for persistent storage
   - Environment variable configuration

### Environment Configuration
- PostgreSQL connection string (configured via Docker Compose)
- Storage paths for artifacts and patches (Docker volumes)
- HDiffPatch binary location (bundled in container)
- Application ports and URLs (defined in docker-compose.yml)

### Database Schema
- Migrations managed via Entity Framework Core
- Automatic schema updates on application start
- Seed data for development environment

## Container Deployment

### Quick Start
```bash
# Start the entire application stack
docker-compose up -d

# View logs
docker-compose logs -f

# Stop the application
docker-compose down
```

### Container Configuration
- **Dockerfile** for EasyPatchy3 app:
  - Multi-stage build for optimized image size
  - .NET runtime base image
  - HDiffPatch binary installation
  - Application code and dependencies

- **docker-compose.yml** structure:
  - Network isolation between containers
  - Health checks for service availability
  - Restart policies for resilience
  - Environment variable injection

### Volume Management
- **Artifacts Volume**: Persistent storage for uploaded builds
- **Patches Volume**: Persistent storage for generated patches
- **PostgreSQL Data Volume**: Database persistence

## Deployment Considerations

### Storage Management
- Define retention policies for old versions
- Implement storage quota management
- Regular cleanup of orphaned patches

### Performance
- Async patch generation for large files
- Caching for frequently accessed patches
- CDN integration for distributed downloads

### Security
- File upload validation and scanning
- Access control for version management
- Secure storage of sensitive artifacts
- Rate limiting for downloads

## Future Enhancements

- Delta compression statistics and reporting
- Batch patch generation for multiple versions
- REST API for programmatic access
- Patch verification and integrity checking
- Version branching and tagging support
- Automated patch testing framework
- Cloud storage integration (S3, Azure Blob)
- Multi-tenant support with organization management