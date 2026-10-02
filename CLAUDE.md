# Claude Code Guidelines

## Philosophy

**YAGNI** - Don't build features until needed. Write the simplest code that works today.

**Circumstances alter cases** - Use judgment. There are no rigid rules—context determines the right approach.

**YAGNI does not license deleting public API** - agg-sharp is a *library* consumed by many applications, not all of them in this repo or in MatterCAD. There is no way to enumerate every project that imports it, so "no references found here" is not evidence a widget or public type is dead. Never delete a public widget/class just because a search in this repo (or one sibling repo) turns up zero call sites. If a public type has latent defects, fix the defects. Removal requires an explicit decision from Lars, not an audit result. (ConsoleWidget was deleted this way and had to be restored — SiteValidator was using it.)

**Push back** - When a request looks like a poor decision, contradicts your recommendation, or would undo or break something built earlier, say so before doing it: what it breaks or why it is worse, and what you recommend instead. A past decision or your recommendation stands until Lars explicitly overrides it. Never silently comply, and never silently substitute your own choice.

**Quality through iterations** - Start fast and simple, then improve to meet actual needs. Code that doesn't matter can be quick and dirty. But code that matters *really* matters—treat it with respect and improve it meticulously.

## Test-First Bug Fixing (Critical Practice)

**This is the single most important practice for agent performance and reliability.**

When a bug is reported, always follow this workflow:

1. **Write a reproducing test first** - Create a test that fails, demonstrating the bug
2. **Fix the bug** - Make the minimal change needed to address the issue
3. **Verify via passing test** - The previously failing test should now pass

**Do not skip the reproducing test.** Even if the fix seems obvious, the test validates your understanding and prevents regressions.

## Testing

- Tests MUST test actual production code, not copies - Never duplicate production logic in tests. Import and call the real code. Tests that verify copied code prove nothing about the actual system.
- Tests should run as fast as possible—fast tests get run more often
- Write tests for regressions and complex logic
- Avoid redundant tests that verify the same behavior
- **Full suite only before a push to any remote.** A step or merge runs only its new tests and the test classes covering the code it touched; the whole suite runs once, on main, before pushing
- When test failures occur, use the fix-test-failures agent (`.claude/agents/fix-test-failures.md`) — it treats all failures as real bugs and resolves them through instrumentation and root cause analysis, never by weakening tests
- **File size limit: 800 non-empty lines per `.cs` file.** `FileComplianceTests` (`Tests/Agg.Tests/Other/FileComplianceTests.cs`) enforces it. Files already over the limit are frozen at their current size in its `ExplicitFileLimits`; those limits only ever go down, and an entry is removed once its file is back under 800. When a file hits its limit, decompose it — use the `file-size-refactoring` skill (never partial classes, never compressing code to squeeze under).

## Project Context

- **Language:** C# (.NET 10.0)
- **Test Framework:** TUnit (v1.56.35)
- **Build:** `dotnet build`
- **Test:** `dotnet test` or run the test executable directly
- **Solution:** `agg-sharp.sln`
- **Test Project:** `Tests/Agg.Tests/Agg.Tests.csproj`
- **What is agg-sharp:** Core graphics/UI framework library used as a submodule by MatterCAD. Includes 2D graphics (agg), GUI widgets (Gui), polygon mesh, vector math, image processing, CSG, ray tracing, and GUI automation.

### Project Map

- `agg/` — Core 2D graphics engine (Graphics2D, image buffers, font rendering, scanline rasterizer)
- `Gui/` — Widget toolkit (buttons, text, layout, theming, windowing)
- `GuiAutomation/` — Automated UI testing framework
- `PolygonMesh/` — 3D mesh data structures, operations, BVH acceleration
- `VectorMath/` — Math primitives (Vector2/3, Matrix4x4, Quaternion, AABB)
- `Csg/` — Constructive solid geometry (boolean operations on meshes)
- `DataConverters2D/` — 2D path/shape conversion utilities
- `DataConverters3D/` — 3D file format loaders (STL, AMF, OBJ, 3MF)
- `RenderCore/` — The backend-agnostic render seam: `IRenderDevice`, `IRenderEncoder`, resource descriptors, texture formats. What a backend implements.
- `RenderGl/` — Graphics abstraction layer for GPU rendering: `IGpuContext`, the `GL` facade class, `Graphics2DGpu` (GPU 2D drawing), `INativeSceneRenderer`, and `Compat/` (the transitional GL-shaped layer over `RenderCore`). The "GL" in these names is a historical API shape, not OpenGL bindings.
- `WebGpu/` — The generated `webgpu.h` binding plus its generator and the wgpu-native bootstrap (`native/WgpuNative.targets`).
- `WebGpuRender/` — **The one render backend**: wgpu-native (D3D12 on Windows, Metal/Vulkan elsewhere) behind `RenderCore`, with WGSL shaders in `Shaders/`.
- `RenderOpenGl/`, `Glfw/`, `VorticeD3D/` — Removed. OpenGL/OpenTK/GLFW and the D3D11/Vortice.Windows backend are gone; WebGPU is the only render path to screen. Any leftover folders on disk are stale build output and belong to no solution.
- `ImageProcessing/` — Image filters, transforms, analysis
- `PlatformWin32/` — Windows platform abstraction (input, clipboard, system windows). `win32/WebGpuSystemWindow.cs` + `win32/WebGpuControl.cs` are the WinForms window host.
- `Tests/Agg.Tests/` — All tests

## Code Quality

**Names** - Choose carefully. Good names make code self-documenting.

**Comments** - Explain *why*, not *what*. The code shows what it does; comments should reveal intent, tradeoffs, and non-obvious reasoning. When investigating code, persist what you learn — add `/// <summary>` to undocumented public methods you had to study, and inline comments where non-obvious logic required investigation. If behavior surprised you, it will surprise the next reader.

**Refactoring** - Improve code when it serves a purpose, not for aesthetics. Refactor to fix bugs, add features, or improve clarity when you're already working in that area. This includes adding documentation — treat it as part of the improvement, not a separate task.

**Copyright** - When updating files with copyright notices, update the year to 2026 if not already current. Include Lars Brubaker in the copyright notice.

**Async/Await** - Never use `.GetAwaiter().GetResult()` or `.Result`. Always propagate async properly with `await`.

## Plans and Progress Docs

Plan and progress documents (in `docs/` or the repo root) describe open work only; history lives in git.

- Delete the doc in the change that completes its work.
- Prune as you go: remove finished steps, stale findings and superseded decisions; never append status updates, changelogs or "how we got here" narrative.
- A decision that must outlive the doc goes in a code comment where it applies (or this file).
- A doc's own status line can be stale — check the code before trusting it.

## Orchestration pattern

The main session acts as planner and orchestrator only — it should not write or edit code directly. All implementation is delegated to the `implementer` subagent (`.claude/agents/implementer.md`), one scoped step at a time. All post-change review is delegated to the `reviewer` subagent (`.claude/agents/reviewer.md`). The main session handles only planning, architecture decisions, and synthesizing subagent results.

Brief each implementer with one deliverable and a 25-minute budget; a run over 30 minutes is an error. Implementers that may run concurrently get `isolation: "worktree"`; a new worktree needs `git reset --hard main` and `git submodule update --init --recursive` before its first build.
