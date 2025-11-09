# RTUB Development Setup

## Configuration Files

### appsettings.Development.json (Local Development)

This file is **not tracked in Git** (see `.gitignore`) because it should contain your real credentials for local development.

**To set up:**
1. Copy `appsettings.Development.json.template` to `appsettings.Development.json`
2. Update the values with your actual credentials:
   - Email SMTP settings
   - IDrive S3 credentials
   - Any other environment-specific settings

```bash
cp src/RTUB.Web/appsettings.Development.json.template src/RTUB.Web/appsettings.Development.json
# Then edit appsettings.Development.json with your credentials
```

### Integration Tests Configuration

Integration tests use a custom `RTUBWebApplicationFactory` that provides test-specific configuration:
- In-memory database (no SQL Server required)
- Mock email settings (no actual emails sent)
- Mock S3/IDrive settings
- Reduced logging noise

You **don't need** `appsettings.Development.json` to run integration tests - they work out of the box with safe test values.

## Running Tests

```bash
# Run all integration tests
dotnet test tests/RTUB.Integration.Tests/RTUB.Integration.Tests.csproj

# Run specific test
dotnet test tests/RTUB.Integration.Tests/RTUB.Integration.Tests.csproj --filter "FullyQualifiedName~AuthenticationTests"
```

## Running the Application Locally

```bash
cd src/RTUB.Web
dotnet run
```

Make sure you have created your `appsettings.Development.json` file first (see above).
