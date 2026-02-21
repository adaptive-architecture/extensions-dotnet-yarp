# AGENTS.md

This file provides technical guidance for AI coding agents working with the Extensions YARP repository. It complements README.md by containing detailed development practices and architectural constraints.

## Communication Guidelines

**IMPORTANT**: Provide succinct, minimal responses unless explicitly requested otherwise.

- Keep explanations brief and to the point
- Avoid verbose commentary or over-explaining
- Focus on actionable information only
- Provide details only when user specifically asks for them
- Let the code and test results speak for themselves

**Examples of preferred communication**:
- ✅ "Tests pass. Coverage maintained at 85%."
- ✅ "Added authentication middleware. See Auth/JwtMiddleware.cs:45"
- ❌ "I've successfully implemented the authentication middleware! This is a great addition that will help secure your application. The middleware validates JWT tokens and ensures that only authenticated users can access protected endpoints..."

## Quick Start

### Commands

**Build:**
```bash
dotnet build                    # Build all projects
dotnet build --no-incremental   # CI build without incremental
```

**Test:**
```bash
sh ./pipeline/unit-test.sh      # Preferred: unit tests with coverage
dotnet test                     # Run all tests
dotnet test --filter "FullyQualifiedName!~AdaptArch.Extensions.Yarp.Samples"  # Exclude samples
```

**Restore:**
```bash
dotnet restore                  # Restore NuGet packages
```

### Environment Variables for CI
- `TESTCONTAINERS_RYUK_DISABLED=true` - Required for integration tests in CI environments

## Project Architecture

### Structure
```
common-utilities/
├── src/              # Source projects (NuGet packages)
├── test/             # Unit and integration tests
├── samples/          # Usage demonstration projects
└── pipeline/         # Build and deployment scripts
```

### Technology Stack
- **Framework**: .NET 10.0 with latest language features
- **Solution Format**: Modern .slnx format
- **Build System**: MSBuild with shared `Directory.Build.props`
- **Packaging**: NuGet packages under `AdaptArch.Extensions.Yarp.*` namespace

### Utility Libraries

TODO: FIX THIS
**Specialized Modules**:
- `Auth`: Authentication and authorization extensions
- `OpenApi`: OpenApi extensions

## Project Constitution

The following constitutional principles govern all development in this repository. These are NON-NEGOTIABLE requirements that MUST be followed.

---

# Common Utilities Constitution

## Core Principles

### I. Library-First Design
Every feature MUST be implemented as a standalone library with clear boundaries. Libraries MUST be self-contained, independently testable, and have well-defined public APIs. Each library MUST solve a specific cross-cutting concern without organizational dependencies. Libraries MUST be packaged as NuGet packages following the `AdaptArch.Extensions.Yarp.*` namespace convention.

**Rationale**: Promotes reusability across projects, maintains clean architecture, and reduces coupling between different utility concerns.

### II. Test-Driven Development (NON-NEGOTIABLE)
Tests MUST be written before implementation. The Red-Green-Refactor cycle is strictly enforced: write failing tests, implement minimal code to pass, then refactor. Unit tests MUST achieve comprehensive coverage, and integration tests MUST use TestContainers for external dependencies (Redis, Postgres). Sample projects MUST demonstrate real-world usage but are excluded from test coverage requirements.

**Rationale**: Ensures reliability, maintainability, and serves as living documentation of expected behavior.

### III. Quality-First Standards
Code MUST pass all quality gates before merge. Warnings MUST be treated as errors. Roslynator analyzers MUST be enabled and passing. SonarCloud quality gates MUST pass. Code coverage MUST be maintained at acceptable levels. All projects MUST use consistent build configuration through `Directory.Build.props`.

**Rationale**: Maintains consistent, professional-grade code quality across all utility libraries.

### IV. Modular Specialization
Each library MUST focus on a single domain of utility functionality (encoding, extensions, Redis operations, etc.). Libraries MUST NOT depend on other utility libraries unless absolutely necessary. Cross-cutting concerns MUST be isolated into their own modules. Dependencies MUST be minimal and well-justified.

**Rationale**: Allows consumers to adopt only needed functionality without bloat, reduces dependency trees, and maintains clear separation of concerns.

### V. API Consistency
Public APIs MUST follow consistent patterns across all libraries. Extension methods MUST be preferred for enhancing existing types. Configuration MUST follow standard .NET patterns. Error handling MUST be consistent and predictable. Documentation MUST be comprehensive for public APIs.

**Rationale**: Provides predictable developer experience and reduces learning curve when adopting multiple utility libraries.

## Quality Standards

All code MUST meet the following non-negotiable quality standards:
- Target framework: .NET 10.0 with latest language features enabled
- Warnings treated as errors across all projects
- Roslynator analyzers enabled with strict rules
- SonarCloud quality gate compliance required
- Code coverage reporting via Coverlet in OpenCover and LCOV formats
- Integration tests using TestContainers where applicable
- `InternalsVisibleTo` attributes for test access to internal members

## Development Workflow

Development MUST follow this workflow:
1. **Design**: Create library with clear public API surface
2. **Test**: Write comprehensive unit tests covering all public functionality
3. **Implement**: Write minimal code to pass tests
4. **Integrate**: Add integration tests for external dependencies
5. **Document**: Create sample projects demonstrating usage
6. **Package**: Configure NuGet packaging with appropriate metadata
7. **Validate**: Ensure all quality gates pass before merge

