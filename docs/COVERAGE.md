# Code Coverage Guide

This project includes comprehensive code coverage reporting to ensure quality and maintainability.

## Visual Studio 2026 Professional

### Viewing Coverage in Visual Studio

Visual Studio 2026 Professional has built-in code coverage support. Here's how to use it:

#### Method 1: Test Explorer
1. Open **Test Explorer** (Test → Test Explorer or `Ctrl+E, T`)
2. Right-click on the solution or any test project
3. Select **"Analyze Code Coverage for All Tests"** or **"Analyze Code Coverage for Selected Tests"**
4. Coverage results will appear in the **Code Coverage Results** window

#### Method 2: Menu Bar
1. Go to **Test** → **Analyze Code Coverage** → **All Tests**
2. Or use **Test** → **Analyze Code Coverage** → **Selected Tests** (if you have tests selected)

#### Method 3: Solution Explorer
1. Right-click on the solution in Solution Explorer
2. Select **"Analyze Code Coverage for All Tests"**

### Understanding Coverage Results

The **Code Coverage Results** window shows:
- **Coverage Percentage** - Overall coverage for each project
- **Line Coverage** - Percentage of lines covered
- **Branch Coverage** - Percentage of branches covered
- **Uncovered Lines** - Specific lines not covered by tests

Click on any project to drill down into:
- Individual files and their coverage
- Specific methods and classes
- Uncovered code highlighted in red

### Coverage Settings

The `.runsettings` file is configured for optimal coverage:
- Excludes test projects from coverage calculations
- Includes source link for better debugging
- Configures multiple output formats (JSON, Cobertura, OpenCover)

## Command Line Coverage

### Quick Coverage Check

Run tests with coverage:
```powershell
dotnet test Prepared.slnx --collect:"XPlat Code Coverage" --settings:.runsettings
```

### Generate HTML Report

Run the coverage script from the solution root:
```powershell
.\scripts\test-coverage.ps1
```

This will:
1. Run all tests with coverage collection
2. Generate an HTML report in the `coverage` folder
3. Display a summary in the console
4. Open `coverage/index.html` in your browser for detailed analysis

### HTML Report Features

The generated HTML report includes:
- **Summary Dashboard** - Overall coverage percentages
- **File-by-File Breakdown** - Detailed coverage per file
- **Line-by-Line Coverage** - See exactly which lines are covered
- **Branch Coverage** - Conditional statement coverage
- **Coverage Badges** - Visual indicators of coverage levels

## Coverage Goals

This project maintains high coverage standards:
- **Target**: 80%+ overall coverage
- **Critical Paths**: 95%+ coverage (services, controllers, middleware)
- **New Code**: 100% coverage requirement before merge

## Current Coverage

Run the coverage script to see current percentages:
```powershell
.\test-coverage.ps1
```

The summary will show coverage for:
- Prepared.Client
- Prepared.Business
- Prepared.Data
- Prepared.Common

## Troubleshooting

### Coverage Not Showing in Visual Studio

1. Ensure `.runsettings` file is in the solution root
2. Check that `coverlet.collector` package is referenced in test projects
3. Try rebuilding the solution
4. Check Visual Studio Test settings: **Test** → **Test Settings** → **Select Test Settings File** → Choose `.runsettings`

### Coverage File Not Generated

1. Verify tests are passing
2. Check that `coverlet.collector` is installed in test projects
3. Ensure `--collect:"XPlat Code Coverage"` parameter is used
4. Check `TestResults` folder for generated files

### ReportGenerator Not Found

Install the tool globally:
```powershell
dotnet tool install -g dotnet-reportgenerator-globaltool
```

## Integration with CI/CD

Coverage can be integrated into CI/CD pipelines:

```yaml
# Example GitHub Actions
- name: Run tests with coverage
  run: dotnet test --collect:"XPlat Code Coverage" --settings:.runsettings

- name: Generate coverage report
  run: |
    dotnet tool install -g dotnet-reportgenerator-globaltool
    reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coverage" -reporttypes:"Html"
```

## Best Practices

1. **Run coverage regularly** - Before committing code
2. **Aim for high coverage** - But focus on meaningful tests
3. **Review uncovered code** - Identify gaps in testing
4. **Use coverage to guide testing** - Find untested edge cases
5. **Don't sacrifice quality for coverage** - 100% coverage doesn't mean perfect tests

