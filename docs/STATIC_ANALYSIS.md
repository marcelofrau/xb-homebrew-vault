Static analysis added

What added:
- Microsoft.CodeAnalysis.NetAnalyzers (Roslyn recommended rules)
- StyleCop.Analyzers (style rules)

How it works:
- Analyzers run during `dotnet build` and in IDE (VS/VSCode) if configured.
- Warnings and errors surface as compiler diagnostics.

Next steps:
1. Run `dotnet build` to see analyzer warnings.
2. Triage issues: decide which rules to treat as errors vs warnings.
3. Add editorconfig to configure severity and style rules.
4. Optionally integrate SonarCloud/GitHub Advanced Security for continuous scanning.