Code reviews MUST verify compliance with all constitutional principles. Complex architectural decisions MUST be documented and justified.

## Governance

This constitution supersedes all other development practices and guidelines. All pull requests MUST be reviewed for constitutional compliance. Any exceptions MUST be explicitly documented and justified.

**Amendment Process**: Constitutional changes require documentation of rationale, impact analysis, and migration plan for affected code. Major principle changes require project-wide review and approval.

**Compliance Review**: Regular audits of libraries against constitutional principles. Non-compliant code MUST be refactored or documented as technical debt with remediation plan.

**Constitution Version**: 1.0.0 | **Ratified**: 2025-12-29 | **Last Amended**: 2025-12-29

---

## Code Style and Conventions

### Design Patterns
- Use `InternalsVisibleTo` for test project access
- Consistent naming: `AdaptArch.Extensions.Yarp.<Module>`
- Preview language features enabled (.NET 10)
- Roslynator analyzers enforce style

### Testing Patterns
- TestContainers for integration tests with external dependencies
- Coverage collected via Coverlet (OpenCover + LCOV formats)
- Integration test environment variables set in CI
- **NEVER suppress warnings unless explicitly told to by the user**
  - Fix the root cause instead of suppressing analyzer warnings
  - If analyzer rules are too strict, discuss with team before disabling

## Development Workflow

When implementing new features:

1. **Design**: Create library with clear public API surface
2. **Test**: Write comprehensive unit tests (TDD - tests first!)
3. **Implement**: Write minimal code to pass tests
4. **Integrate**: Add integration tests for external dependencies
5. **Document**: Create sample projects demonstrating usage
6. **Package**: Configure NuGet packaging with metadata
7. **Validate**: Ensure all quality gates pass

## Common Tasks

### Adding a New Utility Library

1. Create project structure:
   - `src/NewModule/`
   - `test/NewModule.Tests/`

2. Create samples in the `samples/Samples/NewModule/` path.

3. Add `InternalsVisibleTo` attribute in source project for test access

4. Follow TDD: write tests first, then implement

5. Ensure NuGet package configuration is complete

6. Add sample demonstrating real-world usage

### LLM.txt (`docfx/llm.txt`)

The `docfx/llm.txt` file follows the [llmstxt.org](https://llmstxt.org/) specification and is served at the root of the GitHub Pages site. It provides a machine-readable summary of the project documentation for AI agents.

- **Keep it in sync** with documentation changes: when docs pages are added, removed, or renamed, update `llm.txt` accordingly
- It is included in the site build via the `resource` section in `docfx/docfx.json`
- Links must point to the rendered HTML pages at `https://adaptive-architecture.github.io/extensions-dotnet-yarp/docs/<page>.html`
- Follow the spec structure: H1 (project name), blockquote (summary), body (packages), H2 sections (file lists with URLs)

### OpenAPI Spec Files (`*.openapi.json`)

**WARNING: NEVER edit `*.openapi.json` files manually.** They are auto-generated during `dotnet build` by `Microsoft.Extensions.ApiDescription.Server`. The source of truth is the C# controllers and their XML doc comments.

**How it works:**
- `.csproj` files set `<OpenApiGenerateDocuments>true</OpenApiGenerateDocuments>` and `<OpenApiDocumentsDirectory>$(MSBuildProjectDirectory)</OpenApiDocumentsDirectory>`
- `Microsoft.Extensions.ApiDescription.Server` MSBuild target runs during build, analyzes the compiled assembly + XML docs, and outputs `{ProjectName}.openapi.json` into the project directory
- To update these files, modify the controllers/models/XML comments and rebuild

### Running Integration Tests

Integration tests for Redis and Postgres use TestContainers:
```bash
# Local development (Ryuk enabled by default)
dotnet test

# CI environment
TESTCONTAINERS_RYUK_DISABLED=true dotnet test
```

### Code Coverage Collection

```bash
sh ./pipeline/unit-test.sh  # Generates coverage reports
```

Reports generated in:
- OpenCover format (for SonarCloud)
- LCOV format (for other tools)

## Quality and Security

### Static Analysis
- Roslynator analyzers enabled project-wide
- Warnings treated as errors
- SonarCloud integration for quality metrics

### Security Considerations
- No organizational dependencies in utility libraries
- Minimal external dependencies
- Each library independently auditable
- Follow standard .NET security practices

## Pull Request Guidelines

Before submitting PRs:
- All tests must pass
- Code coverage maintained or improved
- Quality gates pass (Roslynator, SonarCloud)
- Constitutional principles followed
- Sample project updated if API changed

## Monorepo Notes

This repository follows a modular monorepo structure:
- Each library is independently versioned via NuGet
- Shared build configuration in `Directory.Build.props`
- Tests organized by module
- Samples excluded from coverage requirements

## Troubleshooting

### Build Issues
- Run `dotnet clean` followed by `dotnet restore`
- Clear NuGet cache: `dotnet nuget locals all --clear`

### Test Container Issues
- Ensure Docker is running for integration tests
- Set `TESTCONTAINERS_RYUK_DISABLED=true` if Ryuk cleanup causes issues

### Quality Gate Failures
- Check Roslynator warnings: they are treated as errors
- Review SonarCloud dashboard for specific issues
- Ensure code coverage hasn't decreased

---

**Version**: 1.0.0
**Last Updated**: 2025-12-29
**Maintained By**: adaptive-architecture team
