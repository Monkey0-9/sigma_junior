# HFT Platform Operations Guide

## Build & Runtime Requirements

### Prerequisites
- **.NET SDK**: 8.0.x or later
- **Runtime**: .NET 8.0
- **Platform**: Windows 10/11 or Linux (tested on Ubuntu 22.04+)

### Required Tools
```powershell
# Windows - Reserve metrics URL (run once as admin)
netsh http add urlacl url=http://127.0.0.1:9180/metrics/ user=%USERDOMAIN%\%USERNAME%
```

## Build Commands

```bash
# Clean previous build artifacts
dotnet clean

# Build Release configuration (institutional standard)
dotnet build -c Release

# Build Debug configuration (for development)
dotnet build -c Debug

# Build specific project
dotnet build -c Release path/to/project.csproj
```

## Test Commands

```bash
# Run all tests
dotnet test -c Release

# Run specific test class
dotnet test -c Release --filter "FullyQualifiedName~DeterministicReplayTests"

# Run with verbose output
dotnet test -c Release --verbosity detailed

# Run single test
dotnet test -c Release --filter "FullyQualifiedName~DeterministicRandomProvider_SameSeed"
```

## Run Commands

```bash
# Run Hft.Runner (Release)
dotnet run -c Release --project Hft.Runner/Hft.Runner.csproj

# Run with custom ports
dotnet run -c Release --project Hft.Runner/Hft.Runner.csproj -- --udp-port 5005 --metrics-port 9180

# Run in shadow-copy mode
dotnet run -c Release --project Hft.Runner/Hft.Runner.csproj -- --shadow-copy-output ./run/timestamp
```

## CI Pipeline

The GitHub Actions workflow (`.github/workflows/build-and-test.yml`) performs:
1. `dotnet restore` - Restore dependencies
2. `dotnet build -c Release --no-restore` - Build with warnings-as-errors
3. `dotnet test -c Release --no-build` - Run all tests
4. `dotnet format --verify-no-changes` - Verify code formatting

## Directory Structure

```
c:/hft_platform/
├── Hft.Runner/              # Main entry point
│   ├── bin/Release/         # Release binaries
│   ├── data/
│   │   ├── audit/           # Audit logs
│   │   ├── metrics/         # Metrics data
│   │   └── replay/          # Replay files
│   └── logs/                # Application logs
├── core/hft.core/           # Core primitives and interfaces
├── infra/hft.infra/         # Infrastructure services
├── feeds/hft.feeds/         # Market data feeds
├── execution/hft.execution/ # Execution engines
├── risk/hft.risk/           # Risk management
├── strategies/hft.strategies/ # Strategy implementations
├── tests/                   # Unit and integration tests
└── docs/                    # Architecture documentation
```

## Troubleshooting

### Build Failures
- Ensure .NET 8.0 SDK is installed: `dotnet --version`
- Run `dotnet clean` before rebuilding
- Check for CAXXXX code analysis warnings

### Metrics Server Fails to Start
- Check if port 9180 is in use: `netstat -ano | findstr :9180`
- Verify HTTP URL reservation (see Prerequisites above)
- Check logs in `logs/` directory

### High Latency
- Monitor with: `dotnet run --project Hft.Runner` (shows real-time stats)
- Check for GC pauses: Enable GC diagnostics
- Consider process affinity: `start /affinity <mask> dotnet run...`

### File Lock Errors
- Ensure no running instances: Check Task Manager for Hft.Runner
- Use shadow-copy mode for isolated execution
- Run `scripts/kill-runner.ps1` to force cleanup

