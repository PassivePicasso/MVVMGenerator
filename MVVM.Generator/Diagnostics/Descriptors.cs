using Microsoft.CodeAnalysis;

namespace MVVM.Generator.Diagnostics;

public static class Descriptors
{
    // Property System (MVVM001-MVVM100)
    public static readonly DiagnosticDescriptor CircularDependency = new(
        id: "MVVM001",
        title: "Circular property dependency detected",
        messageFormat: "Property '{0}' has a circular dependency with '{1}'",
        category: "Properties",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Properties cannot have circular dependencies as this would cause infinite update loops."
    );

    public static readonly DiagnosticDescriptor InvalidPropertyType = new(
        id: "MVVM002",
        title: "Invalid property type",
        messageFormat: "Type '{0}' is not supported for properties",
        category: "Properties",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor PropertyNameCollision = new(
        id: "MVVM003",
        title: "Property name collision",
        messageFormat: "Property '{0}' conflicts with existing member",
        category: "Properties",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    // Command System (MVVM101-MVVM200)
    public static readonly DiagnosticDescriptor InvalidCommandMethod = new(
        id: "MVVM101",
        title: "Invalid command method signature",
        messageFormat: "Method '{0}' has invalid signature for command: {1}",
        category: "Commands",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor AsyncCommandWithoutTask = new(
        id: "MVVM102",
        title: "Async command must return Task",
        messageFormat: "Async command method '{0}' must return Task or Task<T>",
        category: "Commands",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor CommandNameCollision = new(
        id: "MVVM103",
        title: "Command name collision",
        messageFormat: "Command '{0}' conflicts with existing member",
        category: "Commands",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    // Validation System (MVVM201-MVVM300)
    public static readonly DiagnosticDescriptor InvalidValidationRule = new(
        id: "MVVM201",
        title: "Invalid validation rule",
        messageFormat: "Validation rule '{0}' is invalid: {1}",
        category: "Validation",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor CircularValidation = new(
        id: "MVVM202",
        title: "Circular validation dependency",
        messageFormat: "Validation for '{0}' creates circular dependency",
        category: "Validation",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    // Navigation System (MVVM301-MVVM400)
    public static readonly DiagnosticDescriptor InvalidNavigationTarget = new(
        id: "MVVM301",
        title: "Invalid navigation target",
        messageFormat: "Navigation target '{0}' is invalid: {1}",
        category: "Navigation",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    // Performance Warnings (MVVM901-MVVM999)
    public static readonly DiagnosticDescriptor LargePropertyChain = new(
        id: "MVVM901",
        title: "Large property dependency chain",
        messageFormat: "Property '{0}' has {1} dependent properties which may impact performance",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor ExpensiveDefaultValue = new(
        id: "MVVM902",
        title: "Expensive default value",
        messageFormat: "Property '{0}' has potentially expensive default value initialization",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    // Code Style (MVVM801-MVVM899)
    public static readonly DiagnosticDescriptor InconsistentNaming = new(
        id: "MVVM801",
        title: "Inconsistent naming convention",
        messageFormat: "Member '{0}' does not follow naming convention: {1}",
        category: "Style",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor MissingDocumentation = new(
        id: "MVVM802",
        title: "Missing documentation",
        messageFormat: "Public member '{0}' is missing documentation",
        category: "Style",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true
    );
    
    public static class AutoNotifyDiagnostics
    {
        private const string Category = "Usage";

        public static readonly DiagnosticDescriptor StaticField = new(
            id: "AN001",
            title: "Static field with AutoNotify",
            messageFormat: "Field '{0}' with AutoNotifyAttribute must not be static",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor NamingConflict = new(
            id: "AN002",
            title: "Property naming conflict",
            messageFormat: "Generated property name '{0}' conflicts with existing member",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidPropertyChangedHandler = new(
            id: "AN003",
            title: "Invalid PropertyChanged handler",
            messageFormat: "PropertyChangedHandler '{0}' for field '{1}' is invalid: {2}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidCollectionChangedHandler = new(
            id: "AN004",
            title: "Invalid CollectionChanged handler",
            messageFormat: "CollectionChangedHandler '{0}' for field '{1}' is invalid: {2}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }
    public static class AutoCommandDiagnostics
    {
        private const string Category = "Usage";

        public static readonly DiagnosticDescriptor NotPublic = new(
            id: "AC001",
            title: "AutoCommand method must be public",
            messageFormat: "Method '{0}' with AutoCommandAttribute must be public",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor TooManyParameters = new(
            id: "AC002",
            title: "AutoCommand method has too many parameters",
            messageFormat: "Method '{0}' must have zero or one parameter",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidCanExecute = new(
            id: "AC003",
            title: "Invalid CanExecute method",
            messageFormat: "CanExecute method '{0}' for command '{1}' is invalid: {2}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor NamingConflict = new(
            id: "AC004",
            title: "Command naming conflict",
            messageFormat: "Generated command class name '{0}' conflicts with existing member",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }
}