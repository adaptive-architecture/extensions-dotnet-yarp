# Specifications (specs) Directory

## Purpose

The `specs/` directory contains comprehensive specifications for features, architectural decisions, implementation details, and known issues for the Extensions YARP repository. This directory serves as the **design documentation hub** where features are planned, decisions are documented, and implementation details are preserved for future reference.

Unlike code comments or API documentation, specs provide:
- **Design rationale**: Why features exist and what problems they solve
- **Architectural decisions**: Key choices made during development with trade-offs
- **Implementation details**: Deep technical explanations beyond what code comments provide
- **Known limitations**: Honest assessment of current constraints and future improvements
- **Living documentation**: Updated as features evolve

## Relationship to Constitutional Principles

This specs directory directly supports the constitutional principles defined in `AGENTS.md`:

### Design-First Development (Constitutional Principle)

From `AGENTS.md` Development Workflow:
> 1. **Design**: Create library with clear public API surface

Specs are created **before implementation** as part of the design phase. This ensures:
- Clear goals and non-goals are established upfront
- Architectural decisions are made consciously, not accidentally
- Trade-offs are documented for future maintainers
- Testing strategy is planned alongside implementation

### Modular Specialization (Section IV)

> Each library MUST focus on a single domain of utility functionality

Each library (`OpenApi`, `Auth`, etc.) has its own specs directory mirroring the `src/` structure. This keeps specifications modular and colocated with their respective domains.

### Quality-First Standards (Section III)

> Documentation MUST be comprehensive for public APIs.

Specs complement API documentation by providing:
- **API docs** (docfx): What the API does and how to use it
- **Specs**: Why the API exists, how it works internally, and what decisions shaped it

### Test-Driven Development (Section II)

Specs define:
- Testing strategy for each feature
- Success criteria (functional, performance, quality)
- Key test scenarios to validate

This aligns with the TDD constitutional requirement by planning tests during the design phase.

## Directory Structure

The specs directory mirrors the `src/` project structure:

```
specs/
├── README.md                    # This file - guide to specs system
├── _templates/                  # Reusable templates for consistency
│   ├── spec.md                 # Main feature specification template
│   ├── extra_details.md        # Technical details template
│   └── known_issues.md         # Issue tracking template
├── OpenApi/                    # Specs for src/OpenApi project
│   └── aggregation/            # OpenAPI aggregation feature
│       ├── spec.md             # Main specification
│       ├── extra_details.md    # Implementation details
│       └── known_issues.md     # Known limitations
└── Auth/                       # Specs for src/Auth project
    └── [future-feature]/       # Future features added here
```

### Organizational Principles

- **Project-level folders** (e.g., `OpenApi/`, `Auth/`): Match `src/` project names exactly (PascalCase)
- **Feature-level folders** (e.g., `aggregation/`): Use kebab-case descriptive names
- **Standard files**: Each feature has three files: `spec.md`, `extra_details.md`, `known_issues.md`

## When to Create Specifications

Create new specifications in these scenarios:

### 1. Before Implementing New Features (Design Phase)

Per the constitutional development workflow, design comes first:
```
Design → Test → Implement → Integrate → Document → Package → Validate
```

**Before writing code**, create a spec covering:
- Feature overview and motivation
- Goals and non-goals
- Technical approach and key components
- API design (if adding public APIs)
- Testing strategy
- Architectural decisions

This ensures thoughtful design and prevents "implementation-first" development.

### 2. When Making Architectural Decisions

Significant technical decisions should be documented as **Architectural Decision Records (ADRs)** within the relevant feature spec's `spec.md` file.

Examples of architectural decisions:
- Choosing between alternative approaches (e.g., metadata format, caching strategy)
- Technology or library selection
- Performance vs. simplicity trade-offs
- Extensibility vs. complexity trade-offs

### 3. When Adding New Public APIs

New public APIs require comprehensive specs covering:
- API design and surface area
- Configuration options
- Extension points
- Integration patterns
- Breaking change considerations

### 4. When Changing Existing Features Significantly

For major refactoring or enhancements:
- Update existing specs to reflect changes
- Document new decisions made during evolution
- Update known issues and limitations
- Revise testing strategy if needed

### 5. When Addressing Complex Technical Challenges

