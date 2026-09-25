; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md
;
; Every release moves the rules it ships from AnalyzerReleases.Unshipped.md into a "## Release <version>"
; section here, in the same change as the tag.

## Release 2026.3.925

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
BNCQ1001 | Bennewitz.Ninja.CodeQuality | Disabled | A visible method lets the caller leave the cancellation token out
BNCQ1002 | Bennewitz.Ninja.CodeQuality | Warning | A type from a leak-prone namespace appears in the visible surface
BNCQ1004 | Bennewitz.Ninja.CodeQuality | Warning | A namespace segment shadows the root namespace of a referenced assembly
