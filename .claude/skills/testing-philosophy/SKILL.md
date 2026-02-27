---
name: testing-philosophy
description: "This skill provides guidance on writing and running tests in this project. It should be used when writing new tests, understanding the test infrastructure, or making decisions about what to test. Covers TUnit configuration, unit tests, automation tests, and testing best practices."
---

# Testing Philosophy

This skill documents the testing approach and infrastructure for the agg-sharp project.

## Test Runner: TUnit

This project uses TUnit (v0.57.24) for all C# testing on .NET 8.0.

**Running tests:**
```bash
# Build the test project
dotnet build Tests/Agg.Tests/Agg.Tests.csproj

# Run all tests
dotnet test Tests/Agg.Tests/Agg.Tests.csproj

# Run with verbose output
dotnet test Tests/Agg.Tests/Agg.Tests.csproj --verbosity normal
```

**Running a single test (preferred method):**

Build the test project first, then run the compiled executable directly with `--treenode-filter`. This is faster and avoids `dotnet test` running all tests before filtering.

```bash
# Build once
dotnet build Tests/Agg.Tests/Agg.Tests.csproj

# Run a single test by method name (4 levels: assembly/namespace/class/method)
Tests\Agg.Tests\bin\Debug\Agg.Tests.exe --treenode-filter "/*/*/*/MyTestMethodName"

# Examples
Tests\Agg.Tests\bin\Debug\Agg.Tests.exe --treenode-filter "/*/*/*/BackBuffersAreScreenAligned"
Tests\Agg.Tests\bin\Debug\Agg.Tests.exe --treenode-filter "/*/*/*/DoubleBufferTests"

# Run all tests in a class (3 levels: assembly/namespace/class)
Tests\Agg.Tests\bin\Debug\Agg.Tests.exe --treenode-filter "/*/*/BackBufferTests"

# List all available tests
Tests\Agg.Tests\bin\Debug\Agg.Tests.exe --list-tests
```

> **Important:** `dotnet test --filter` with TUnit can be unreliable and may run all tests. Always prefer the executable + `--treenode-filter` approach for running specific tests.

**Configuration:**
- Test project: `Tests/Agg.Tests/Agg.Tests.csproj`
- Target framework: `net8.0-windows`
- TUnit attributes: `[Test]`, `[Before(Class)]`, `[After(Class)]`
- Assertions: `await Assert.That(value).IsEqualTo(expected)`

## Test Organization

Tests are organized by module:

- `Tests/Agg.Tests/` - All test files
- `Tests/Agg.Tests/Agg/` - Core 2D graphics tests (images, fonts, vertex sources)
- `Tests/Agg.Tests/Agg.UI/` - GUI widget unit tests (anchors, borders, lists, text)
- `Tests/Agg.Tests/Agg Automation Tests/` - GUI automation tests (widget clicks, menus, flow layout)
- `Tests/Agg.Tests/Agg.PolygonMesh/` - Mesh and CSG operation tests
- `Tests/Agg.Tests/Agg.RayTracer/` - Ray tracer tests (frustum, boolean, trace API)
- `Tests/Agg.Tests/Agg.VectorMath/` - Vector math tests
- `Tests/Agg.Tests/Agg.Csg/` - CSG primitive tests
- `Tests/Agg.Tests/Other/` - Misc tests (affine transforms, clipper, tessellator, vectors)
- `Tests/Agg.Tests/TestingInfrastructure/` - Test setup and helpers

**Test file naming:**
- `*Tests.cs` - Test classes that TUnit will discover
- Classes end with `Tests` suffix (e.g., `SimpleTests`, `MeshTests`, `VectorMathTests`)

## Core Testing Principles

### Speed Matters

Tests should run as fast as possible. Fast tests get run more often, which means faster feedback and fewer bugs reaching production.

- Prefer Standard (unit) tests over AutomationTests when possible
- Avoid unnecessary setup/teardown
- Don't test the same behavior multiple times
- Use `[Before(Class)]` for expensive one-time setup

### Test What Matters

Write tests for:
- Regressions (bugs that were fixed - prevent them from returning)
- Complex logic (algorithms, mesh operations, vector math, edge cases)
- GUI widget behavior (via automation tests)
- Graphics rendering and image processing operations

Avoid:
- Redundant tests that verify the same behavior
- Tests for trivial code
- Tests that just verify framework behavior

### Test Failures Are Real Bugs (No Cheating)

**Every test failure indicates a real bug in the production code.** Tests gate deployment and protect user experience - there are no workarounds.

When a test fails:

1. Investigate the failure
2. Add instrumentation (debug output) to understand what's happening
3. Find and fix the root cause in production code
4. Never weaken or skip tests to make them pass

**Forbidden actions:**
- Weakening assertions or changing expected values
- Using `[Skip]` as a permanent solution
- Using `--no-verify` to bypass pre-commit hooks
- Adding try/catch to swallow errors
- Mocking away the behavior being tested

See the `fix-test-failures` skill for the detailed debugging process.

## Standard Tests (Unit Tests)

Unit tests live in `Tests/Agg.Tests/` organized by module (e.g., `Agg/`, `Agg.UI/`, `Agg.PolygonMesh/`) and test isolated pieces of functionality.

**Characteristics:**
- Fast (milliseconds per test)
- No GUI required
- Isolated from other tests
- Use TUnit assertions with async/await

**Example structure:**
```csharp
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
    public class FeatureNameTests
    {
        [Test]
        public async Task ShouldDoExpectedBehavior()
        {
            // Arrange
            var input = CreateTestInput();

            // Act
            var result = ProductionCode.Process(input);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Count).IsEqualTo(3);
        }
    }
}
```

## Automation Tests

GUI automation tests live in `Tests/Agg.Tests/Agg Automation Tests/` and use `AutomationRunner.ShowWindowAndExecuteTests`.

**Characteristics:**
- Require GUI framework initialization (handled by `TestSetup` in `TestingInfrastructure/`)
- Test actual user interactions with widgets
- Slower than unit tests
- Use `[NotInParallel]` attribute for thread safety

**Example structure:**
```csharp
[NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
public class FeatureTests
{
    [Test]
    public async Task UserCanPerformAction()
    {
        var testWindow = new TestWindow(300, 200);
        await AutomationRunner.ShowWindowAndExecuteTests(
            testWindow,
            async (runner) =>
            {
                // Test implementation using automation runner
                await Assert.That(result).IsEqualTo(expected);
            });
    }
}
```

## When to Write Tests

**Always write tests for:**
- Bug fixes (regression test to prevent the bug from returning)
- Complex algorithms or mesh/geometry logic
- Critical user-facing features
- File format loading and saving
- Edge cases that are easy to break

**Consider skipping tests for:**
- Trivial one-line properties
- Code that's just wiring (no logic)
- Temporary/experimental code that will be rewritten

## Bug Fix Workflow: Failing Test First

**When fixing a bug, always write a failing test before writing the fix.**

This approach:
1. Proves the bug exists and is reproducible
2. Ensures you understand the actual problem
3. Verifies your fix actually works
4. Prevents the bug from returning (regression protection)

**The process:**
1. Reproduce the bug manually to understand it
2. Write a test that fails because of the bug
3. Run the test to confirm it fails (red)
4. Fix the bug in production code
5. Run the test to confirm it passes (green)
6. Commit both the test and the fix together
