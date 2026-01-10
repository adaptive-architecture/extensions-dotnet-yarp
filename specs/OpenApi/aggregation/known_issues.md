# OpenAPI Aggregation - Known Issues

> **Last Updated**: 2026-01-10

## Current Limitations

| Limitation | Severity | Impact | Workaround |
|------------|----------|--------|------------|
| Only OpenAPI 3.0+ supported | Medium | Services using Swagger 2.0 cannot be aggregated | Upgrade downstream services to OpenAPI 3.0 |
| Custom YARP transforms not analyzed | Low | Paths may be incorrectly included/excluded with custom transforms | Implement custom `IRouteTransformAnalyzer` for your custom transform types |
| No real-time config updates | Low | Changes to downstream specs require cache expiration | Reduce `CacheDuration` option, or wait for cache expiration (default 5 minutes) |
| Schema transformation limited to prefixing | Low | Cannot perform complex schema modifications (field filtering, transformation) | Implement custom `ISchemaRenamer` for advanced scenarios |
| No built-in authentication for fetching | Medium | Cannot fetch from authenticated downstream endpoints out-of-box | Implement custom `IOpenApiDocumentFetcher` with authentication headers |
| L1-only caching by default | Low | Cache not shared across instances without L2 configuration | Configure IDistributedCache (Redis, SQL Server, Postgres) for multi-instance scenarios - no code changes needed |
| JSON-in-JSON metadata format | Low | Awkward configuration syntax, no compile-time validation | Use careful JSON escaping; future versions may support strongly-typed config section |
| Limited OpenAPI 3.1 support | Low | May not handle newest OpenAPI 3.1 features fully | Use OpenAPI 3.0 specs; OpenAPI 3.1 support is partial and depends on Microsoft.OpenApi library |
| No GraphQL schema support | Low | Cannot aggregate GraphQL schemas | Use separate GraphQL federation tools; this extension is OpenAPI-specific |

## Known Bugs

**No known bugs at this time.** All identified issues during development have been resolved.

If you discover a bug, please report it via GitHub issues with:
- Steps to reproduce
- Expected vs. actual behavior
- Relevant logs (with debug logging enabled)
- Environment details (.NET version, YARP version)

## Future Improvements

### High Priority

- [ ] **Integration Tests**: Add end-to-end tests with TestContainers
  - **Rationale**: Validate full pipeline with real YARP instance and downstream services
  - **Effort**: Medium (requires TestContainers setup, test service creation)
  - **Benefit**: Increased confidence in real-world scenarios
  
- [ ] **Performance Benchmarks**: Establish baseline performance metrics
  - **Rationale**: Quantify cache hit/miss latency, memory usage, throughput
  - **Effort**: Small (BenchmarkDotNet setup)
  - **Benefit**: Performance regression detection, optimization guidance

- [ ] **Sample Application Completion**: Finalize OpenApiAggregation sample
  - **Rationale**: Currently in progress; needs more complex routing scenarios
  - **Effort**: Small (add a few more scenarios)
  - **Benefit**: Better learning resource for users

### Medium Priority

- [ ] **Authenticated Fetching**: Built-in support for authenticated downstream endpoints
  - **Rationale**: Common requirement for internal services behind auth
  - **Effort**: Medium (token management, auth scheme support)
  - **Benefit**: Reduces need for custom `IOpenApiDocumentFetcher` implementations

- [x] **Distributed Caching**: Support for Redis, SQL Server distributed caches *(Completed)*
  - **Status**: HybridCache supports L2 distributed caching out-of-box
  - **Configuration**: Just register `IDistributedCache` implementation
  - **Benefit**: Multi-instance cache sharing with zero code changes

- [ ] **Configuration Validation**: Validate metadata at startup
  - **Rationale**: Catch config errors early rather than at runtime
  - **Effort**: Small (validate during service registration)
  - **Benefit**: Improved developer experience, clearer error messages

- [ ] **OpenAPI Extension Preservation**: Preserve `x-*` vendor extensions
  - **Rationale**: Custom extensions may be lost during processing
  - **Effort**: Small (ensure extensions copied during merge)
  - **Benefit**: Full fidelity for specs with custom extensions

