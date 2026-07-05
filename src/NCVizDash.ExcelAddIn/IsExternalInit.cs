namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Polyfill for the C# 9 <c>init</c> accessor / record feature, which needs this
    /// marker type to exist somewhere visible to the compiling assembly. Present in
    /// the BCL on .NET 5+ but not on .NET Framework 4.8 — Roslyn normally
    /// auto-synthesizes this when missing, but that synthesis is unreliable in some
    /// MSBuild/SDK-style net48 configurations, so it is declared explicitly here
    /// instead of relying on that behaviour. Internal (not public) so each assembly
    /// that needs it declares its own copy with no cross-assembly conflicts.
    /// </summary>
    internal static class IsExternalInit
    {
    }
}

