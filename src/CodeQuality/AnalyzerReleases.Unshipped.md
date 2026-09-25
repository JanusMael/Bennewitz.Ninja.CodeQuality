; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
BNCQ1001 | Bennewitz.Ninja.CodeQuality | Disabled | A visible method lets the caller leave the cancellation token out
BNCQ1002 | Bennewitz.Ninja.CodeQuality | Warning | A type from a leak-prone namespace appears in the visible surface
BNCQ1004 | Bennewitz.Ninja.CodeQuality | Warning | A namespace segment shadows the root namespace of a referenced assembly
