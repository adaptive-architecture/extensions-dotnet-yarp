# [Feature Name] - Known Issues

> **Last Updated**: YYYY-MM-DD

## Current Limitations

| Limitation | Severity | Impact | Workaround |
|------------|----------|--------|------------|
| Brief description of limitation | High/Medium/Low | Who it affects and how | How to work around it, or "None" |
| Another limitation | High/Medium/Low | Impact description | Workaround description |
| Third limitation | High/Medium/Low | Impact description | Workaround description |

### Severity Levels

- **High**: Blocks major use cases or causes significant problems
- **Medium**: Affects some users or scenarios, but workarounds exist
- **Low**: Minor inconvenience or affects rare scenarios

## Known Bugs

| Issue | Status | Severity | Notes |
|-------|--------|----------|-------|
| Brief bug description | Open/In Progress/Fixed | High/Medium/Low | Additional context, related GitHub issue |
| Another bug | Open/In Progress/Fixed | High/Medium/Low | Context and links |

**Note**: If no bugs are currently known, state: "No known bugs at this time."

## Future Improvements

Prioritized backlog of enhancements and features to add in future versions.

### High Priority

Items that should be addressed in the near term:

- [ ] **Improvement 1**: Description of what should be improved
  - **Rationale**: Why this is important
  - **Effort**: Estimated complexity (Small/Medium/Large)
  
- [ ] **Improvement 2**: Another high-priority item
  - **Rationale**: Why this matters
  - **Effort**: Estimated complexity

- [ ] **Improvement 3**: Third high-priority improvement
  - **Rationale**: Justification
  - **Effort**: Estimated complexity

### Medium Priority

Items to address when time permits:

- [ ] **Improvement 1**: Description
  - **Rationale**: Why this would be nice to have
  - **Effort**: Estimated complexity

- [ ] **Improvement 2**: Another medium-priority item
  - **Rationale**: Benefit
  - **Effort**: Estimated complexity

### Low Priority / Nice to Have

Items that would be nice but aren't urgent:

- [ ] **Improvement 1**: Description
- [ ] **Improvement 2**: Another nice-to-have
- [ ] **Improvement 3**: Third low-priority item

## Technical Debt

Items that work but should be improved for maintainability, performance, or code quality.

| Item | Impact | Effort to Fix | Priority | Notes |
|------|--------|---------------|----------|-------|
| Debt item description | What needs improvement | Small/Medium/Large | High/Med/Low | Additional context |
| Another debt item | Impact on codebase | Estimated effort | Priority level | Context |

### Technical Debt Categories

Common categories to consider:
- **Code quality**: Refactoring needs, code smells
- **Testing**: Missing test coverage, brittle tests
- **Documentation**: Missing or outdated docs
- **Performance**: Known inefficiencies
- **Dependencies**: Outdated libraries, tight coupling
- **Architecture**: Design issues, circular dependencies

## Breaking Changes (Planned)

Future breaking changes that are anticipated or under consideration.

### Version X.0.0 (Target: Date or TBD)

#### Breaking Change 1: [Title]

**Current Behavior**: How it works now.

**Future Behavior**: How it will work after the change.

**Rationale**: Why this breaking change is necessary.

**Migration Path**: How users can adapt their code.

**Impact**: Who will be affected and how severely.

#### Breaking Change 2: [Another Change]

Repeat structure for each planned breaking change.

### Long-Term Breaking Changes (No Target Date)

Changes being considered but not yet scheduled:

- **Change 1**: Description and rationale
- **Change 2**: Another potential change
- **Change 3**: Third consideration

## Related Issues

Links to GitHub issues, discussions, or external resources.

### GitHub Issues

- Issue #XXX: [Title] - Brief description
- Issue #YYY: [Title] - Brief description
- Issue #ZZZ: [Title] - Brief description

### Related Specs

- [Spec Name](../path/to/spec.md): How it relates
- [Another Spec](../path/to/spec.md): Relationship

### External Resources

- [External Doc](https://example.com): Relevance
- [Library Issue](https://github.com/org/repo/issues/123): Dependency limitation

## Feedback and Contributions

How users can report issues or contribute improvements.

### Reporting Issues

If you encounter issues or limitations not listed here:
1. Check debug logs for detailed diagnostics
2. Review `AGENTS.md` for constitutional principles
3. Search existing GitHub issues to avoid duplicates
4. Open a new issue with:
   - Clear reproduction steps
   - Expected vs. actual behavior
   - Environment details (.NET version, OS, etc.)
   - Relevant logs or error messages

### Contributing Fixes

If you'd like to contribute:
1. Review the spec for architectural context
2. Discuss your approach in a GitHub issue first
3. Follow the TDD workflow (tests before implementation)
4. Update this document if your fix addresses a known issue
5. Submit a pull request with tests and documentation

## Status Tracking

### Features in Development

If work is actively ongoing:

| Feature | Status | Target Version | Notes |
|---------|--------|----------------|-------|
| Feature name | In Progress | vX.Y.Z | Who's working on it |

### Recently Completed

Recently addressed items (remove after a release or two):

- ✅ **Improvement**: Description (Completed in vX.Y.Z)
- ✅ **Bug fix**: Description (Fixed in vX.Y.Z)

---

**Template Version**: 1.0.0  
**Last Updated**: 2026-01-10

## Notes for Template Users

- Replace all placeholders in `[brackets]` with actual content
- Remove this "Notes for Template Users" section when creating real specs
- Be honest about limitations - users appreciate transparency
- Keep the "Current Limitations" table updated as workarounds are found
- Move completed improvements to "Recently Completed" section
- Remove "Recently Completed" items after 1-2 releases
- Link to actual GitHub issues when they exist
- Aim for 500-1,500 words (concise and actionable)