For non-trivial algorithms or complex scenarios:
- Create detailed explanations in `extra_details.md`
- Document edge cases and corner cases
- Explain performance characteristics
- Provide troubleshooting guidance

## File Purposes and Guidelines

Each feature specification consists of three files:

### 1. `spec.md` - Main Feature Specification

**Purpose**: High-level overview, goals, approach, and decisions.

**Key Sections**:
- **Overview**: What the feature is (2-3 sentences)
- **Motivation**: Why it exists, what problem it solves
- **Goals / Non-Goals**: Clear scope boundaries
- **Technical Approach**: High-level architecture and strategy
- **Key Components**: Main classes, interfaces, modules
- **API Design**: Public APIs, configuration, extensibility
- **Testing Strategy**: How the feature is tested
- **Success Criteria**: How to measure success
- **Architectural Decisions**: ADRs for significant choices
- **References**: Links to related docs, code, issues

**Target Length**: 1,500-3,000 words (comprehensive but focused)

### 2. `extra_details.md` - Implementation Details

**Purpose**: Deep technical details for maintainers and contributors.

**Key Sections**:
- **Implementation Details**: Component-by-component deep dive
- **Algorithms**: Step-by-step explanations of complex logic
- **Complex Scenarios**: Edge cases, multi-stage processing
- **Integration Points**: How feature integrates with YARP, ASP.NET Core, etc.
- **Performance Considerations**: Caching, optimization, benchmarks
- **Extensibility Points**: How to customize and extend
- **Code Examples**: Usage patterns and advanced scenarios
- **Troubleshooting**: Common issues and solutions

**Target Length**: 1,000-2,500 words (as detailed as needed)

### 3. `known_issues.md` - Known Issues and Future Work

**Purpose**: Honest assessment of limitations and roadmap.

**Key Sections**:
- **Current Limitations**: Known constraints with workarounds
- **Known Bugs**: Open issues (if any)
- **Future Improvements**: Prioritized enhancement backlog
- **Technical Debt**: Items to address eventually
- **Breaking Changes (Planned)**: Future incompatibilities
- **Related Issues**: Links to GitHub issues

**Target Length**: 500-1,500 words (concise, actionable)

## Using Templates

Templates are provided in `specs/_templates/` to ensure consistency across all specifications.

### Creating a New Specification

1. **Create feature directory**:
   ```bash
   mkdir -p specs/{Project}/{feature-name}
   ```

2. **Copy templates**:
   ```bash
   cp specs/_templates/spec.md specs/{Project}/{feature-name}/
   cp specs/_templates/extra_details.md specs/{Project}/{feature-name}/
   cp specs/_templates/known_issues.md specs/{Project}/{feature-name}/
   ```

3. **Fill out templates**:
   - Replace placeholders (e.g., `[Feature Name]`) with actual content
   - Update dates
   - Follow the structure provided in templates
   - Remove sections that don't apply (with justification in spec)

4. **Review against constitutional principles**:
   - Does the design align with modular specialization?
   - Is the testing strategy comprehensive?
   - Are architectural decisions clearly documented?
   - Does the API follow consistency patterns?

### Template Flexibility

While templates provide structure, they are **guidelines, not rigid rules**:
- Add sections if needed for your specific feature
- Combine or split sections if it improves clarity
- Adapt to feature complexity (simple features need less detail)
- Maintain consistency in tone and formatting

## Naming Conventions

Consistent naming ensures discoverability and maintains clean organization.

### Project-Level Folders (PascalCase)

Match `src/` project names exactly:
- ✅ `OpenApi/` (matches `src/OpenApi/`)
- ✅ `Auth/` (matches `src/Auth/`)
- ❌ `open-api/` (lowercase - incorrect)
- ❌ `OpenAPI/` (capitalization mismatch - incorrect)

### Feature-Level Folders (kebab-case)

Use descriptive, lowercase names with hyphens:
- ✅ `aggregation/`
- ✅ `jwt-validation/`
- ✅ `rate-limiting/`
- ❌ `AggregationFeature/` (PascalCase - incorrect)
- ❌ `jwt_validation/` (underscores - incorrect)

### Files (lowercase with hyphens)

