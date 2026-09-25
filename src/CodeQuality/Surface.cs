using Microsoft.CodeAnalysis;

namespace Bennewitz.Ninja.CodeQuality;

/// <summary>
/// What a consumer outside the assembly can see: the public surface the rules examine.
/// </summary>
internal static class Surface
{
    /// <summary>
    /// Whether <paramref name="symbol"/> is visible from another assembly: public or protected at
    /// every level, up through its containing types.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Protected counts.</b> A protected member is part of the contract a subclass in another
    /// assembly binds to, so it is held to the same rules as a public one. <c>private protected</c>
    /// is not, and neither is anything inside an internal type.
    /// </remarks>
    public static bool IsVisibleOutside(ISymbol symbol)
    {
        for (ISymbol? current = symbol; current is not null and not INamespaceSymbol; current = current.ContainingSymbol)
        {
            switch (current.DeclaredAccessibility)
            {
                case Accessibility.Public:
                case Accessibility.Protected:
                case Accessibility.ProtectedOrInternal:
                    continue;
                default:
                    return false;
            }
        }

        return true;
    }
}
