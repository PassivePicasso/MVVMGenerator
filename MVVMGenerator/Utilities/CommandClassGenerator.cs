using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace MVVM.Generator.Utilities;

public class CommandClassGenerator
{
    public void AddCommandClass(List<string> definitions, IMethodSymbol symbol, string className, string canExecuteMethodName)
    {
        string methodCall;
        string canExecute;
        string callerSource = symbol.IsStatic ? symbol.ContainingType.Name : "_owner";

        methodCall = $"""
                {callerSource}.{symbol.Name}();
""";
        canExecute = !string.IsNullOrEmpty(canExecuteMethodName)
            ? $"""
                return {callerSource}.{canExecuteMethodName}();
"""
            : """
                return true;
""";

        if (symbol.Parameters.Length == 1)
        {
            string parameterType = symbol.Parameters[0].Type.Name;
            methodCall = $$"""
                if(parameter is not {{parameterType}} typedParameter) return;
                    {{callerSource}}.{{symbol.Name}}(typedParameter);
""";

            canExecute = !string.IsNullOrEmpty(canExecuteMethodName)
                       ? $$"""
                if(parameter is not {{parameterType}} typedParameter) return false;
                    return {{callerSource}}.{{canExecuteMethodName}}(typedParameter);
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

        var ctorBody = symbol.IsStatic ? string.Empty : $"""
                _owner = owner;
""";
        var constructor = $$"""
            public {{className}}({{(symbol.IsStatic ? string.Empty : $"{symbol.ContainingType.Name} owner")}})
            {
{{ctorBody}}
            }
""";

        definitions.Add($$"""
        public class {{className}} : ICommand
        {
            public event EventHandler CanExecuteChanged = delegate { };
{{ownerField}}
{{constructor}}
            public bool CanExecute(object? parameter) 
            {
{{canExecute}}
            }

            public void Execute(object? parameter)
            {
{{methodCall}} 
            }
        }
""");
    }
}
