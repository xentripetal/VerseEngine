# ADR 001: Allow Components to be Classes

## Status
Accepted (Implemented)

## Context
Initially, VerseEngine components were restricted to value types (structs) following traditional ECS patterns. This aligns with cache-friendly data layouts and performance optimization.

However, this restriction created challenges:
1. **Complex components** with inheritance hierarchies couldn't be modeled
2. **Large data structures** were expensive to copy
3. **Reference semantics** were sometimes more appropriate than value semantics
4. **Integration** with existing C# libraries expecting reference types was difficult

## Decision
We decided to allow components to be either **structs** or **classes**, giving developers flexibility to choose the appropriate type for their use case.

## Implementation
- Component registry now handles both value and reference types
- Storage system differentiates between struct and class components
- Class components store references in archetype storage
- Struct components continue to be stored inline for optimal cache performance

## Consequences

### Positive
- **Flexibility**: Can use classes when inheritance or reference semantics are needed
- **Interoperability**: Easier integration with existing C# code
- **Complex components**: Can model sophisticated component hierarchies
- **No copying overhead**: Large data structures passed by reference

### Negative
- **Performance variation**: Class components have indirection overhead
- **Cache locality**: Class components may cause cache misses
- **Memory management**: GC pressure from class allocations
- **Developer choice**: Developers must understand trade-offs

### Guidance
- **Prefer structs** for:
  - Simple data (positions, velocities, colors)
  - Hot path components accessed frequently
  - Components under 64 bytes

- **Use classes** for:
  - Complex hierarchies needing inheritance
  - Large data structures (>64 bytes)
  - Components requiring reference semantics
  - Integration with existing class-based APIs

## References
- Git commit: "Allow components to be classes"
- Related: ADR 002 (Resources as Structs)
