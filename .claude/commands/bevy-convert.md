## Conversion Request

Convert the following Bevy component/system from Rust to C# for the Verse engine project:

**Bevy Component/System:** #$1
**Rust Source:** #$2
**C# Target:** #$3

Bevy is located at ~/rust/bevy. If you do not have access to this directory, report an error and stop. It will need to
be added to context

## Conversion Requirements

### 1. Architecture Mapping

- Map Rust structs to C# structs (prefer structs over classes for components)
- Discriminated unions should use a custom type that tries to match similar behavior while minimizing memory footprint.
- Translate Rust traits to C# interfaces where applicable
- Maintain the same API surface and functionality as the original Bevy implementation

### 2. Code Style Requirements

- Follow existing Verse C# conventions and patterns
- Use nullable reference types
- Apply appropriate C# naming conventions (PascalCase for public members, camelCase for private)
- Use modern C# features (pattern matching, record types where appropriate)
- Add XML documentation comments for public APIs

### 3. ECS Integration

- Components should be structs implementing appropriate interfaces
- Systems should be methods with proper parameter injection. A partial <Namespace>Systems struct should be made for
  related systems and each system should have a [Schedule] attribute on its method for code generation
- Use Verse.ECS query patterns and access methods
- Leverage code generation attributes where beneficial for performance

### 4. Performance Considerations

- Minimize heap allocations in hot paths
- Use `Span<T>` and `ReadOnlySpan<T>` for array operations when appropriate
- Prefer stack allocation for temporary data structures
- Consider unsafe code only when necessary for interop or critical performance

### 5. Dependencies and Integration

- Place the implementation in the most appropriate Verse library:
    - Core components → `Verse.Core`
    - ECS-specific → `Verse.ECS`
    - Math-related → `Verse.Math`
    - Rendering → `Verse.Render`
    - Transform/spatial → `Verse.Transform`
    - Assets → `Verse.Assets`
- Add necessary project references and using statements
- Follow the plugin registration pattern for systems

### 6. Testing Requirements

- Create corresponding test classes in the appropriate test project
- Test all public methods and edge cases
- Include performance benchmarks if the component is performance-critical
- Follow xUnit testing patterns used in existing Verse tests

### 7. Documentation

- Maintain semantic equivalence with Bevy documentation. Have an initial short summary and add additional details as
  remarks
  or other relevant xmldoc tags.
- Reference the original Bevy component as a <remarks> comment.
- Document any C#-specific adaptations or differences from the Rust version
- Include usage examples in XML docs

## Expected Deliverables

1. **Component/System Implementation**: Complete C# implementation
2. **Integration Code**: Plugin registration and system scheduling
3. **Unit Tests**: Comprehensive test coverage
4. **Documentation**: XML docs and usage examples
5. **Migration Notes**: Any differences from the Bevy original and reasoning

## Context Notes

- The Verse engine targets .NET 10.0
- We use a custom ECS system with code generation for performance
- The project follows Bevy's plugin architecture adapted for C#
- Performance is critical, especially for rendering and ECS operations
- Maintain compatibility with the existing Verse component ecosystem
