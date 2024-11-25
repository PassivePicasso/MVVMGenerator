using Microsoft.CodeAnalysis;

namespace MVVM.Generator.Diagnostics;

public static class Descriptors
{
    public static class Generator
    {
        public static class AutoNotify
        {
            private const string Category = "Generator";

            public static readonly DiagnosticDescriptor CircularDependency = new(
                id: "MGAN001",
                title: "Circular property dependency detected",
                messageFormat: "Property '{0}' has a circular dependency chain",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);

            public static readonly DiagnosticDescriptor InvalidPropertyType = new(
                id: "MGAN002",
                title: "Invalid property type",
                messageFormat: "Cannot generate property for field '{0}' of type '{1}'",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);

            public static readonly DiagnosticDescriptor DependencyNotFound = new(
                id: "MGAN003",
                title: "Property dependency not found",
                messageFormat: "Property '{0}' depends on non-existent property '{1}'",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);
        }

        public static class AutoCommand
        {
            private const string Category = "Generator";

            public static readonly DiagnosticDescriptor NotPublic = new(
                id: "MGAC001",
                title: "Command method must be public",
                messageFormat: "Method '{0}' with AutoCommand attribute must be public",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);

            public static readonly DiagnosticDescriptor InvalidMethodSignature = new(
                id: "MGAC101",
                title: "Invalid command method signature",
                messageFormat: "Method '{0}' has invalid signature for command: {1}",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);

            public static readonly DiagnosticDescriptor MissingCanExecute = new(
                id: "MGAC102",
                title: "Missing CanExecute method",
                messageFormat: "CanExecute method '{0}' not found for command '{1}'",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);

            public static readonly DiagnosticDescriptor InvalidCanExecuteSignature = new(
                id: "MGAC103",
                title: "Invalid CanExecute signature",
                messageFormat: "CanExecute method '{0}' has invalid signature: {1}",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);
        }
    }
    public static class Analzyer
    {
        public static class AutoNotify
        {
            private const string Category = "Usage";

            public static readonly DiagnosticDescriptor StaticField = new(
                id: "MAANA001",
                title: "Static field with AutoNotify",
                messageFormat: "Field '{0}' with AutoNotifyAttribute must not be static",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);

            public static readonly DiagnosticDescriptor NamingConflict = new(
                id: "MAANA002",
                title: "Property naming conflict",
                messageFormat: "Generated property name '{0}' conflicts with existing member",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);

            public static readonly DiagnosticDescriptor InvalidPropertyChangedHandler = new(
                id: "MAANA003",
                title: "Invalid PropertyChanged handler",
                messageFormat: "PropertyChangedHandler '{0}' for field '{1}' is invalid: {2}",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);

            public static readonly DiagnosticDescriptor InvalidCollectionChangedHandler = new(
                id: "MAANA004",
                title: "Invalid CollectionChanged handler",
                messageFormat: "CollectionChangedHandler '{0}' for field '{1}' is invalid: {2}",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);
        }
        public static class AutoCommand
        {
            private const string Category = "Usage";

            public static readonly DiagnosticDescriptor NotPublic = new(
                id: "MAACA001",
                title: "AutoCommand method must be public",
                messageFormat: "Method '{0}' with AutoCommandAttribute must be public",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);

            public static readonly DiagnosticDescriptor TooManyParameters = new(
                id: "MAACA002",
                title: "AutoCommand method has too many parameters",
                messageFormat: "Method '{0}' must have zero or one parameter",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);

            public static readonly DiagnosticDescriptor InvalidCanExecute = new(
                id: "MAACA003",
                title: "Invalid CanExecute method",
                messageFormat: "CanExecute method '{0}' for command '{1}' is invalid: {2}",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);

            public static readonly DiagnosticDescriptor NamingConflict = new(
                id: "MAACA004",
                title: "Command naming conflict",
                messageFormat: "Generated command class name '{0}' conflicts with existing member",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);
        }
    }
}