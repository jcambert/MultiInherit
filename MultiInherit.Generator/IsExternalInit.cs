// Polyfill requis pour utiliser 'record' et 'init' sur netstandard2.0.
// Le compilateur C# cherche ce type dans le namespace System.Runtime.CompilerServices ;
// s'il ne le trouve pas dans le framework cible, il faut le déclarer manuellement.

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}