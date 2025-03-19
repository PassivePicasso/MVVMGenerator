using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;

namespace MVVM.Generator.Utilities
{
    internal static class TypeHelper
    {
        private static readonly Dictionary<SpecialType, string> SpecialNames = new Dictionary<SpecialType, string>
        {
            { SpecialType.System_Boolean, "bool" },
            { SpecialType.System_Byte, "byte" },
            { SpecialType.System_SByte, "sbyte" },
            { SpecialType.System_Char, "char" },
            { SpecialType.System_Decimal, "decimal" },
            { SpecialType.System_Double, "double" },
            { SpecialType.System_Single, "float" },
            { SpecialType.System_Int32, "int" },
            { SpecialType.System_UInt32, "uint" },
            { SpecialType.System_Int64, "long" },
            { SpecialType.System_UInt64, "ulong" },
            { SpecialType.System_Int16, "short" },
            { SpecialType.System_UInt16, "ushort" },
            { SpecialType.System_Object, "object" },
            { SpecialType.System_String, "string" },
            { SpecialType.System_Void, "void" },
        };

        public static string GetTypeName(ITypeSymbol typeSymbol)
        {
            // Handle special case for Nullable<T>
            var type = typeSymbol.Name;
            if (typeSymbol is INamedTypeSymbol namedType && namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                var elementType = namedType.TypeArguments[0];
                type = $"{GetTypeName(elementType)}?";
            }
            else if (typeSymbol is INamedTypeSymbol namedTypeSymbol)
            {
                if (SpecialNames.TryGetValue(namedTypeSymbol.SpecialType, out var specialName))
                {
                    type = specialName;
                }
                else
                {
                    var typeName = namedTypeSymbol.Name;
                    if (namedTypeSymbol.IsGenericType && !namedTypeSymbol.IsNullableType())
                    {
                        var typeArguments = namedTypeSymbol.TypeArguments;
                        var typeArgumentNames = typeArguments.Select(arg => GetTypeName(arg)).ToArray();
                        typeName = $"{typeName}<{string.Join(", ", typeArgumentNames)}>";
                    }
                    type = typeName;
                }
            }
            else if (typeSymbol.TypeKind == TypeKind.Array)
            {
                var arrayTypeSymbol = (IArrayTypeSymbol)typeSymbol;
                var elementType = GetTypeName(arrayTypeSymbol.ElementType);
                type = $"{elementType}[]";
            }
            else
            {
                type = typeSymbol.Name;
            }

            // Only add nullable annotation if it's not already a Nullable<T>
            if (!(typeSymbol is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T })
                && typeSymbol.NullableAnnotation == NullableAnnotation.Annotated)
            {
                type = $"{type}?";
            }

            return type;
        }

        private static bool IsNullableType(this INamedTypeSymbol type)
        {
            return type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
        }
    }
}