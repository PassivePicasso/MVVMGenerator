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
                messageFormat: "Property '{0}' has a circular dependency chain: {1}",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);

            public static readonly DiagnosticDescriptor StaticType = new(
                id: "MGAN002",
                title: "Static type not supported",
                messageFormat: "Cannot generate property for field '{0}' because type '{1}' is static",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);

            public static readonly DiagnosticDescriptor DependencyNotFound = new(
                id: "MGAN003",
                title: "Property dependency not found",
                messageFormat: "Property '{0}' depends on property '{1}' which does not exist or is not an AutoNotify property",
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
                id: "MGAC002",
                title: "Invalid command method signature",
                messageFormat: "Method '{0}' has invalid signature. Commands must return void/Task and take 0-1 parameters. Found: {1}.",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);

            public static readonly DiagnosticDescriptor InvalidCanExecuteSignature = new(
                id: "MGAC003",
                title: "Invalid CanExecute signature",
                messageFormat: "CanExecute method '{0}' must return bool and have matching parameters with command method. Found: {1}.",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);

            // Optional CanExecute warning
            public static readonly DiagnosticDescriptor MissingCanExecute = new(
                id: "MGAC101", // Moved to 100+ series as it's not an error
                title: "Consider adding CanExecute method",
                messageFormat: "Command '{0}' has no CanExecute method defined. Consider adding method '{1}' for command validation.",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Info,
                isEnabledByDefault: false);
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