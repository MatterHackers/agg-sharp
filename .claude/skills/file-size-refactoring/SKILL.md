---
name: file-size-refactoring
description: This skill provides guidance for fixing file size violations detected by FileComplianceTests. Use when a C# file exceeds its line limit (800 lines default, or explicit limit for legacy files). The skill explains strategies to reduce file size while maintaining code quality.
---

# File Size Refactoring

This skill provides strategies for reducing file size when `FileComplianceTests` reports a violation.

## When This Applies

The test counts **non-empty lines** (excluding blank lines and whitespace-only lines). A file fails when it exceeds:
- **800 lines** (default limit for all files)
- **Explicit limit** for legacy files listed in `ExplicitFileLimits` in the test class

## Quick Fixes (for small overages of 1-10 lines)

When a file is only slightly over the limit, prefer minimal changes:

1. **Remove unnecessary blank lines** - Look for double blank lines or blank lines inside methods that don't aid readability
2. **Consolidate short statements** - Combine related single-line statements where it doesn't hurt readability
3. **Remove dead code** - Look for commented-out code, unused usings, or unreachable branches
4. **Simplify conditionals** - Early returns can sometimes eliminate nesting

## Refactoring Strategies (for larger overages)

When a file significantly exceeds the limit, extract cohesive functionality:

### 1. Extract by Responsibility

Identify distinct responsibilities and move them to separate classes/files:
- Helper methods that don't depend on instance state -> static utility class
- Data transformation logic -> dedicated service class
- Validation logic -> separate validators
- Event handlers that form a subsystem -> separate handler class

### 2. Extract by Feature

Group related methods that implement a specific feature:
- All methods related to "mesh operations" -> `MeshOperations.cs`
- All methods related to "file import" -> `FileImportService.cs`
- All methods related to "undo/redo" -> `UndoRedoManager.cs`

### 3. Extract Partial Classes

For large classes where methods are tightly coupled to instance state, use C# partial classes:
```csharp
// SceneContext.cs - core functionality
public partial class SceneContext
{
    // Core scene management methods
}

// SceneContext.Selection.cs - selection-related methods
public partial class SceneContext
{
    // Selection management methods
}

// SceneContext.UndoRedo.cs - undo/redo methods
public partial class SceneContext
{
    // Undo/redo methods
}
```

### 4. Extract Interfaces and Implementations

When a class has multiple distinct interfaces:
```csharp
// Before: one large class
public class PrinterService
{
    // 200 lines of connection management
    // 200 lines of print job management
    // 200 lines of status monitoring
    // 200 lines of configuration
}

// After: interface-segregated classes
public class PrinterConnectionService : IPrinterConnection { ... }
public class PrintJobService : IPrintJobManager { ... }
public class PrinterStatusService : IPrinterStatus { ... }
public class PrinterConfigService : IPrinterConfig { ... }
```

## C# Specific Patterns

- **Extension methods**: Move extension methods to their own static class files
- **Nested classes**: Extract nested classes to their own files
- **Constants/Enums**: Move large enum definitions or constant collections to dedicated files
- **LINQ expressions**: Complex LINQ can sometimes be extracted to named methods for clarity and line count

## What NOT to Do

- **Don't add to ExplicitFileLimits** - That dictionary is only for freezing existing legacy files at their current size. Limits should only ever decrease.
- **Don't just delete comments to meet the limit** - Comments don't count toward the line limit anyway (only non-empty lines are counted)
- **Don't sacrifice readability** - If consolidating code makes it harder to understand, don't do it
- **Don't create artificial splits** - Extracted classes should represent cohesive functionality, not arbitrary chunks

## After Refactoring

1. Build to verify nothing is broken: `dotnet build agg-sharp.sln`
2. Run tests: `dotnet test Tests/Agg.Tests/Agg.Tests.csproj`
3. Verify the refactored file is under 800 non-empty lines

**Note:** agg-sharp does not currently have a FileComplianceTests test. These guidelines are general best practices for keeping files manageable.
