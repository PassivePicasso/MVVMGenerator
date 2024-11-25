using Microsoft.CodeAnalysis;

namespace MVVM.Generator.Diagnostics;

public static class Descriptors
{
    public static class Analzyer
    {
        public static class AutoNotify
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
        public static class AutoCommand
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
}