namespace Bennewitz.Ninja.CodeQuality;

/// <summary>
/// What every rule in this package shares: where it says it comes from.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>An ID is four letters and a number, and the rest is metadata.</b> Every analyzer a project
/// loads shares one flat namespace of IDs, in <c>#pragma</c>, <c>NoWarn</c> and
/// <c>.editorconfig</c>, so the ID stays short and unique; where a rule comes from is shown by its
/// help link, its category and the package id, not by a longer ID. <c>BNCQ</c> is <c>BN</c> plus the
/// product's initials, the scheme across the Bennewitz.Ninja quality packages, and once one ID has
/// shipped the prefix never changes, because every rename breaks a consumer's suppressions.
/// </para>
/// <para>
/// ⭐ <b>A rule's number is its AssemblyQuality counterpart's.</b> <c>BNCQ1004</c> finds at compile
/// time what <c>BNAQ1004</c> finds on the shipped assembly, so a consumer who knows one knows the
/// other. <c>BNCQ1003</c> is reserved: a forbidden use site is
/// <c>Microsoft.CodeAnalysis.BannedApiAnalyzers</c>' job already.
/// </para>
/// </remarks>
internal static class Catalog
{
    /// <summary>The category every rule reports under: the package, so a reader can find it.</summary>
    public const string Category = "Bennewitz.Ninja.CodeQuality";

    /// <summary>The rule's section of the README, which is also the package's nuget.org page.</summary>
    public static string HelpLink(string id) =>
        "https://github.com/JanusMael/Bennewitz.Ninja.CodeQuality#" + id.ToLowerInvariant();
}
