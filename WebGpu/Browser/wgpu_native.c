// Copyright (c) 2026 Lars Brubaker, MatterHackers Inc.
//
// Claims the "wgpu_native" DllImport module name for the Emscripten link, and stubs the
// wgpu-native-only entry points that emdawnwebgpu does not implement.
//
// Graduated from spikes/WebGpuWasmSpike/native/wgpu_native.c; linked by
// build/WebGpuBrowser.targets under -p:LinkEmdawnWebGpu=true.
//
// Two things are going on here:
//
// 1. mono-wasm derives the set of resolvable P/Invoke modules from the *file names* of the native
//    inputs it links (see _WasmPInvokeModules in WasmApp.Common.targets). emdawnwebgpu links in as
//    a cached Emscripten port library (libemdawnwebgpu-*.a), so nothing named wgpu_native would
//    otherwise be on the link line and all 203 DllImport("wgpu_native") entry points would be
//    dropped from the generated pinvoke table.
//
// 2. MatterHackers.WebGpu binds wgpu-native's API, which is webgpu.h plus wgpu.h extensions
//    (wgpuDevicePoll, multi-draw-indirect, Metal interop, pipeline statistics queries, ...).
//    emdawnwebgpu implements the browser's WebGPU only, so those symbols have no definition. They
//    are unreachable in the browser by construction, so they are stubbed to a loud no-op rather
//    than being papered over with -sERROR_ON_UNDEFINED_SYMBOLS=0, which would also hide real
//    mistakes.
//
// The signatures below are copied verbatim from the generated obj/.../pinvoke-table.h so that the
// definitions and the table declarations agree; a mismatch shows up as a wasm-ld signature error.
//
// What is deliberately NOT here: wgpuRenderPassEncoderMultiDrawIndirect and
// wgpuRenderPassEncoderMultiDrawIndexedIndirect - the two *without* the Count suffix. emdawnwebgpu
// DOES define both (library_webgpu.js), so a stub here is a duplicate definition, and worse, the two
// sides disagree about the signature: Dawn's takes the draw-count buffer and offset that
// wgpu-native's ...IndirectCount entry point carries, so the managed binding's declaration and the
// JS implementation can silently differ in arity. Leave them to emdawnwebgpu, and never add a stub
// for a symbol the port already provides. (docs/wasm_blazor.html D-W1: "two MultiDraw*Indirect
// signature collisions to guard".)

#include <stdint.h>
#include <emscripten/console.h>

static void wgpu_native_unavailable(const char *name)
{
	emscripten_errf("wgpu_native: %s is a wgpu-native extension with no WebGPU equivalent; "
	                "it is not available in the browser build", name);
}

void wgpuCommandEncoderClearTexture(void *a, void *b, void *c)
{
	(void)a; (void)b; (void)c;
	wgpu_native_unavailable("wgpuCommandEncoderClearTexture");
}

void wgpuComputePassEncoderBeginPipelineStatisticsQuery(void *a, void *b, uint32_t c)
{
	(void)a; (void)b; (void)c;
	wgpu_native_unavailable("wgpuComputePassEncoderBeginPipelineStatisticsQuery");
}

void wgpuComputePassEncoderEndPipelineStatisticsQuery(void *a)
{
	(void)a;
	wgpu_native_unavailable("wgpuComputePassEncoderEndPipelineStatisticsQuery");
}

void *wgpuDeviceCreateShaderModuleSpirV(void *a, void *b)
{
	(void)a; (void)b;
	wgpu_native_unavailable("wgpuDeviceCreateShaderModuleSpirV");
	return 0;
}

void *wgpuDeviceCreateShaderModuleTrusted(void *a, void *b, uint64_t c)
{
	(void)a; (void)b; (void)c;
	wgpu_native_unavailable("wgpuDeviceCreateShaderModuleTrusted");
	return 0;
}

void *wgpuDeviceGetNativeMetalDevice(void *a)
{
	(void)a;
	wgpu_native_unavailable("wgpuDeviceGetNativeMetalDevice");
	return 0;
}

// The browser drives WebGPU's queue itself, so polling is simply a no-op that reports "idle".
uint32_t wgpuDevicePoll(void *a, uint32_t b, void *c)
{
	(void)a; (void)b; (void)c;
	return 0;
}

uint32_t wgpuDeviceStartGraphicsDebuggerCapture(void *a)
{
	(void)a;
	wgpu_native_unavailable("wgpuDeviceStartGraphicsDebuggerCapture");
	return 0;
}

void wgpuDeviceStopGraphicsDebuggerCapture(void *a)
{
	(void)a;
	wgpu_native_unavailable("wgpuDeviceStopGraphicsDebuggerCapture");
}

void wgpuGenerateReport(void *a, void *b)
{
	(void)a; (void)b;
	wgpu_native_unavailable("wgpuGenerateReport");
}

uint32_t wgpuGetVersion(void)
{
	// wgpu-native's packed version. Nothing reads it beyond diagnostics, so report 0.
	return 0;
}

void *wgpuInstanceEnumerateAdapters(void *a, void *b, void *c)
{
	(void)a; (void)b; (void)c;
	wgpu_native_unavailable("wgpuInstanceEnumerateAdapters");
	return 0;
}

void *wgpuQueueGetNativeMetalCommandQueue(void *a)
{
	(void)a;
	wgpu_native_unavailable("wgpuQueueGetNativeMetalCommandQueue");
	return 0;
}

float wgpuQueueGetTimestampPeriod(void *a)
{
	(void)a;
	wgpu_native_unavailable("wgpuQueueGetTimestampPeriod");
	return 0.0f;
}

uint64_t wgpuQueueSubmitForIndex(void *a, void *b, void *c)
{
	(void)a; (void)b; (void)c;
	wgpu_native_unavailable("wgpuQueueSubmitForIndex");
	return 0;
}

void wgpuRenderPassEncoderBeginPipelineStatisticsQuery(void *a, void *b, uint32_t c)
{
	(void)a; (void)b; (void)c;
	wgpu_native_unavailable("wgpuRenderPassEncoderBeginPipelineStatisticsQuery");
}

void wgpuRenderPassEncoderEndPipelineStatisticsQuery(void *a)
{
	(void)a;
	wgpu_native_unavailable("wgpuRenderPassEncoderEndPipelineStatisticsQuery");
}

void wgpuRenderPassEncoderMultiDrawIndexedIndirectCount(void *a, void *b, uint64_t c, void *d, uint64_t e, uint32_t f)
{
	(void)a; (void)b; (void)c; (void)d; (void)e; (void)f;
	wgpu_native_unavailable("wgpuRenderPassEncoderMultiDrawIndexedIndirectCount");
}

void wgpuRenderPassEncoderMultiDrawIndirectCount(void *a, void *b, uint64_t c, void *d, uint64_t e, uint32_t f)
{
	(void)a; (void)b; (void)c; (void)d; (void)e; (void)f;
	wgpu_native_unavailable("wgpuRenderPassEncoderMultiDrawIndirectCount");
}

// Logging is wgpu-native's own facility; emdawnwebgpu logs through the browser console. Silently
// accepted so that startup code that configures logging does not have to be conditionalized.
void wgpuSetLogCallback(void *a, void *b)
{
	(void)a; (void)b;
}

void wgpuSetLogLevel(int32_t a)
{
	(void)a;
}

void *wgpuTextureGetNativeMetalTexture(void *a)
{
	(void)a;
	wgpu_native_unavailable("wgpuTextureGetNativeMetalTexture");
	return 0;
}
