# Source Generator Architecture Guidelines

## AI Assistant Collaboration Guidelines

### Interaction Model
- Suggest changes in small, focused chunks that can be easily reviewed and tested
- Provide clear rationale for architectural decisions
- Include specific file paths and affected components
- Use code blocks with clear boundary markers for changes
- Highlight potential impact on other components

### Communication Format
- Break down complex changes into sequential steps
- Group changes by file/component
- Provide brief context for each modification
- Use standard markdown for documentation
- Include relevant code snippets when discussing patterns

## Core Architecture

### Design Principles
- Maintain strict separation of concerns
- Keep generator logic independent of generated code
- Use interfaces to define clear boundaries
- Implement incremental generation

### Component Structure

```csharp
public interface ICodeGenerator 
{
    void Initialize(IncrementalGeneratorInitializationContext context);
    void Execute(SourceProductionContext context, ISymbol targetSymbol);
}
```

### Implementation Patterns
- Use incremental generators for performance
- Implement clear separation of concerns
- Follow interface-based design
- Cache expensive computations

### Generator Implementation Pattern

```csharp
[Generator]
public class ViewModelGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<ClassDeclarationSyntax> classes = 
            context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: (s, _) => s is ClassDeclarationSyntax c && IsTargetType(c),
                    transform: (ctx, _) => GetTargetType(ctx))
                .Where(m => m is not null);
                
        context.RegisterSourceOutput(classes, Execute);
    }
}
```

## Clean Architecture Implementation

### Layer Separation
- Generator Core (Domain Logic)
  - Attribute processing
  - Code generation rules
  - Model transformations
- Infrastructure
  - Roslyn integration
  - File system access
  - Diagnostic reporting
- Presentation
  - Generated code structure
  - Code formatting
  - Source mapping

### Dependency Management
- Flow dependencies from outer to inner layers
- Use interfaces to define boundaries
- Implement dependency injection where appropriate
- Avoid circular dependencies

## Extension Points

### Generator Pipeline
- Pre-generation hooks
- Post-generation validation
- Custom attribute handling
- Code transformation plugins

### Code Generation
- Template customization
- Naming conventions
- Output formatting
- Custom generators

## Best Practices

### Performance
- Use object pooling for frequent allocations
- Cache compilation artifacts
- Implement proper disposal patterns
- Profile generator performance

Example:
```csharp
private static readonly ObjectPool<StringBuilder> _stringBuilderPool = 
    new DefaultObjectPoolProvider().CreateStringBuilderPool();

public string GenerateCode()
{
    using var builder = _stringBuilderPool.Get();
    // Generate code
    return builder.ToString();
}
```

### Error Handling
- Provide clear, actionable diagnostic messages
- Include source locations in errors
- Handle edge cases gracefully

Example:
```csharp
context.ReportDiagnostic(Diagnostic.Create(
    new DiagnosticDescriptor(
        id: "MVVM001",
        title: "Invalid property configuration",
        messageFormat: "Property '{0}' has invalid configuration: {1}",
        category: "MVVM",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true),
    Location.Create(syntax.SyntaxTree, syntax.Span),
    propertyName, details));
```

## Performance Guidelines

### Optimization Strategies
- Cache compilation artifacts
- Implement incremental generation
- Pool frequently used objects
- Minimize allocations

### Resource Management
- Proper disposal patterns
- Memory usage monitoring
- Thread safety considerations
- Compilation context reuse

## Error Handling and Diagnostics

### Diagnostic System
- Clear error messages
- Source location tracking
- Action suggestions
- Warning levels

### Error Recovery
- Graceful degradation
- Partial generation
- State recovery
- Error reporting

### Integration Tests
- Test end-to-end generation
- Verify compilation
- Test with different C# versions
- Benchmark performance

## Implementation Process

### Development Workflow
1. Design changes in small, testable increments
2. Document architectural decisions
3. Implement core functionality
4. Add extension points
5. Optimize performance
6. Add diagnostics

### Change Management
- Review impact before implementation
- Test changes incrementally
- Document breaking changes
- Update affected components

## Documentation Requirements
1. Document generator architecture
2. Provide usage examples
3. Include troubleshooting guide
4. Document breaking changes
5. Maintain API documentation

## Security Considerations

### Input Validation
- Syntax validation
- Semantic analysis
- Malformed input handling
- Injection prevention

### Generated Code Safety
- Output sanitization
- Safe default values
- Access control verification
- Security annotations

## Implementation Checklist
- [ ] Implement IIncrementalGenerator
- [ ] Set up error handling
- [ ] Add comprehensive tests
- [ ] Document public APIs
- [ ] Profile performance
- [ ] Add source mapping
- [ ] Implement diagnostics

## Performance Monitoring
- Track generation time
- Monitor memory usage
- Profile hot paths
- Optimize critical sections

## Security Considerations
- Validate input syntax
- Sanitize generated code
- Handle malformed input
- Protect against injection

## Documentation Standards

### Code Documentation
- Clear XML comments
- Usage examples
- Extension points
- Breaking changes

### Architecture Documentation
- Component relationships
- Extension mechanisms
- Performance considerations
- Security guidelines
