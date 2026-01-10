# [Feature Name] - Extra Details

> This document contains deep technical details for [Feature Name].  
> **Last Updated**: YYYY-MM-DD

## Implementation Details

Deep dive into how the feature is implemented. This section complements the high-level "Technical Approach" in `spec.md` with detailed explanations.

### [Component/Class Name]

**Purpose**: What this component does and why it exists.

**Responsibilities**:
- Responsibility 1
- Responsibility 2
- Responsibility 3

#### Algorithm

Step-by-step explanation of the core algorithm or logic:

```
Given input X and parameters Y:
1. Step 1: Initialize state
2. Step 2: Process input
3. Step 3: Apply transformations
4. Step 4: Return result

Pseudocode or detailed explanation
```

#### Data Structures

Key data structures used:
- **StructureName**: Purpose and characteristics
- **AnotherStructure**: What it stores and why

#### Performance Characteristics

- **Time complexity**: O(n), O(log n), etc.
- **Space complexity**: Memory usage patterns
- **Optimization notes**: How performance is maintained

### [Another Component/Class Name]

Repeat the same structure for each significant component.

## Complex Scenarios

### Scenario 1: [Scenario Name]

**Description**: What makes this scenario complex or interesting.

**Approach**: How the feature handles this scenario.

**Edge Cases**: Special conditions to watch for.

**Example**:
```
Input: ...
Processing: ...
Output: ...
```

### Scenario 2: [Another Complex Scenario]

Continue documenting complex scenarios that required special handling.

## Integration Points

How this feature integrates with other systems and libraries.

### Integration with YARP

If applicable:
- What YARP APIs are used
- How the feature hooks into YARP's pipeline
- Any YARP-specific considerations
- Transform handling, route matching, etc.

### Integration with ASP.NET Core

- Middleware registration patterns
- Dependency injection integration
- Configuration system usage
- Logging patterns

### Integration with Other Modules

If this feature depends on or integrates with other modules in this repo:
- How they communicate
- Shared interfaces or abstractions
- Dependency relationships

### Integration with External Systems

If applicable:
- How external services are called
- Authentication/authorization handling
- Error handling for external failures
- Retry and resilience patterns

## Edge Cases and Corner Cases

Unusual situations and how they're handled.

### Edge Case 1: [Description]

**Scenario**: What makes this an edge case.

**Handling**: How the implementation deals with it.

**Testing**: How this is validated.

### Edge Case 2: [Another Edge Case]

Continue documenting important edge cases.

### Corner Case 1: [Rare Combination]

Corner cases involve multiple conditions occurring simultaneously.

## Performance Considerations

### Caching Strategies

If caching is used:
- **Cache keys**: How cache keys are generated
- **Expiration policy**: When cached data expires
- **Invalidation**: When cache is cleared
- **Memory management**: How memory usage is controlled

### Optimization Techniques

- **Technique 1**: What was optimized and how
- **Technique 2**: Another optimization
- **Technique 3**: etc.

### Benchmarking Notes

If performance has been measured:
- **Baseline metrics**: Performance before optimization
- **Current metrics**: Current performance characteristics
- **Test methodology**: How performance was measured
- **Bottlenecks**: Known performance limitations

### Scalability

- **Horizontal scalability**: How it behaves with multiple instances
- **Vertical scalability**: Resource usage as load increases
- **Limits**: Known scalability boundaries

## Extensibility Points

How developers can extend this feature.

### Custom Interfaces to Implement

#### `IInterfaceName`

**Purpose**: What this interface allows you to customize.

**When to Implement**: Use cases for custom implementations.

**Example**:
```csharp
public class CustomImplementation : IInterfaceName
{
    public ReturnType Method(Parameters params)
    {
        // Custom logic
    }
}

// Register:
services.AddSingleton<IInterfaceName, CustomImplementation>();
```

### Extension Points

List places where the feature can be extended:
- **Extension point 1**: What can be customized
- **Extension point 2**: Another extensibility mechanism

### Customization Examples

Provide examples of common customization scenarios.

## Migration Guide

For existing users upgrading to this feature or migrating from an old approach.

### Migrating from [Old Approach]

**Breaking changes**:
- Change 1: What changed and why
- Change 2: Another breaking change

**Migration steps**:
1. Step 1: First action to take
2. Step 2: Next action
3. Step 3: etc.

**Code examples**:

**Before**:
```csharp
// Old code
```

**After**:
```csharp
// New code
```

## Code Examples

### Basic Usage

```csharp
// Basic example demonstrating typical usage
var builder = WebApplication.CreateBuilder(args);

// Register feature
builder.Services.AddFeatureName();

var app = builder.Build();

// Use feature
app.UseFeatureName();

app.Run();
```

### Advanced Usage

```csharp
// Advanced example with custom configuration
builder.Services.AddFeatureName(options =>
{
    options.AdvancedSetting = value;
    options.CustomBehavior = CustomBehavior.Enabled;
});

// Register custom implementation
builder.Services.AddSingleton<ICustomInterface, CustomImplementation>();
```

### Custom Extension Example

```csharp
// Example of extending the feature
public class MyCustomExtension : IExtensionInterface
{
    public void CustomMethod()
    {
        // Custom implementation
    }
}
```

## Troubleshooting

Common issues and solutions.

### Issue: [Common Problem]

**Symptoms**: What the user experiences.

**Possible Causes**:
1. Cause 1: Most likely reason
2. Cause 2: Another possibility
3. Cause 3: Less common cause

**Solutions**:
1. Solution 1: Try this first
2. Solution 2: If that doesn't work, try this
3. Solution 3: Last resort

**Diagnostic commands**:
```bash
# Commands to diagnose the issue
```

### Issue: [Another Common Problem]

Repeat the same structure for each common issue.

## Debugging Tips

### Enabling Debug Logging

```json
{
  "Logging": {
    "LogLevel": {
      "AdaptArch.Extensions.Yarp.FeatureName": "Debug"
    }
  }
}
```

### What to Look For in Logs

- Log message 1: What it means
- Log message 2: When it appears and why
- Warning signs: Indicators of problems

### Common Mistakes

- **Mistake 1**: What developers often do wrong
- **Mistake 2**: Another common error
- **Mistake 3**: etc.

## Internal Implementation Notes

Notes for maintainers and contributors.

### Design Patterns Used

- **Pattern 1**: Why this pattern was chosen
- **Pattern 2**: Another pattern
- **Pattern 3**: etc.

### Code Organization

How the code is structured:
- **Folder 1**: What it contains
- **Folder 2**: Purpose of this folder
- **File naming**: Naming conventions used

### Testing Approach

Internal testing strategies:
- Unit test organization
- Mock/stub patterns
- Test data generation

### Future Refactoring Opportunities

Ideas for improving the implementation:
- Opportunity 1: Potential improvement
- Opportunity 2: Another idea
- Opportunity 3: etc.

---

**Template Version**: 1.0.0  
**Last Updated**: 2026-01-10

## Notes for Template Users

- Replace all placeholders in `[brackets]` with actual content
- Remove this "Notes for Template Users" section when creating real specs
- Adjust sections as needed - not all features need all sections
- Aim for 1,000-2,500 words depending on complexity
- Focus on details that aren't obvious from the code
- Include diagrams if they help clarify complex flows
