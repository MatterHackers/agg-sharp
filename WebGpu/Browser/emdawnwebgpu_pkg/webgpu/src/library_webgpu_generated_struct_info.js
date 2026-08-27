{{{

const structInfo32 = (
{
    "defines": {},
    "structs": {
        "WGPUAdapterInfo": {
            "__size__": 60,
            "adapterType": 40,
            "architecture": 12,
            "backendType": 36,
            "description": 28,
            "device": 20,
            "deviceID": 48,
            "nextInChain": 0,
            "subgroupMaxSize": 56,
            "subgroupMinSize": 52,
            "vendor": 4,
            "vendorID": 44
        },
        "WGPUAdapterPropertiesSubgroupMatrixConfigs": {
            "__size__": 16,
            "chain": 0,
            "configCount": 8,
            "configs": 12
        },
        "WGPUBindGroupDescriptor": {
            "__size__": 24,
            "entries": 20,
            "entryCount": 16,
            "label": 4,
            "layout": 12,
            "nextInChain": 0
        },
        "WGPUBindGroupEntry": {
            "__size__": 40,
            "binding": 4,
            "buffer": 8,
            "nextInChain": 0,
            "offset": 16,
            "sampler": 32,
            "size": 24,
            "textureView": 36
        },
        "WGPUBindGroupLayoutDescriptor": {
            "__size__": 20,
            "entries": 16,
            "entryCount": 12,
            "label": 4,
            "nextInChain": 0
        },
        "WGPUBindGroupLayoutEntry": {
            "__size__": 88,
            "binding": 4,
            "bindingArraySize": 16,
            "buffer": 24,
            "nextInChain": 0,
            "sampler": 48,
            "storageTexture": 72,
            "texture": 56,
            "visibility": 8
        },
        "WGPUBlendComponent": {
            "__size__": 12,
            "dstFactor": 8,
            "operation": 0,
            "srcFactor": 4
        },
        "WGPUBlendState": {
            "__size__": 24,
            "alpha": 12,
            "color": 0
        },
        "WGPUBufferBindingLayout": {
            "__size__": 24,
            "hasDynamicOffset": 8,
            "minBindingSize": 16,
            "nextInChain": 0,
            "type": 4
        },
        "WGPUBufferDescriptor": {
            "__size__": 40,
            "label": 4,
            "mappedAtCreation": 32,
            "nextInChain": 0,
            "size": 24,
            "usage": 16
        },
        "WGPUChainedStruct": {
            "__size__": 8,
            "next": 0,
            "sType": 4
        },
        "WGPUColor": {
            "__size__": 32,
            "a": 24,
            "b": 16,
            "g": 8,
            "r": 0
        },
        "WGPUColorTargetState": {
            "__size__": 24,
            "blend": 8,
            "format": 4,
            "nextInChain": 0,
            "writeMask": 16
        },
        "WGPUCommandBufferDescriptor": {
            "__size__": 12,
            "label": 4,
            "nextInChain": 0
        },
        "WGPUCommandEncoderDescriptor": {
            "__size__": 12,
            "label": 4,
            "nextInChain": 0
        },
        "WGPUCompatibilityModeLimits": {
            "__size__": 24,
            "chain": 0,
            "maxStorageBuffersInFragmentStage": 16,
            "maxStorageBuffersInVertexStage": 8,
            "maxStorageTexturesInFragmentStage": 20,
            "maxStorageTexturesInVertexStage": 12
        },
        "WGPUCompilationInfo": {
            "__size__": 12,
            "messageCount": 4,
            "messages": 8,
            "nextInChain": 0
        },
        "WGPUCompilationMessage": {
            "__size__": 48,
            "length": 40,
            "lineNum": 16,
            "linePos": 24,
            "message": 4,
            "nextInChain": 0,
            "offset": 32,
            "type": 12
        },
        "WGPUComputePassDescriptor": {
            "__size__": 16,
            "label": 4,
            "nextInChain": 0,
            "timestampWrites": 12
        },
        "WGPUComputePipelineDescriptor": {
            "__size__": 40,
            "compute": 16,
            "label": 4,
            "layout": 12,
            "nextInChain": 0
        },
        "WGPUComputeState": {
            "__size__": 24,
            "constantCount": 16,
            "constants": 20,
            "entryPoint": 8,
            "module": 4,
            "nextInChain": 0
        },
        "WGPUConstantEntry": {
            "__size__": 24,
            "key": 4,
            "nextInChain": 0,
            "value": 16
        },
        "WGPUDawnCompilationMessageUtf16": {
            "__size__": 32,
            "chain": 0,
            "length": 24,
            "linePos": 8,
            "offset": 16
        },
        "WGPUDepthStencilState": {
            "__size__": 68,
            "depthBias": 56,
            "depthBiasClamp": 64,
            "depthBiasSlopeScale": 60,
            "depthCompare": 12,
            "depthWriteEnabled": 8,
            "format": 4,
            "nextInChain": 0,
            "stencilBack": 32,
            "stencilFront": 16,
            "stencilReadMask": 48,
            "stencilWriteMask": 52
        },
        "WGPUDeviceDescriptor": {
            "__size__": 72,
            "defaultQueue": 24,
            "deviceLostCallbackInfo": 36,
            "label": 4,
            "nextInChain": 0,
            "requiredFeatureCount": 12,
            "requiredFeatures": 16,
            "requiredLimits": 20,
            "uncapturedErrorCallbackInfo": 56
        },
        "WGPUEmscriptenSurfaceSourceCanvasHTMLSelector": {
            "__size__": 16,
            "chain": 0,
            "selector": 8
        },
        "WGPUExtent3D": {
            "__size__": 12,
            "depthOrArrayLayers": 8,
            "height": 4,
            "width": 0
        },
        "WGPUExternalTextureBindingEntry": {
            "__size__": 12,
            "chain": 0,
            "externalTexture": 8
        },
        "WGPUExternalTextureBindingLayout": {
            "__size__": 8,
            "chain": 0
        },
        "WGPUFragmentState": {
            "__size__": 32,
            "constantCount": 16,
            "constants": 20,
            "entryPoint": 8,
            "module": 4,
            "nextInChain": 0,
            "targetCount": 24,
            "targets": 28
        },
        "WGPUFuture": {
            "__size__": 8,
            "id": 0
        },
        "WGPUFutureWaitInfo": {
            "__size__": 16,
            "completed": 8,
            "future": 0
        },
        "WGPUINTERNAL_HAVE_EMDAWNWEBGPU_HEADER": {
            "__size__": 4,
            "unused": 0
        },
        "WGPUInstanceDescriptor": {
            "__size__": 16,
            "nextInChain": 0,
            "requiredFeatureCount": 4,
            "requiredFeatures": 8,
            "requiredLimits": 12
        },
        "WGPUInstanceLimits": {
            "__size__": 8,
            "nextInChain": 0,
            "timedWaitAnyMaxCount": 4
        },
        "WGPULimits": {
            "__size__": 152,
            "maxBindGroups": 20,
            "maxBindGroupsPlusVertexBuffers": 24,
            "maxBindingsPerBindGroup": 28,
            "maxBufferSize": 96,
            "maxColorAttachmentBytesPerSample": 120,
            "maxColorAttachments": 116,
            "maxComputeInvocationsPerWorkgroup": 128,
            "maxComputeWorkgroupSizeX": 132,
            "maxComputeWorkgroupSizeY": 136,
            "maxComputeWorkgroupSizeZ": 140,
            "maxComputeWorkgroupStorageSize": 124,
            "maxComputeWorkgroupsPerDimension": 144,
            "maxDynamicStorageBuffersPerPipelineLayout": 36,
            "maxDynamicUniformBuffersPerPipelineLayout": 32,
            "maxImmediateSize": 148,
            "maxInterStageShaderVariables": 112,
            "maxSampledTexturesPerShaderStage": 40,
            "maxSamplersPerShaderStage": 44,
            "maxStorageBufferBindingSize": 72,
            "maxStorageBuffersPerShaderStage": 48,
            "maxStorageTexturesPerShaderStage": 52,
            "maxTextureArrayLayers": 16,
            "maxTextureDimension1D": 4,
            "maxTextureDimension2D": 8,
            "maxTextureDimension3D": 12,
            "maxUniformBufferBindingSize": 64,
            "maxUniformBuffersPerShaderStage": 56,
            "maxVertexAttributes": 104,
            "maxVertexBufferArrayStride": 108,
            "maxVertexBuffers": 88,
            "minStorageBufferOffsetAlignment": 84,
            "minUniformBufferOffsetAlignment": 80,
            "nextInChain": 0
        },
        "WGPUMultisampleState": {
            "__size__": 16,
            "alphaToCoverageEnabled": 12,
            "count": 4,
            "mask": 8,
            "nextInChain": 0
        },
        "WGPUOrigin3D": {
            "__size__": 12,
            "x": 0,
            "y": 4,
            "z": 8
        },
        "WGPUPassTimestampWrites": {
            "__size__": 16,
            "beginningOfPassWriteIndex": 8,
            "endOfPassWriteIndex": 12,
            "nextInChain": 0,
            "querySet": 4
        },
        "WGPUPipelineLayoutDescriptor": {
            "__size__": 24,
            "bindGroupLayoutCount": 12,
            "bindGroupLayouts": 16,
            "immediateSize": 20,
            "label": 4,
            "nextInChain": 0
        },
        "WGPUPrimitiveState": {
            "__size__": 24,
            "cullMode": 16,
            "frontFace": 12,
            "nextInChain": 0,
            "stripIndexFormat": 8,
            "topology": 4,
            "unclippedDepth": 20
        },
        "WGPUQuerySetDescriptor": {
            "__size__": 20,
            "count": 16,
            "label": 4,
            "nextInChain": 0,
            "type": 12
        },
        "WGPUQueueDescriptor": {
            "__size__": 12,
            "label": 4,
            "nextInChain": 0
        },
        "WGPURenderBundleDescriptor": {
            "__size__": 12,
            "label": 4,
            "nextInChain": 0
        },
        "WGPURenderBundleEncoderDescriptor": {
            "__size__": 36,
            "colorFormatCount": 12,
            "colorFormats": 16,
            "depthReadOnly": 28,
            "depthStencilFormat": 20,
            "label": 4,
            "nextInChain": 0,
            "sampleCount": 24,
            "stencilReadOnly": 32
        },
        "WGPURenderPassColorAttachment": {
            "__size__": 56,
            "clearValue": 24,
            "depthSlice": 8,
            "loadOp": 16,
            "nextInChain": 0,
            "resolveTarget": 12,
            "storeOp": 20,
            "view": 4
        },
        "WGPURenderPassDepthStencilAttachment": {
            "__size__": 40,
            "depthClearValue": 16,
            "depthLoadOp": 8,
            "depthReadOnly": 20,
            "depthStoreOp": 12,
            "nextInChain": 0,
            "stencilClearValue": 32,
            "stencilLoadOp": 24,
            "stencilReadOnly": 36,
            "stencilStoreOp": 28,
            "view": 4
        },
        "WGPURenderPassDescriptor": {
            "__size__": 32,
            "colorAttachmentCount": 12,
            "colorAttachments": 16,
            "depthStencilAttachment": 20,
            "label": 4,
            "nextInChain": 0,
            "occlusionQuerySet": 24,
            "timestampWrites": 28
        },
        "WGPURenderPassMaxDrawCount": {
            "__size__": 16,
            "chain": 0,
            "maxDrawCount": 8
        },
        "WGPURenderPipelineDescriptor": {
            "__size__": 96,
            "depthStencil": 72,
            "fragment": 92,
            "label": 4,
            "layout": 12,
            "multisample": 76,
            "nextInChain": 0,
            "primitive": 48,
            "vertex": 16
        },
        "WGPURequestAdapterOptions": {
            "__size__": 24,
            "backendType": 16,
            "compatibleSurface": 20,
            "featureLevel": 4,
            "forceFallbackAdapter": 12,
            "nextInChain": 0,
            "powerPreference": 8
        },
        "WGPURequestAdapterWebXROptions": {
            "__size__": 12,
            "chain": 0,
            "xrCompatible": 8
        },
        "WGPUSamplerBindingLayout": {
            "__size__": 8,
            "nextInChain": 0,
            "type": 4
        },
        "WGPUSamplerDescriptor": {
            "__size__": 52,
            "addressModeU": 12,
            "addressModeV": 16,
            "addressModeW": 20,
            "compare": 44,
            "label": 4,
            "lodMaxClamp": 40,
            "lodMinClamp": 36,
            "magFilter": 24,
            "maxAnisotropy": 48,
            "minFilter": 28,
            "mipmapFilter": 32,
            "nextInChain": 0
        },
        "WGPUShaderModuleDescriptor": {
            "__size__": 12,
            "label": 4,
            "nextInChain": 0
        },
        "WGPUShaderSourceSPIRV": {
            "__size__": 16,
            "chain": 0,
            "code": 12,
            "codeSize": 8
        },
        "WGPUShaderSourceWGSL": {
            "__size__": 16,
            "chain": 0,
            "code": 8
        },
        "WGPUStencilFaceState": {
            "__size__": 16,
            "compare": 0,
            "depthFailOp": 8,
            "failOp": 4,
            "passOp": 12
        },
        "WGPUStorageTextureBindingLayout": {
            "__size__": 16,
            "access": 4,
            "format": 8,
            "nextInChain": 0,
            "viewDimension": 12
        },
        "WGPUStringView": {
            "__size__": 8,
            "data": 0,
            "length": 4
        },
        "WGPUSubgroupMatrixConfig": {
            "K": 16,
            "M": 8,
            "N": 12,
            "__size__": 20,
            "componentType": 0,
            "resultComponentType": 4
        },
        "WGPUSupportedFeatures": {
            "__size__": 8,
            "featureCount": 0,
            "features": 4
        },
        "WGPUSupportedInstanceFeatures": {
            "__size__": 8,
            "featureCount": 0,
            "features": 4
        },
        "WGPUSupportedWGSLLanguageFeatures": {
            "__size__": 8,
            "featureCount": 0,
            "features": 4
        },
        "WGPUSurfaceCapabilities": {
            "__size__": 40,
            "alphaModeCount": 32,
            "alphaModes": 36,
            "formatCount": 16,
            "formats": 20,
            "nextInChain": 0,
            "presentModeCount": 24,
            "presentModes": 28,
            "usages": 8
        },
        "WGPUSurfaceColorManagement": {
            "__size__": 16,
            "chain": 0,
            "colorSpace": 8,
            "toneMappingMode": 12
        },
        "WGPUSurfaceConfiguration": {
            "__size__": 48,
            "alphaMode": 40,
            "device": 4,
            "format": 8,
            "height": 28,
            "nextInChain": 0,
            "presentMode": 44,
            "usage": 16,
            "viewFormatCount": 32,
            "viewFormats": 36,
            "width": 24
        },
        "WGPUSurfaceDescriptor": {
            "__size__": 12,
            "label": 4,
            "nextInChain": 0
        },
        "WGPUSurfaceTexture": {
            "__size__": 12,
            "nextInChain": 0,
            "status": 8,
            "texture": 4
        },
        "WGPUTexelCopyBufferInfo": {
            "__size__": 24,
            "buffer": 16,
            "layout": 0
        },
        "WGPUTexelCopyBufferLayout": {
            "__size__": 16,
            "bytesPerRow": 8,
            "offset": 0,
            "rowsPerImage": 12
        },
        "WGPUTexelCopyTextureInfo": {
            "__size__": 24,
            "aspect": 20,
            "mipLevel": 4,
            "origin": 8,
            "texture": 0
        },
        "WGPUTextureBindingLayout": {
            "__size__": 16,
            "multisampled": 12,
            "nextInChain": 0,
            "sampleType": 4,
            "viewDimension": 8
        },
        "WGPUTextureBindingViewDimension": {
            "__size__": 12,
            "chain": 0,
            "textureBindingViewDimension": 8
        },
        "WGPUTextureComponentSwizzle": {
            "__size__": 16,
            "a": 12,
            "b": 8,
            "g": 4,
            "r": 0
        },
        "WGPUTextureComponentSwizzleDescriptor": {
            "__size__": 24,
            "chain": 0,
            "swizzle": 8
        },
        "WGPUTextureDescriptor": {
            "__size__": 64,
            "dimension": 24,
            "format": 40,
            "label": 4,
            "mipLevelCount": 44,
            "nextInChain": 0,
            "sampleCount": 48,
            "size": 28,
            "usage": 16,
            "viewFormatCount": 52,
            "viewFormats": 56
        },
        "WGPUTextureViewDescriptor": {
            "__size__": 48,
            "arrayLayerCount": 32,
            "aspect": 36,
            "baseArrayLayer": 28,
            "baseMipLevel": 20,
            "dimension": 16,
            "format": 12,
            "label": 4,
            "mipLevelCount": 24,
            "nextInChain": 0,
            "usage": 40
        },
        "WGPUVertexAttribute": {
            "__size__": 24,
            "format": 4,
            "nextInChain": 0,
            "offset": 8,
            "shaderLocation": 16
        },
        "WGPUVertexBufferLayout": {
            "__size__": 24,
            "arrayStride": 8,
            "attributeCount": 16,
            "attributes": 20,
            "nextInChain": 0,
            "stepMode": 4
        },
        "WGPUVertexState": {
            "__size__": 32,
            "bufferCount": 24,
            "buffers": 28,
            "constantCount": 16,
            "constants": 20,
            "entryPoint": 8,
            "module": 4,
            "nextInChain": 0
        }
    }
}
);

const structInfo64 = (
{
    "defines": {},
    "structs": {
        "WGPUAdapterInfo": {
            "__size__": 96,
            "adapterType": 76,
            "architecture": 24,
            "backendType": 72,
            "description": 56,
            "device": 40,
            "deviceID": 84,
            "nextInChain": 0,
            "subgroupMaxSize": 92,
            "subgroupMinSize": 88,
            "vendor": 8,
            "vendorID": 80
        },
        "WGPUAdapterPropertiesSubgroupMatrixConfigs": {
            "__size__": 32,
            "chain": 0,
            "configCount": 16,
            "configs": 24
        },
        "WGPUBindGroupDescriptor": {
            "__size__": 48,
            "entries": 40,
            "entryCount": 32,
            "label": 8,
            "layout": 24,
            "nextInChain": 0
        },
        "WGPUBindGroupEntry": {
            "__size__": 56,
            "binding": 8,
            "buffer": 16,
            "nextInChain": 0,
            "offset": 24,
            "sampler": 40,
            "size": 32,
            "textureView": 48
        },
        "WGPUBindGroupLayoutDescriptor": {
            "__size__": 40,
            "entries": 32,
            "entryCount": 24,
            "label": 8,
            "nextInChain": 0
        },
        "WGPUBindGroupLayoutEntry": {
            "__size__": 120,
            "binding": 8,
            "bindingArraySize": 24,
            "buffer": 32,
            "nextInChain": 0,
            "sampler": 56,
            "storageTexture": 96,
            "texture": 72,
            "visibility": 16
        },
        "WGPUBlendComponent": {
            "__size__": 12,
            "dstFactor": 8,
            "operation": 0,
            "srcFactor": 4
        },
        "WGPUBlendState": {
            "__size__": 24,
            "alpha": 12,
            "color": 0
        },
        "WGPUBufferBindingLayout": {
            "__size__": 24,
            "hasDynamicOffset": 12,
            "minBindingSize": 16,
            "nextInChain": 0,
            "type": 8
        },
        "WGPUBufferDescriptor": {
            "__size__": 48,
            "label": 8,
            "mappedAtCreation": 40,
            "nextInChain": 0,
            "size": 32,
            "usage": 24
        },
        "WGPUChainedStruct": {
            "__size__": 16,
            "next": 0,
            "sType": 8
        },
        "WGPUColor": {
            "__size__": 32,
            "a": 24,
            "b": 16,
            "g": 8,
            "r": 0
        },
        "WGPUColorTargetState": {
            "__size__": 32,
            "blend": 16,
            "format": 8,
            "nextInChain": 0,
            "writeMask": 24
        },
        "WGPUCommandBufferDescriptor": {
            "__size__": 24,
            "label": 8,
            "nextInChain": 0
        },
        "WGPUCommandEncoderDescriptor": {
            "__size__": 24,
            "label": 8,
            "nextInChain": 0
        },
        "WGPUCompatibilityModeLimits": {
            "__size__": 32,
            "chain": 0,
            "maxStorageBuffersInFragmentStage": 24,
            "maxStorageBuffersInVertexStage": 16,
            "maxStorageTexturesInFragmentStage": 28,
            "maxStorageTexturesInVertexStage": 20
        },
        "WGPUCompilationInfo": {
            "__size__": 24,
            "messageCount": 8,
            "messages": 16,
            "nextInChain": 0
        },
        "WGPUCompilationMessage": {
            "__size__": 64,
            "length": 56,
            "lineNum": 32,
            "linePos": 40,
            "message": 8,
            "nextInChain": 0,
            "offset": 48,
            "type": 24
        },
        "WGPUComputePassDescriptor": {
            "__size__": 32,
            "label": 8,
            "nextInChain": 0,
            "timestampWrites": 24
        },
        "WGPUComputePipelineDescriptor": {
            "__size__": 80,
            "compute": 32,
            "label": 8,
            "layout": 24,
            "nextInChain": 0
        },
        "WGPUComputeState": {
            "__size__": 48,
            "constantCount": 32,
            "constants": 40,
            "entryPoint": 16,
            "module": 8,
            "nextInChain": 0
        },
        "WGPUConstantEntry": {
            "__size__": 32,
            "key": 8,
            "nextInChain": 0,
            "value": 24
        },
        "WGPUDawnCompilationMessageUtf16": {
            "__size__": 40,
            "chain": 0,
            "length": 32,
            "linePos": 16,
            "offset": 24
        },
        "WGPUDepthStencilState": {
            "__size__": 72,
            "depthBias": 60,
            "depthBiasClamp": 68,
            "depthBiasSlopeScale": 64,
            "depthCompare": 16,
            "depthWriteEnabled": 12,
            "format": 8,
            "nextInChain": 0,
            "stencilBack": 36,
            "stencilFront": 20,
            "stencilReadMask": 52,
            "stencilWriteMask": 56
        },
        "WGPUDeviceDescriptor": {
            "__size__": 144,
            "defaultQueue": 48,
            "deviceLostCallbackInfo": 72,
            "label": 8,
            "nextInChain": 0,
            "requiredFeatureCount": 24,
            "requiredFeatures": 32,
            "requiredLimits": 40,
            "uncapturedErrorCallbackInfo": 112
        },
        "WGPUEmscriptenSurfaceSourceCanvasHTMLSelector": {
            "__size__": 32,
            "chain": 0,
            "selector": 16
        },
        "WGPUExtent3D": {
            "__size__": 12,
            "depthOrArrayLayers": 8,
            "height": 4,
            "width": 0
        },
        "WGPUExternalTextureBindingEntry": {
            "__size__": 24,
            "chain": 0,
            "externalTexture": 16
        },
        "WGPUExternalTextureBindingLayout": {
            "__size__": 16,
            "chain": 0
        },
        "WGPUFragmentState": {
            "__size__": 64,
            "constantCount": 32,
            "constants": 40,
            "entryPoint": 16,
            "module": 8,
            "nextInChain": 0,
            "targetCount": 48,
            "targets": 56
        },
        "WGPUFuture": {
            "__size__": 8,
            "id": 0
        },
        "WGPUFutureWaitInfo": {
            "__size__": 16,
            "completed": 8,
            "future": 0
        },
        "WGPUINTERNAL_HAVE_EMDAWNWEBGPU_HEADER": {
            "__size__": 4,
            "unused": 0
        },
        "WGPUInstanceDescriptor": {
            "__size__": 32,
            "nextInChain": 0,
            "requiredFeatureCount": 8,
            "requiredFeatures": 16,
            "requiredLimits": 24
        },
        "WGPUInstanceLimits": {
            "__size__": 16,
            "nextInChain": 0,
            "timedWaitAnyMaxCount": 8
        },
        "WGPULimits": {
            "__size__": 152,
            "maxBindGroups": 24,
            "maxBindGroupsPlusVertexBuffers": 28,
            "maxBindingsPerBindGroup": 32,
            "maxBufferSize": 96,
            "maxColorAttachmentBytesPerSample": 120,
            "maxColorAttachments": 116,
            "maxComputeInvocationsPerWorkgroup": 128,
            "maxComputeWorkgroupSizeX": 132,
            "maxComputeWorkgroupSizeY": 136,
            "maxComputeWorkgroupSizeZ": 140,
            "maxComputeWorkgroupStorageSize": 124,
            "maxComputeWorkgroupsPerDimension": 144,
            "maxDynamicStorageBuffersPerPipelineLayout": 40,
            "maxDynamicUniformBuffersPerPipelineLayout": 36,
            "maxImmediateSize": 148,
            "maxInterStageShaderVariables": 112,
            "maxSampledTexturesPerShaderStage": 44,
            "maxSamplersPerShaderStage": 48,
            "maxStorageBufferBindingSize": 72,
            "maxStorageBuffersPerShaderStage": 52,
            "maxStorageTexturesPerShaderStage": 56,
            "maxTextureArrayLayers": 20,
            "maxTextureDimension1D": 8,
            "maxTextureDimension2D": 12,
            "maxTextureDimension3D": 16,
            "maxUniformBufferBindingSize": 64,
            "maxUniformBuffersPerShaderStage": 60,
            "maxVertexAttributes": 104,
            "maxVertexBufferArrayStride": 108,
            "maxVertexBuffers": 88,
            "minStorageBufferOffsetAlignment": 84,
            "minUniformBufferOffsetAlignment": 80,
            "nextInChain": 0
        },
        "WGPUMultisampleState": {
            "__size__": 24,
            "alphaToCoverageEnabled": 16,
            "count": 8,
            "mask": 12,
            "nextInChain": 0
        },
        "WGPUOrigin3D": {
            "__size__": 12,
            "x": 0,
            "y": 4,
            "z": 8
        },
        "WGPUPassTimestampWrites": {
            "__size__": 24,
            "beginningOfPassWriteIndex": 16,
            "endOfPassWriteIndex": 20,
            "nextInChain": 0,
            "querySet": 8
        },
        "WGPUPipelineLayoutDescriptor": {
            "__size__": 48,
            "bindGroupLayoutCount": 24,
            "bindGroupLayouts": 32,
            "immediateSize": 40,
            "label": 8,
            "nextInChain": 0
        },
        "WGPUPrimitiveState": {
            "__size__": 32,
            "cullMode": 20,
            "frontFace": 16,
            "nextInChain": 0,
            "stripIndexFormat": 12,
            "topology": 8,
            "unclippedDepth": 24
        },
        "WGPUQuerySetDescriptor": {
            "__size__": 32,
            "count": 28,
            "label": 8,
            "nextInChain": 0,
            "type": 24
        },
        "WGPUQueueDescriptor": {
            "__size__": 24,
            "label": 8,
            "nextInChain": 0
        },
        "WGPURenderBundleDescriptor": {
            "__size__": 24,
            "label": 8,
            "nextInChain": 0
        },
        "WGPURenderBundleEncoderDescriptor": {
            "__size__": 56,
            "colorFormatCount": 24,
            "colorFormats": 32,
            "depthReadOnly": 48,
            "depthStencilFormat": 40,
            "label": 8,
            "nextInChain": 0,
            "sampleCount": 44,
            "stencilReadOnly": 52
        },
        "WGPURenderPassColorAttachment": {
            "__size__": 72,
            "clearValue": 40,
            "depthSlice": 16,
            "loadOp": 32,
            "nextInChain": 0,
            "resolveTarget": 24,
            "storeOp": 36,
            "view": 8
        },
        "WGPURenderPassDepthStencilAttachment": {
            "__size__": 48,
            "depthClearValue": 24,
            "depthLoadOp": 16,
            "depthReadOnly": 28,
            "depthStoreOp": 20,
            "nextInChain": 0,
            "stencilClearValue": 40,
            "stencilLoadOp": 32,
            "stencilReadOnly": 44,
            "stencilStoreOp": 36,
            "view": 8
        },
        "WGPURenderPassDescriptor": {
            "__size__": 64,
            "colorAttachmentCount": 24,
            "colorAttachments": 32,
            "depthStencilAttachment": 40,
            "label": 8,
            "nextInChain": 0,
            "occlusionQuerySet": 48,
            "timestampWrites": 56
        },
        "WGPURenderPassMaxDrawCount": {
            "__size__": 24,
            "chain": 0,
            "maxDrawCount": 16
        },
        "WGPURenderPipelineDescriptor": {
            "__size__": 168,
            "depthStencil": 128,
            "fragment": 160,
            "label": 8,
            "layout": 24,
            "multisample": 136,
            "nextInChain": 0,
            "primitive": 96,
            "vertex": 32
        },
        "WGPURequestAdapterOptions": {
            "__size__": 32,
            "backendType": 20,
            "compatibleSurface": 24,
            "featureLevel": 8,
            "forceFallbackAdapter": 16,
            "nextInChain": 0,
            "powerPreference": 12
        },
        "WGPURequestAdapterWebXROptions": {
            "__size__": 24,
            "chain": 0,
            "xrCompatible": 16
        },
        "WGPUSamplerBindingLayout": {
            "__size__": 16,
            "nextInChain": 0,
            "type": 8
        },
        "WGPUSamplerDescriptor": {
            "__size__": 64,
            "addressModeU": 24,
            "addressModeV": 28,
            "addressModeW": 32,
            "compare": 56,
            "label": 8,
            "lodMaxClamp": 52,
            "lodMinClamp": 48,
            "magFilter": 36,
            "maxAnisotropy": 60,
            "minFilter": 40,
            "mipmapFilter": 44,
            "nextInChain": 0
        },
        "WGPUShaderModuleDescriptor": {
            "__size__": 24,
            "label": 8,
            "nextInChain": 0
        },
        "WGPUShaderSourceSPIRV": {
            "__size__": 32,
            "chain": 0,
            "code": 24,
            "codeSize": 16
        },
        "WGPUShaderSourceWGSL": {
            "__size__": 32,
            "chain": 0,
            "code": 16
        },
        "WGPUStencilFaceState": {
            "__size__": 16,
            "compare": 0,
            "depthFailOp": 8,
            "failOp": 4,
            "passOp": 12
        },
        "WGPUStorageTextureBindingLayout": {
            "__size__": 24,
            "access": 8,
            "format": 12,
            "nextInChain": 0,
            "viewDimension": 16
        },
        "WGPUStringView": {
            "__size__": 16,
            "data": 0,
            "length": 8
        },
        "WGPUSubgroupMatrixConfig": {
            "K": 16,
            "M": 8,
            "N": 12,
            "__size__": 20,
            "componentType": 0,
            "resultComponentType": 4
        },
        "WGPUSupportedFeatures": {
            "__size__": 16,
            "featureCount": 0,
            "features": 8
        },
        "WGPUSupportedInstanceFeatures": {
            "__size__": 16,
            "featureCount": 0,
            "features": 8
        },
        "WGPUSupportedWGSLLanguageFeatures": {
            "__size__": 16,
            "featureCount": 0,
            "features": 8
        },
        "WGPUSurfaceCapabilities": {
            "__size__": 64,
            "alphaModeCount": 48,
            "alphaModes": 56,
            "formatCount": 16,
            "formats": 24,
            "nextInChain": 0,
            "presentModeCount": 32,
            "presentModes": 40,
            "usages": 8
        },
        "WGPUSurfaceColorManagement": {
            "__size__": 24,
            "chain": 0,
            "colorSpace": 16,
            "toneMappingMode": 20
        },
        "WGPUSurfaceConfiguration": {
            "__size__": 64,
            "alphaMode": 56,
            "device": 8,
            "format": 16,
            "height": 36,
            "nextInChain": 0,
            "presentMode": 60,
            "usage": 24,
            "viewFormatCount": 40,
            "viewFormats": 48,
            "width": 32
        },
        "WGPUSurfaceDescriptor": {
            "__size__": 24,
            "label": 8,
            "nextInChain": 0
        },
        "WGPUSurfaceTexture": {
            "__size__": 24,
            "nextInChain": 0,
            "status": 16,
            "texture": 8
        },
        "WGPUTexelCopyBufferInfo": {
            "__size__": 24,
            "buffer": 16,
            "layout": 0
        },
        "WGPUTexelCopyBufferLayout": {
            "__size__": 16,
            "bytesPerRow": 8,
            "offset": 0,
            "rowsPerImage": 12
        },
        "WGPUTexelCopyTextureInfo": {
            "__size__": 32,
            "aspect": 24,
            "mipLevel": 8,
            "origin": 12,
            "texture": 0
        },
        "WGPUTextureBindingLayout": {
            "__size__": 24,
            "multisampled": 16,
            "nextInChain": 0,
            "sampleType": 8,
            "viewDimension": 12
        },
        "WGPUTextureBindingViewDimension": {
            "__size__": 24,
            "chain": 0,
            "textureBindingViewDimension": 16
        },
        "WGPUTextureComponentSwizzle": {
            "__size__": 16,
            "a": 12,
            "b": 8,
            "g": 4,
            "r": 0
        },
        "WGPUTextureComponentSwizzleDescriptor": {
            "__size__": 32,
            "chain": 0,
            "swizzle": 16
        },
        "WGPUTextureDescriptor": {
            "__size__": 80,
            "dimension": 32,
            "format": 48,
            "label": 8,
            "mipLevelCount": 52,
            "nextInChain": 0,
            "sampleCount": 56,
            "size": 36,
            "usage": 24,
            "viewFormatCount": 64,
            "viewFormats": 72
        },
        "WGPUTextureViewDescriptor": {
            "__size__": 64,
            "arrayLayerCount": 44,
            "aspect": 48,
            "baseArrayLayer": 40,
            "baseMipLevel": 32,
            "dimension": 28,
            "format": 24,
            "label": 8,
            "mipLevelCount": 36,
            "nextInChain": 0,
            "usage": 56
        },
        "WGPUVertexAttribute": {
            "__size__": 32,
            "format": 8,
            "nextInChain": 0,
            "offset": 16,
            "shaderLocation": 24
        },
        "WGPUVertexBufferLayout": {
            "__size__": 40,
            "arrayStride": 16,
            "attributeCount": 24,
            "attributes": 32,
            "nextInChain": 0,
            "stepMode": 8
        },
        "WGPUVertexState": {
            "__size__": 64,
            "bufferCount": 48,
            "buffers": 56,
            "constantCount": 32,
            "constants": 40,
            "entryPoint": 16,
            "module": 8,
            "nextInChain": 0
        }
    }
}
);

// Double check that the struct info was generated from the right header.
// (The include directory option of gen_struct_info.py affects this.)
if (!('WGPUINTERNAL_HAVE_EMDAWNWEBGPU_HEADER' in structInfo32.structs)) {
    throw new Error('struct_info32 generation error - need webgpu.h from Dawn, got it from Emscripten');
}
if (!('WGPUINTERNAL_HAVE_EMDAWNWEBGPU_HEADER' in structInfo64.structs)) {
    throw new Error('struct_info64 generation error - need webgpu.h from Dawn, got it from Emscripten');
}

// Make sure we aren't inheriting any of the webgpu.h struct info from
// Emscripten's copy.
for (const k of Object.keys(C_STRUCTS)) {
    if (k.startsWith('WGPU')) {
        delete C_STRUCTS[k];
    }
}

const WGPU_STRUCTS = (MEMORY64 ? structInfo64 : structInfo32).structs;
for (const [k, v] of Object.entries(WGPU_STRUCTS)) {
    C_STRUCTS[k] = v;
}
globalThis.__HAVE_EMDAWNWEBGPU_STRUCT_INFO = true;

null;
}}}
