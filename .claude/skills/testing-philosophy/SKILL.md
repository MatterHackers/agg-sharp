---
name: testing-philosophy
description: "This skill provides guidance on writing and running tests in this project. It should be used when writing new tests, understanding the test infrastructure, or making decisions about what to test. Covers TUnit configuration, unit tests, automation tests, and testing best practices."
---

# Testing Philosophy

This skill documents the testing approach and infrastructure for the agg-sharp project.

## Why We Test

Tests exist to give us **confidence to change code.** A good test suite means you can refactor, fix bugs, and add features knowing that if you break something, you'll find out immediately -- not from a user report weeks later.

This means tests are most valuable when they're fast (so you actually run them), when they test real behavior (so passing means something), and when they fail for meaningful reasons (so a failure tells you what went wrong).

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

## What to Test

### Write tests for:
- **Bug fixes** -- a regression test proves the bug is fixed and prevents it from returning. This is the highest-value test you can write.
- **Complex logic** -- algorithms, mesh operations, vector math, edge cases where it's easy to introduce subtle errors
- **GUI widget behavior** -- via automation tests, for interactions that must not break
- **Graphics rendering and image processing** -- operations where correctness is visually important

### Consider skipping tests for:
- Trivial one-line properties with no logic
- Code that's pure wiring (no branching, no computation)
- Temporary/experimental code that will be rewritten soon

### Speed matters
Fast tests get run more often, which means faster feedback and fewer bugs reaching production.
- Prefer unit tests over automation tests when possible
- Avoid unnecessary setup/teardown
- Don't test the same behavior multiple times
- Use `[Before(Class)]` for expensive one-time setup

## Bug Fix Workflow: Failing Test First

When fixing a bug, write a failing test before writing the fix. This approach:

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

## When Tests Fail

A failing test is valuable information. The goal is always to understand what it's telling you before changing anything.

Most failures are real bugs in production code. Occasionally a test has a problem, or requirements genuinely changed. In every case, investigate first -- see the `fix-test-failures` skill for the full diagnostic process.

The key distinction: **updating a test because requirements changed** is fine. **Weakening a test to make it pass without understanding why it failed** hides real problems.

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
