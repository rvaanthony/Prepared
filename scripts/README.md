# Scripts

This directory contains utility scripts for building, testing, and maintaining the project.

## Available Scripts

### `test-coverage.ps1` / `test-coverage.sh`
Runs all tests with code coverage collection and generates an HTML coverage report.

**Usage:**
```powershell
# Windows
.\scripts\test-coverage.ps1

# Linux/Mac
./scripts/test-coverage.sh
```

**What it does:**
- Runs all tests in the solution
- Collects code coverage data
- Generates an HTML report in the `coverage` folder
- Displays a summary in the console

**Requirements:**
- ReportGenerator tool: `dotnet tool install -g dotnet-reportgenerator-globaltool`

## Adding New Scripts

When adding new scripts:
- Use descriptive names (e.g., `build.ps1`, `deploy.sh`)
- Include usage comments at the top
- Ensure scripts work from the solution root directory
- Update this README with script descriptions

