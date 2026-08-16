namespace System.Runtime.CompilerServices
{
    // netstandard2.0 lacks this type, which the compiler requires for the
    // init accessors that records generate.
    internal static class IsExternalInit { }
}
