; Unshipped analyzer releases — rules added in this cycle
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MI0001 | MultiInherit | Error | Parent model not found
MI0002 | MultiInherit | Error | Circular inheritance detected
MI0003 | MultiInherit | Error | Model class must be partial
MI0004 | MultiInherit | Error | Compute method not found
MI0005 | MultiInherit | Error | Computed property must be read-only
MI0006 | MultiInherit | Error | Foreign key property name collision
MI0007 | MultiInherit | Error | Constrains method not found
MI0008 | MultiInherit | Error | Onchange method not found
MI0009 | MultiInherit | Error | One2many inverse field not found
MI0010 | MultiInherit | Error | Relation comodel not found
MI0011 | MultiInherit | Error | Computed property must be partial
MI0012 | MultiInherit | Error | Selection field must be a string property
MI0013 | MultiInherit | Error | Default method not found
MI0101 | MultiInherit | Warning | Inherited field name conflict
MI0102 | MultiInherit | Warning | Model in global namespace
