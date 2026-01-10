# [Feature Name]

> **Status**: [Planning | In Progress | Implemented | Deprecated]  
> **Project**: [Auth | OpenApi | etc.]  
> **Created**: YYYY-MM-DD  
> **Last Updated**: YYYY-MM-DD

## Overview

Brief description of the feature and its purpose (2-3 sentences). This should be a clear, concise statement of what this feature is and what it does.

## Motivation

Why does this feature exist? What problem does it solve? What pain points or use cases drove its creation?

Describe:
- The context and background
- User needs or challenges
- Why existing solutions are insufficient
- How this feature addresses the problem

## Goals

What this feature aims to achieve:
- Goal 1: Clear, measurable objective
- Goal 2: Another specific goal
- Goal 3: etc.

## Non-Goals

What this feature explicitly does NOT aim to achieve (scope boundaries):
- Non-goal 1: What is intentionally excluded
- Non-goal 2: What is out of scope
- Non-goal 3: etc.

Clear non-goals prevent scope creep and set appropriate expectations.

## Technical Approach

High-level technical strategy and architecture. Describe the overall approach without diving into implementation details (save those for `extra_details.md`).

### Key Components

List main classes, interfaces, or modules:

| Component | Purpose |
|-----------|---------|
| `ComponentName` | Brief description of what it does |
| `AnotherComponent` | Another component's purpose |

### Dependencies

What this feature depends on:
- **YARP**: Specific YARP features used (if applicable)
- **Libraries**: External NuGet packages required
- **Other features**: Dependencies on other modules in this repo
- **External services**: Any external system integrations

## API Design

Public API surface (if applicable):

### Configuration Options

```csharp
services.AddFeatureName(options =>
{
    options.SomeSetting = value;
    options.AnotherSetting = value;
});
```

### Extension Methods

```csharp
app.UseFeatureName();
```

### Interfaces for Extensibility

- `IInterfaceName`: Purpose and when to implement
- `IAnotherInterface`: Another extensibility point

### Metadata Configuration

If using YARP metadata, show the structure:
```json
{
  "Metadata": {
    "Ada.FeatureName": "{\"key\":\"value\"}"
  }
}
```

## Configuration

How users configure this feature.

### Global Options

Describe each configuration option:
- **`OptionName`**: What it does, default value, valid range
- **`AnotherOption`**: Description and defaults

### Per-Route/Cluster Configuration

If applicable, describe route or cluster-level configuration.

### Configuration Examples

```csharp
// Basic configuration
services.AddFeatureName();

// Advanced configuration
services.AddFeatureName(options =>
{
    options.DetailedSetting = value;
    options.ComplexOption = new ComplexValue();
});
```

## Testing Strategy

How this feature is tested to ensure quality and reliability.

### Unit Tests

- **Coverage target**: e.g., "All public methods and core logic"
- **Test categories**: What types of tests exist
  - Component tests for individual classes
  - Configuration parsing tests
  - Logic validation tests
- **Key test scenarios**: Critical paths that must be validated

### Integration Tests

If applicable:
- **Test environment**: TestContainers, in-memory services, etc.
- **End-to-end scenarios**: Complete workflows tested
- **Dependencies**: How external dependencies are handled

### Sample Application

If a sample exists:
- **Location**: `samples/{SampleName}/`
- **Purpose**: What the sample demonstrates
- **Coverage**: What scenarios are included

## Success Criteria

How do we know this feature is successful?

### Functional Criteria
- ✅ Criterion 1: Specific functional requirement met
- ✅ Criterion 2: Another functional requirement
- ✅ Criterion 3: etc.

### Performance Criteria
- ✅ Target latency: e.g., "<50ms for cached operations"
- ✅ Target throughput: e.g., "1000 requests/second"
- ✅ Memory usage: e.g., "<100MB for typical usage"

### Quality Criteria
- ✅ Test coverage: e.g., ">90% line coverage"
- ✅ No compiler warnings
- ✅ SonarCloud quality gate passing
- ✅ Roslynator rules passing

## Architectural Decisions

Document significant technical decisions made during design/implementation.

### Decision 1: [Title of Decision]

**Context**: Why this decision was needed. What problem or question required a choice?

**Options Considered**:
1. **Option A**: Description and pros/cons
2. **Option B**: Description and pros/cons
3. **Option C**: Description and pros/cons

**Decision**: What was chosen (e.g., "Option B")

**Rationale**: Why this option was selected over alternatives. What factors were most important?

**Consequences**:
- ✅ Positive outcome 1
- ✅ Positive outcome 2
- ❌ Trade-off or limitation 1
- ❌ Trade-off or limitation 2

### Decision 2: [Title of Another Decision]

Repeat the same structure for each significant decision.

### Decision 3: [Title of Yet Another Decision]

Continue documenting all important architectural choices.

## References

- **Related specs**: Links to other specs in this repository
- **External documentation**: Links to YARP docs, OpenAPI spec, etc.
- **GitHub issues**: Related issues or discussions
- **Pull requests**: PRs that implemented this feature
- **Source code**: `src/{Project}/` location
- **Tests**: `test/{Project}.Tests/` location
- **Samples**: `samples/{Sample}/` location (if applicable)

---

**Template Version**: 1.0.0  
**Last Updated**: 2026-01-10

## Notes for Template Users

- Replace all placeholders in `[brackets]` with actual content
- Remove this "Notes for Template Users" section when creating real specs
- Adjust sections as needed for your specific feature
- Aim for 1,500-3,000 words for a comprehensive spec
- Use `extra_details.md` for deep technical details
- Use `known_issues.md` for limitations and future work
