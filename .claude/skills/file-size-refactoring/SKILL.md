---
name: file-size-refactoring
description: This skill provides guidance for fixing file size violations detected by FileComplianceTests. Use when a C# file exceeds its line limit (800 lines default, or explicit limit for legacy files). The skill explains strategies to reduce file size while maintaining code quality.
---

# File Size Refactoring

This skill provides strategies for reducing file size to keep files manageable and maintainable.

## Why File Size Limits Exist

The limit exists for one reason: **a smaller file is easier for a human to understand, navigate, and maintain.** When a file grows beyond ~800 meaningful lines, it almost always means it has accumulated too many responsibilities. Hitting the limit is a healthy signal that the file deserves structural attention.

The correct response is always to **decompose the file into smaller, cohesive pieces** -- never to compress the existing code to squeeze under the limit.

## What Gets Measured

File size is measured as **non-empty lines** (excluding blank lines and whitespace-only lines). Comments, code, braces, usings -- all count. The default limit is **800 non-empty lines**.

## The Only Valid Approaches

Regardless of whether a file is 5 lines or 500 lines over the limit, the approach is the same:

### 1. Remove Dead Code

Unused methods, commented-out code blocks, unreachable branches, unused usings -- remove them. This isn't about hitting a number; dead code hurts readability at any file size. This is general housekeeping.

### 2. Extract by Responsibility

Identify distinct responsibilities and move them to separate classes/files:
- Helper methods that don't depend on instance state -> static utility class
- Data transformation logic -> dedicated service class
- Validation logic -> separate validators
- Event handlers that form a subsystem -> separate handler class

### 3. Extract by Feature

Group related methods that implement a specific feature:
- All methods related to "mesh operations" -> `MeshOperations.cs`
- All methods related to "file import" -> `FileImportService.cs`
- All methods related to "undo/redo" -> `UndoRedoManager.cs`

### 4. Extract Partial Classes

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

### 5. Extract Interfaces and Implementations

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

## How to Evaluate an Extraction

Before splitting a file, ask:

- **Can you give the new file a clear, purposeful name?** If not, the split is probably artificial.
- **Does the extracted piece represent a cohesive concept?** It should make sense on its own, not just be "the second half of the file."
- **Would a new developer understand why this is its own file?** The structure should be self-evident.
- **Why did the file grow?** Understanding the pattern (feature creep, accumulated helpers, multiple responsibilities) leads to better decomposition than just looking for the biggest method to extract.

## C# Specific Patterns

- **Extension methods**: Move extension methods to their own static class files
- **Nested classes**: Extract nested classes to their own files
- **Constants/Enums**: Move large enum definitions or constant collections to dedicated files

## What NOT to Do

- **Don't remove blank lines, comments, or whitespace to shrink the file** -- This reduces readability, which is the exact opposite of the goal. The limit exists to *improve* readability, not to create pressure to sacrifice it.
- **Don't consolidate statements or compress code style** -- Multi-line formatting that aids readability should stay. Never trade clarity for line count.
- **Don't create artificial splits** -- Extracted classes should represent cohesive functionality, not arbitrary chunks. If you can't name it well, don't split it.
- **Don't treat small overages differently** -- A file at 801 lines needs the same structural thinking as one at 1200 lines. The extraction might be smaller, but the approach is the same.

## After Refactoring

1. Build to verify nothing is broken: `dotnet build agg-sharp.sln`
2. Run tests: `dotnet test Tests/Agg.Tests/Agg.Tests.csproj`
3. Verify the refactored file is under 800 non-empty lines