Standard file names across all features:
- ✅ `spec.md`
- ✅ `extra_details.md`
- ✅ `known_issues.md`
- ❌ `Spec.md` (capitalized - incorrect)
- ❌ `extra-details.md` (use underscore - incorrect)

## Example: OpenApi Aggregation

The `specs/OpenApi/aggregation/` directory provides a **complete reference example** of a well-documented feature:

- **`spec.md`**: Comprehensive specification covering motivation, approach, decisions, and testing
- **`extra_details.md`**: Deep technical details on algorithms, integration, and extensibility
- **`known_issues.md`**: Honest assessment of current limitations and future work

When creating new specs, refer to this example for:
- Appropriate level of detail
- Tone and style
- How to document architectural decisions
- How to structure complex technical explanations

## Maintenance and Updates

Specifications are **living documents** that evolve with the codebase.

### When to Update Specs

- **Feature changes**: Update specs when implementation changes significantly
- **New decisions**: Add ADRs when making new architectural choices
- **Bugs discovered**: Update `known_issues.md` when limitations are found
- **Improvements completed**: Move items from "Future Improvements" to implementation
- **Breaking changes**: Document in `known_issues.md` under "Breaking Changes (Planned)"

### Update Frequency

- **Regularly**: Update specs as part of feature development (not as an afterthought)
- **PR reviews**: Verify specs are updated in pull requests that change features
- **Version milestones**: Review all specs for accuracy before major releases

### Avoiding Drift

To keep specs in sync with code:
- Include spec updates in feature PR checklists
- Reference spec files in PR descriptions
- Mark outdated specs with warnings at the top of the file
- Periodically audit specs for accuracy (quarterly or per release)

## Specs vs. Other Documentation

Understanding how specs fit into the documentation ecosystem:

| Document Type | Purpose | Audience | Location |
|---------------|---------|----------|----------|
| **Specs** (this dir) | Design, decisions, implementation details | Developers, maintainers, architects | `specs/` |
| **API Docs** (docfx) | Public API reference | Library consumers | `docfx/` (auto-generated) |
| **README** (repo root) | Quick start, overview | First-time users | `README.md` |
| **AGENTS.md** | Constitutional principles, dev guidelines | AI agents, contributors | `AGENTS.md` |
| **Sample READMEs** | Usage examples, how-to guides | Developers learning by example | `samples/{Sample}/README.md` |
| **Code Comments** | Line-by-line explanations | Code readers | Source files |

**Specs complement, not replace, other documentation.** Use specs for *why* and *how design*, use other docs for *what* and *how to use*.

## Integration with Development Workflow

Specs integrate into the constitutional development workflow:

```
1. Design (specs created) → 2. Test → 3. Implement → 4. Integrate → 5. Document → 6. Package → 7. Validate
```

### Phase 1: Design (Create Specs)

- Create `spec.md` outlining feature design
- Document architectural decisions with ADRs
- Define testing strategy and success criteria
- Review spec with team/maintainers before proceeding

### Phase 2-4: Test, Implement, Integrate

- Refer to spec during implementation
- Update spec if design changes during development
- Ensure implementation aligns with documented decisions

### Phase 5: Document

- Create `extra_details.md` with implementation insights
- Update API documentation (docfx)
- Create or update sample READMEs

### Phase 6-7: Package, Validate

- Create `known_issues.md` documenting any limitations
- Verify all quality gates pass
- Ensure specs accurately reflect final implementation

## Contributing

When contributing features to this repository:

1. **Read constitutional principles**: Review `AGENTS.md` for non-negotiable requirements
2. **Design first**: Create specs before writing code
3. **Use templates**: Copy from `specs/_templates/` for consistency
4. **Document decisions**: Include ADRs for significant choices
5. **Update existing specs**: If changing features, update their specs
6. **Request spec review**: Ask maintainers to review specs alongside code

## Questions and Feedback

If you have questions about specs or suggestions for improvement:
- Open a GitHub issue tagged with `documentation`
- Reference this README and the specific spec in question
- Suggest improvements to templates or structure

## Version History

- **1.0.0** (2026-01-10): Initial specs directory structure and templates created
  - Added OpenApi aggregation as reference example
  - Created comprehensive templates
  - Established naming conventions and guidelines
