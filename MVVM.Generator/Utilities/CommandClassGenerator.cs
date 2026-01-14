using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;

namespace MVVM.Generator.Utilities;

public class CommandClassGenerator
{
    private const string LogPrefix = "CommandClassGenerator: ";

    public void AddCommandClass(
        List<string> definitions,
        IMethodSymbol symbol,
        string className,
        string canExecuteMemberName,
        bool canExecuteIsProperty,
        IReadOnlyList<string>? dependencies = null)
    {
        LogManager.Log($"{LogPrefix}Starting generation for {className}");
        var startTime = System.Diagnostics.Stopwatch.StartNew();
        string methodCall;
        string canExecute;
        string callerSource = symbol.IsStatic ? symbol.ContainingType.Name : "_owner";
        bool isAsync = IsAsyncMethod(symbol);
        bool hasDependencies = dependencies != null && dependencies.Count > 0;

        try
        {
            // For async methods, use await; for sync, just call directly
            var awaitPrefix = isAsync ? "await " : "";
            methodCall = $"""
                {awaitPrefix}{callerSource}.{symbol.Name}();
""";
            // For properties, access directly; for methods, call with ()
            var canExecuteInvocation = canExecuteIsProperty ? canExecuteMemberName : $"{canExecuteMemberName}()";
            canExecute = !string.IsNullOrEmpty(canExecuteMemberName)
                ? $"""
                return {callerSource}.{canExecuteInvocation};
"""
                : """
                return true;
""";

            if (symbol.Parameters.Length == 1)
            {
                string parameterType = symbol.Parameters[0].Type.Name;
                methodCall = $$"""
                if(parameter is not {{parameterType}} typedParameter) return;
                    {{awaitPrefix}}{{callerSource}}.{{symbol.Name}}(typedParameter);
""";

                // For parameterized commands, CanExecute must be a method (properties can't take parameters)
                canExecute = !string.IsNullOrEmpty(canExecuteMemberName)
                           ? $$"""
                if(parameter is not {{parameterType}} typedParameter) return false;
                    return {{callerSource}}.{{canExecuteMemberName}}(typedParameter);
"""
                           : $"""
                return parameter is {parameterType};
""";
            }

            var ownerField = symbol.IsStatic
                ? """

"""
                : $$"""
            readonly {{symbol.ContainingType.Name}} _owner;

""";

            // Generate constructor with PropertyChanged subscription if we have dependencies
            string ctorBody;
            string disposeMethod = "";

            if (symbol.IsStatic)
            {
                ctorBody = "";
            }
            else if (hasDependencies)
            {
                ctorBody = $"""
                _owner = owner;
                _owner.PropertyChanged += OnOwnerPropertyChanged;
""";
                disposeMethod = GeneratePropertyChangedHandler(dependencies!);
            }
            else
            {
                ctorBody = """
                _owner = owner;
""";
            }

            var constructor = $$"""
            public {{className}}({{(symbol.IsStatic ? string.Empty : $"{symbol.ContainingType.Name} owner")}})
            {
{{ctorBody}}
            }
""";

            // Use async void for Execute when method is async (standard ICommand pattern)
            var asyncModifier = isAsync ? "async " : "";

            // Remove the pragma warning disable if we're actually using the event
            var pragmaDisable = hasDependencies ? "" : "#pragma warning disable CS0067 // Event is never used\n";
            var pragmaRestore = hasDependencies ? "" : "#pragma warning restore CS0067\n";

            definitions.Add($$"""
{{pragmaDisable}}        public class {{className}} : ICommand
        {
            public event EventHandler? CanExecuteChanged;
{{ownerField}}
{{constructor}}
            public bool CanExecute(object? parameter)
            {
{{canExecute}}
            }

            public {{asyncModifier}}void Execute(object? parameter)
            {
{{methodCall}}
            }
{{disposeMethod}}
        }
{{pragmaRestore}}
""");
            LogManager.Log($"{LogPrefix}Completed {className} generation in {startTime.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            LogManager.LogError($"{LogPrefix}Failed to generate {className}", ex);
            throw;
        }
    }

    private static string GeneratePropertyChangedHandler(IReadOnlyList<string> dependencies)
    {
        var propertyChecks = string.Join(" || ", dependencies.Select(d => $"e.PropertyName == \"{d}\""));

        return $$"""

            private void OnOwnerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
            {
                if ({{propertyChecks}})
                {
                    CanExecuteChanged?.Invoke(this, EventArgs.Empty);
                }
            }
""";
    }

    private static bool IsAsyncMethod(IMethodSymbol method)
    {
        return method.ReturnType.Name == "Task" &&
               method.ReturnType.ContainingNamespace?.ToString() == "System.Threading.Tasks";
    }
}
