# GitHub Actions Workflows

This directory contains the CI/CD workflows for the Fishing Regulations Blazor AI project.

## Workflows

### 1. CI Build (`ci.yml`)
- **Triggers**: Pull requests and pushes to main/master branches
- **Purpose**: Comprehensive build, test, and security scanning
- **Features**:
  - Builds the entire .NET 8 solution
  - Runs tests with result upload
  - Security vulnerability scanning
  - Artifact publishing for main branch
  - NuGet package caching for faster builds

### 2. Aspire Build & Test (`aspire-ci.yml`)
- **Triggers**: Pull requests and pushes to main/master branches
- **Purpose**: Specialized workflow for .NET Aspire applications
- **Features**:
  - Installs Aspire workload
  - Tests AppHost startup
  - Validates Aspire configuration
  - Checks for common Aspire issues

## Getting Started

1. **For Pull Requests**: Both workflows will run automatically when you create a PR
2. **For Merges**: Workflows run when PRs are merged to main/master
3. **Manual Runs**: Both workflows support manual triggering via the Actions tab

## Configuration

The workflows use these environment variables:
- `DOTNET_VERSION`: Set to '8.0.x' for .NET 8
- `ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS`: Allows anonymous access for CI
- `DOTNET_DASHBOARD_OTLP_ENDPOINT_URL`: Disables OTLP endpoint for CI

## Customization

To customize the workflows for your needs:

1. **Add Tests**: Uncomment the code coverage sections in `ci.yml`
2. **Different Branches**: Modify the `branches` arrays in the trigger sections
3. **Additional Projects**: Add more projects to the solution file
4. **Environment Variables**: Add project-specific environment variables

## Troubleshooting

- **Build Failures**: Check that all projects compile locally first
- **Test Failures**: Ensure tests pass locally before pushing
- **Aspire Issues**: Verify the AppHost can start locally without errors
- **Dependencies**: Make sure all NuGet packages are properly restored