- [ ] **Rate Limiting for Fetching**: Add rate limiting for downstream fetches
  - **Rationale**: Protect downstream services from aggressive fetching
  - **Effort**: Small (use rate limiter library)
  - **Benefit**: Better downstream service protection

- [ ] **Configuration Reload Support**: React to YARP config changes
  - **Rationale**: Currently requires app restart for config changes
  - **Effort**: Medium (hook into YARP's config reload mechanism)
  - **Benefit**: Dynamic config updates without restart

### Low Priority / Nice to Have

- [ ] **Schema Deduplication**: Detect and merge identical schemas
  - **Rationale**: If two services have identical `User` schema, use one
  - **Effort**: Medium (schema comparison, ref rewriting)
  - **Benefit**: Smaller aggregated specs

- [ ] **Spec Versioning**: Track changes in downstream specs over time
  - **Rationale**: Useful for monitoring API evolution
  - **Effort**: Large (storage, diff computation, alerting)
  - **Benefit**: Visibility into API changes

- [ ] **Swagger UI Integration**: Built-in Swagger UI for aggregated docs
  - **Rationale**: Provide ready-to-use documentation viewer
  - **Effort**: Medium (embed Swagger UI, wire up endpoints)
  - **Benefit**: Complete documentation solution out-of-box

- [ ] **Offline Mode**: Cache specs persistently for offline operation
  - **Rationale**: Useful for air-gapped or disconnected scenarios
  - **Effort**: Medium (persistent cache implementation)
  - **Benefit**: Works without network access to downstream services

- [ ] **Multi-version Support**: Aggregate multiple versions of same API
  - **Rationale**: Support v1 and v2 of same service
  - **Effort**: Large (version tracking, conflict resolution)
  - **Benefit**: Better support for API versioning strategies

- [ ] **OpenAPI 3.1 Full Support**: Complete OpenAPI 3.1 compatibility
  - **Rationale**: Support latest OpenAPI features (webhooks, etc.)
  - **Effort**: Medium (depends on Microsoft.OpenApi library updates)
  - **Benefit**: Future-proof, modern OpenAPI feature support

## Technical Debt

| Item | Impact | Effort to Fix | Priority | Notes |
|------|--------|---------------|----------|-------|
| JSON-in-JSON metadata format | Awkward config experience, no validation | Medium | Low | Consider strongly-typed config section in future major version |
| Hardcoded metadata key "Ada.OpenApi" | Not configurable by users | Low | Low | Could be made configurable via options |
| No schema validation on fetched docs | Malformed specs may cause runtime errors | Medium | Medium | Add validation layer after fetching |
| Limited error context in logs | Some errors lack detailed context | Small | Low | Add more contextual info to error logs |
| No metrics/telemetry | Cannot monitor aggregation health | Medium | Medium | Add OpenTelemetry metrics for cache hits, fetch times, errors |

## Breaking Changes (Planned)

### Potential Breaking Changes in Future Versions

No breaking changes are currently planned for the next minor/patch versions. The following are under consideration for future major versions (v2.0.0+):

#### Potential: Metadata Format Change

**Current Behavior**: Configuration stored as JSON strings in `Ada.OpenApi` metadata key.

**Future Behavior**: Strongly-typed configuration section in `appsettings.json` or support for both formats.

**Rationale**: Improve developer experience with IntelliSense, compile-time validation, and cleaner syntax.

**Migration Path**: Provide automatic converter or support both formats during transition period.

**Impact**: Moderate - requires configuration changes in gateway `appsettings.json`.

#### Potential: Default Cache Duration Change

**Current Behavior**: 5 minutes default cache duration.

**Future Behavior**: Might increase to 10-15 minutes for better performance.

**Rationale**: Most OpenAPI specs change infrequently; longer cache reduces overhead.

**Migration Path**: Users can explicitly set `CacheDuration` in configuration to maintain current behavior.

**Impact**: Low - only affects users relying on default value.

#### Potential: NonAnalyzableStrategy Default Change

**Current Behavior**: `IncludeWithWarning` (conservative - includes paths when analysis uncertain).

**Future Behavior**: Might change to `ExcludeWithWarning` (strict - excludes paths when uncertain).

**Rationale**: Stricter default prevents accidental inclusion of unreachable paths.

**Migration Path**: Users can explicitly set `NonAnalyzableStrategy` to maintain current behavior.

**Impact**: Moderate - could result in paths being excluded that were previously included.

### Long-Term Considerations (No Target Date)

- **Change schema renaming default**: Could change from prefix-based to namespace-based
- **Deprecate IMemoryCache**: Move to distributed cache as default for better multi-instance support

## Related Issues

### GitHub Issues

No open GitHub issues at this time. The feature is in active development with stable core implementation.

### Related Specs

- **Main Spec**: [spec.md](spec.md) - Feature overview and architectural decisions
- **Technical Details**: [extra_details.md](extra_details.md) - Implementation deep dive

### External Resources

- **YARP Documentation**: https://microsoft.github.io/reverse-proxy/
- **OpenAPI Specification**: https://swagger.io/specification/
- **Microsoft.OpenApi Library**: https://github.com/microsoft/OpenAPI.NET

## Feedback and Contributions

### Reporting Issues

If you encounter issues or limitations not listed here:

1. **Enable debug logging** for detailed diagnostics:
   ```json
   {
     "Logging": {
       "LogLevel": {
         "AdaptArch.Extensions.Yarp.OpenApi": "Debug"
       }
     }
   }
   ```

2. **Review AGENTS.md** for constitutional principles and development practices

3. **Search existing GitHub issues** to avoid duplicates

4. **Open a new issue** with:
   - Clear reproduction steps
   - Expected vs. actual behavior
   - Environment details (.NET version, OS, YARP version)
   - Relevant logs or error messages
   - Minimal reproducible example if possible

### Contributing Fixes

If you'd like to contribute improvements:

1. **Review the spec** (`spec.md`) for architectural context and decisions
2. **Discuss your approach** in a GitHub issue first to align with maintainers
3. **Follow TDD workflow** (tests before implementation per AGENTS.md Section II)
4. **Update specs** if your changes affect design or architecture
5. **Update this document** if your fix addresses a known issue or limitation
6. **Add integration tests** if applicable (especially for new features)
7. **Submit a pull request** with:
   - Comprehensive tests
   - Updated documentation
   - Reference to related issue

## Status Tracking

### Features in Development

| Feature | Status | Target Version | Notes |
|---------|--------|----------------|-------|
| Sample Application | In Progress | v1.1.0 | Adding more complex routing scenarios |

### Recently Completed

- ✅ **Core Implementation**: All major components (Completed in v1.0.0)
- ✅ **Unit Test Suite**: 10 test files with comprehensive coverage (Completed in v1.0.0)
- ✅ **Basic Sample Application**: UserService, ProductService, Gateway (Completed in v1.0.0)
- ✅ **HybridCache Migration**: Modernized caching with built-in stampede protection, tag-based invalidation, and optional distributed cache (Completed in v1.1.0)
- ✅ **Cache Invalidation API**: Public API for programmatic cache invalidation (Completed in v1.1.0)

## Compatibility Notes

### .NET Version

- **Minimum**: .NET 10.0
- **Tested on**: .NET 10.0
- **Expected compatibility**: Should work on future .NET versions with minimal changes

### YARP Version

- **Built for**: YARP 2.x
- **Expected compatibility**: Future YARP versions (3.x+) may require updates if transform model changes

### OpenAPI Versions

- **Supported**: OpenAPI 3.0.x
- **Partial support**: OpenAPI 3.1 (depends on Microsoft.OpenApi library)
- **Not supported**: Swagger 2.0 / OpenAPI 2.0 (not planned for future support)

---

**Document Version**: 1.0.0  
**Last Updated**: 2026-01-10

## Document Maintenance

This document should be updated:
- When new limitations are discovered
- When improvements are implemented (move to "Recently Completed")
- When bugs are found or fixed
- When breaking changes are planned or implemented
- At major version milestones (review all sections for accuracy)

**Next Review Date**: 2026-04-10 (quarterly review)
