# MatterHackers.WebGpu

The raw C# binding for [wgpu-native](https://github.com/gfx-rs/wgpu-native)'s `webgpu.h`, generated
from the pinned headers in `headers/`. This is the bottom of the WebGPU render stack: 1:1 with the C
API, unsafe, and free of any ergonomics. Anything friendlier belongs in a separate layer so that
regenerating this one stays mechanical.

| | |
|---|---|
| Pinned wgpu-native | **v29.0.1.1** (see `headers/README.md`) |
| Native payload | downloaded and verified at build time by `native/WgpuNative.targets` |
| Target framework | `net10.0`, `AllowUnsafeBlocks` |

## Native bootstrap

`git clone` + `dotnet build WebGpu/MatterHackers.WebGpu.csproj` is the whole setup: no NuGet feed, no
credentials, no script, no PowerShell (the mac and Linux have none). Note that it is *this project*
that builds on any OS - `agg-sharp.sln` still contains Windows-only projects (`PlatformWin32` and its
WinForms window host, the WinForms examples), so a whole-solution build is Windows-only until later phases
of the port. `native/WgpuNative.targets`, imported by this project,
uses MSBuild's own cross-platform `DownloadFile` / `VerifyFileHash` / `Unzip` tasks to, on the first
build only:

1. download the pinned release zip **for the building machine's RID alone** from
   `https://github.com/gfx-rs/wgpu-native/releases/download/v<version>/` into `native/downloads/`,
2. verify it against `native/checksums-v<version>.txt` (SHA-256, sha256sum format),
3. unzip and stage just the shared library into `native/staging/v<version>/<rid>/`.

The staged library is then added as a `None` item with `CopyToOutputDirectory=PreserveNewest`, so it
lands beside this assembly *and* flows transitively to anything that references this project
(`Agg.Tests`, `examples/WebGpuTriangle`). Every later build sees the staged file and skips the whole
bootstrap, so incremental builds do zero network I/O. `downloads/` and `staging/` are gitignored;
the manifest, the targets file and this README are what is checked in.

Six RIDs are mapped - `win-x64`, `win-arm64`, `osx-arm64`, `osx-x64`, `linux-x64`, `linux-arm64` -
and an unmapped platform fails with a message naming it rather than a `DllNotFoundException` later.

Staging is atomic: the library is copied to a per-build `.tmp` name inside the destination folder and
renamed into place, so the `Exists()` skip-guard of a build running in parallel never sees a
half-written file. The bootstrap is also skipped entirely during design-time builds, so opening the
solution in Visual Studio or Rider - offline, or with many projects loading at once - never downloads
anything.

**Bumping the version:** set `WgpuNativeVersion` in `native/WgpuNative.targets`, add a
`native/checksums-v<new>.txt` (`sha256sum *.zip`, or `Get-FileHash -Algorithm SHA256`), refresh
`headers/` from the new release, and regenerate the binding - webgpu.h moves with the release.

**Priming the cache offline:** drop the release zips into `native/downloads/v<version>/` by hand.
Hand-placed and downloaded files are verified identically, and a zip that fails verification is
deleted so the next build re-downloads it.

## Layout

- `Generated/` - the checked in binding. Do not edit; regenerate.
  - `CoreTypes.cs` - `WGPUBool`, `WGPUStringView`, `WGPUChainedStruct`, `WGPUConstants`
  - `Enums.*.cs` - enums (int backed) and bitflag sets (`[Flags]`, ulong, matching `WGPUFlags`)
  - `Handles.cs` - opaque objects as `readonly struct` wrappers around a pointer
  - `Structs.*.cs` - blittable `LayoutKind.Sequential` structs in C member order
  - `Functions.cs` / `Functions.Native.cs` - `Wgpu` and `WgpuNative`, plain `DllImport("wgpu_native")`
  - `StructLayouts.cs` - the C size and alignment of every struct, asserted by the tests
- `generator/` - the hand run generator (not referenced by anything, not part of any build graph)
- `headers/` - the pinned `webgpu.h`, `wgpu.h` and `webgpu.yml`
- `native/` - the build-time native bootstrap: `WgpuNative.targets` and the `checksums-v*.txt`
  SHA-256 manifest every release zip is verified against (plus the gitignored `downloads/` cache and
  `staging/` output)

Callbacks have no generated type of their own: a C callback typedef becomes
`delegate* unmanaged[Cdecl]<...>` wherever it appears - in a `*CallbackInfo` struct member or as an
entry point parameter - and callers fill it from an `[UnmanagedCallersOnly]` method. There is
deliberately no `[UnmanagedFunctionPointer]` delegate, because handing wgpu a pointer to a managed
delegate the GC may collect is a bug with no compile time warning.

## Regenerating

Run after bumping the pinned wgpu-native version (headers and native payload move together):

```pwsh
dotnet run --project generator
```

The generator reads `headers/webgpu.yml` - the machine readable spec `webgpu.h` is itself generated
from - and parses `headers/wgpu.h` for wgpu-native's own extensions, which have no yml. It then cross
checks the finished model against both headers: every struct's member names, order **and C types**,
every portable enum value, and every entry point's full signature (return type and parameter types).
A mismatch is reported and exits non zero, because the failure mode of a wrong binding is silent
memory corruption rather than a compile error.

The portable half of that check is a real second opinion (model from the yml, expectations from the
header). The `wgpu.h` half is weaker - the model was read from that same file - but the cross check
parses it separately, so it still catches a declaration or member the reader quietly dropped. Native
enum values are not re-checked: `wgpu.h` writes them as expressions, so restating them would only be
a second copy of the reader's own evaluator.

Known spec gap: `wgpuGetProcAddress` is declared in `webgpu.h` but absent from `webgpu.yml`, so the
generator adds it by hand (returning `nint` - a C# delegate type could not be cast to the real
signature by the caller anyway).

## Portability

`Wgpu` holds the portable `webgpu.h` surface, which also exists in Emscripten's emdawnwebgpu; calls
in `WgpuNative` are wgpu-native only and are a deliberate step away from a future browser build.
Plain `DllImport` (rather than a loader framework) is what keeps that door open at no present cost.

## Tests

`Tests/Agg.Tests/Agg.WebGpu/WebGpuBindingTests.cs` measures every generated struct against the C
layout the generator computed, and smoke tests the P/Invoke path by creating and releasing a real
`WGPUInstance`.

`Tests/Agg.Tests/Agg.WebGpu/WebGpuTriangleRenderTests.cs` is the offscreen proof of life: it renders a
WGSL triangle into an RGBA8 texture on a real adapter, reads it back through `copyTextureToBuffer`,
asserts the clear and triangle colours, and drops the frame at
`%TEMP%\MatterCADTests\WebGpuOffscreenTriangle.png` for eyeballing. Its window-side counterpart is
`examples/WebGpuTriangle` - a WinForms `Control` whose HWND becomes a `WGPUSurface`; run it with
`--smoke` for a 60 frame non interactive pass that also resizes (exit code 0 means the swapchain,
present, and reconfigure paths all held).

Both require a GPU: they ask for the D3D12 backend and assert they got it.
