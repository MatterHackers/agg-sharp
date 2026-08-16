# wgpu-native C headers

These are the C API headers the MatterCAD WebGPU binding is generated from.
They are **copies, not sources** - refresh them from the release zip (they live
in `include/webgpu/` and `wgpu-native-meta/`) rather than editing them.

| | |
|---|---|
| Pinned wgpu-native version | **v29.0.1.1** (released 2026-06-23) |
| Source | <https://github.com/gfx-rs/wgpu-native/releases/tag/v29.0.1.1> |
| Extracted from | `wgpu-linux-x86_64-release.zip` (`include/webgpu/`, `wgpu-native-meta/`) |
| Matching native payload | pinned by `../native/WgpuNative.targets` (same version) |

## Files

- `webgpu.h` - the standard [webgpu-headers](https://github.com/webgpu-native/webgpu-headers)
  C API. This is the portable surface; anything declared here also exists in
  Emscripten's `emdawnwebgpu`, which is what keeps the browser door open.
- `wgpu.h` - wgpu-native's own extensions to webgpu.h (native-only surface
  configuration, logging, instance enumeration, backend selection). Using
  something from here is a deliberate step away from browser portability.
- `webgpu.yml` - the machine-readable specification webgpu.h itself is generated
  from, shipped in the release under `wgpu-native-meta/`. It is a far better
  input for a binding generator than parsing the header: enum values, struct
  members, function signatures, defaults and nullability are all explicit.

## Version pinning

The header set changes between wgpu-native releases (WebGPU's C API is still
moving), so the binding, the headers and the `WgpuNativeVersion` in
`../native/WgpuNative.targets` move together as one unit. Bumping the release
means refreshing these headers *and* regenerating the binding - a header refresh
on its own will produce a binding that mismatches the downloaded native.

The headers are byte-identical across the platform zips apart from line endings
(the Windows zips use CRLF), so a single checked-in copy serves every platform.
