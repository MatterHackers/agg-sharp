// Copyright 2026 The Dawn & Tint Authors
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
//
// 1. Redistributions of source code must retain the above copyright notice, this
//    list of conditions and the following disclaimer.
//
// 2. Redistributions in binary form must reproduce the above copyright notice,
//    this list of conditions and the following disclaimer in the documentation
//    and/or other materials provided with the distribution.
//
// 3. Neither the name of the copyright holder nor the names of its
//    contributors may be used to endorse or promote products derived from
//    this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
// FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
// SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
// CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
// OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

module;
#include "webgpu/webgpu_cpp.h"
export module wgpu;

export namespace wgpu {
// constants
using wgpu::kArrayLayerCountUndefined;
using wgpu::kCopyStrideUndefined;
using wgpu::kDepthClearValueUndefined;
using wgpu::kDepthSliceUndefined;
using wgpu::kLimitU32Undefined;
using wgpu::kLimitU64Undefined;
using wgpu::kMipLevelCountUndefined;
using wgpu::kQuerySetIndexUndefined;
using wgpu::kStrlen;
using wgpu::kWholeMapSize;
using wgpu::kWholeSize;

// enums
using wgpu::AdapterType;
using wgpu::AddressMode;
using wgpu::BackendType;
using wgpu::BlendFactor;
using wgpu::BlendOperation;
using wgpu::BufferBindingType;
using wgpu::BufferMapState;
using wgpu::CallbackMode;
using wgpu::CompareFunction;
using wgpu::CompilationInfoRequestStatus;
using wgpu::CompilationMessageType;
using wgpu::ComponentSwizzle;
using wgpu::CompositeAlphaMode;
using wgpu::CreatePipelineAsyncStatus;
using wgpu::CullMode;
using wgpu::DeviceLostReason;
using wgpu::ErrorFilter;
using wgpu::ErrorType;
using wgpu::FeatureLevel;
using wgpu::FeatureName;
using wgpu::FilterMode;
using wgpu::FrontFace;
using wgpu::IndexFormat;
using wgpu::InstanceFeatureName;
using wgpu::LoadOp;
using wgpu::MapAsyncStatus;
using wgpu::MipmapFilterMode;
using wgpu::PopErrorScopeStatus;
using wgpu::PowerPreference;
using wgpu::PredefinedColorSpace;
using wgpu::PresentMode;
using wgpu::PrimitiveTopology;
using wgpu::QueryType;
using wgpu::QueueWorkDoneStatus;
using wgpu::RequestAdapterStatus;
using wgpu::RequestDeviceStatus;
using wgpu::SamplerBindingType;
using wgpu::Status;
using wgpu::StencilOperation;
using wgpu::StorageTextureAccess;
using wgpu::StoreOp;
using wgpu::SType;
using wgpu::SubgroupMatrixComponentType;
using wgpu::SurfaceGetCurrentTextureStatus;
using wgpu::TextureAspect;
using wgpu::TextureDimension;
using wgpu::TextureFormat;
using wgpu::TextureSampleType;
using wgpu::TextureViewDimension;
using wgpu::ToneMappingMode;
using wgpu::VertexFormat;
using wgpu::VertexStepMode;
using wgpu::WaitStatus;
using wgpu::WGSLLanguageFeatureName;

// bitmasks
using wgpu::BufferUsage;
using wgpu::ColorWriteMask;
using wgpu::MapMode;
using wgpu::ShaderStage;
using wgpu::TextureUsage;

WGPU_IMPORT_BITMASK_OPERATORS

using wgpu::IsWGPUBitmask;
using wgpu::LowerBitmask;
using wgpu::BoolConvertible;

// callbacks
using wgpu::BufferMapCallback;
using wgpu::CompilationInfoCallback;
using wgpu::CreateComputePipelineAsyncCallback;
using wgpu::CreateRenderPipelineAsyncCallback;
using wgpu::DeviceLostCallback;
using wgpu::PopErrorScopeCallback;
using wgpu::QueueWorkDoneCallback;
using wgpu::RequestAdapterCallback;
using wgpu::RequestDeviceCallback;
using wgpu::UncapturedErrorCallback;

// classes
using wgpu::Adapter;
using wgpu::BindGroup;
using wgpu::BindGroupLayout;
using wgpu::Buffer;
using wgpu::CommandBuffer;
using wgpu::CommandEncoder;
using wgpu::ComputePipeline;
using wgpu::Device;
using wgpu::ExternalTexture;
using wgpu::Instance;
using wgpu::PipelineLayout;
using wgpu::QuerySet;
using wgpu::Queue;
using wgpu::RenderBundle;
using wgpu::RenderPipeline;
using wgpu::Sampler;
using wgpu::ShaderModule;
using wgpu::Surface;
using wgpu::Texture;
using wgpu::TextureView;
using wgpu::ComputePassEncoder;
using wgpu::RenderBundleEncoder;
using wgpu::RenderPassEncoder;

// structs
using wgpu::StringView;
using wgpu::BlendComponent;
using wgpu::BufferBindingLayout;
using wgpu::BufferDescriptor;
using wgpu::Color;
using wgpu::CommandBufferDescriptor;
using wgpu::CommandEncoderDescriptor;
using wgpu::CompatibilityModeLimits;
using wgpu::ConstantEntry;
using wgpu::DawnCompilationMessageUtf16;
using wgpu::EmscriptenSurfaceSourceCanvasHTMLSelector;
using wgpu::Extent3D;
using wgpu::ExternalTextureBindingEntry;
using wgpu::ExternalTextureBindingLayout;
using wgpu::Future;
using wgpu::InstanceLimits;
using wgpu::INTERNAL_HAVE_EMDAWNWEBGPU_HEADER;
using wgpu::MultisampleState;
using wgpu::Origin3D;
using wgpu::PassTimestampWrites;
using wgpu::PipelineLayoutDescriptor;
using wgpu::PrimitiveState;
using wgpu::QuerySetDescriptor;
using wgpu::QueueDescriptor;
using wgpu::RenderBundleDescriptor;
using wgpu::RenderBundleEncoderDescriptor;
using wgpu::RenderPassDepthStencilAttachment;
using wgpu::RenderPassMaxDrawCount;
using wgpu::RequestAdapterWebXROptions;
using wgpu::SamplerBindingLayout;
using wgpu::SamplerDescriptor;
using wgpu::ShaderSourceSPIRV;
using wgpu::ShaderSourceWGSL;
using wgpu::StencilFaceState;
using wgpu::StorageTextureBindingLayout;
using wgpu::SubgroupMatrixConfig;
using wgpu::SupportedFeatures;
using wgpu::SupportedInstanceFeatures;
using wgpu::SupportedWGSLLanguageFeatures;
using wgpu::SurfaceCapabilities;
using wgpu::SurfaceColorManagement;
using wgpu::SurfaceConfiguration;
using wgpu::SurfaceTexture;
using wgpu::TexelCopyBufferLayout;
using wgpu::TextureBindingLayout;
using wgpu::TextureBindingViewDimension;
using wgpu::TextureComponentSwizzle;
using wgpu::VertexAttribute;
using wgpu::AdapterPropertiesSubgroupMatrixConfigs;
using wgpu::BindGroupEntry;
using wgpu::BindGroupLayoutEntry;
using wgpu::BlendState;
using wgpu::CompilationMessage;
using wgpu::ComputePassDescriptor;
using wgpu::ComputeState;
using wgpu::DepthStencilState;
using wgpu::FutureWaitInfo;
using wgpu::InstanceDescriptor;
using wgpu::Limits;
using wgpu::RenderPassColorAttachment;
using wgpu::RequestAdapterOptions;
using wgpu::ShaderModuleDescriptor;
using wgpu::SurfaceDescriptor;
using wgpu::TexelCopyBufferInfo;
using wgpu::TexelCopyTextureInfo;
using wgpu::TextureComponentSwizzleDescriptor;
using wgpu::TextureDescriptor;
using wgpu::VertexBufferLayout;
using wgpu::AdapterInfo;
using wgpu::BindGroupDescriptor;
using wgpu::BindGroupLayoutDescriptor;
using wgpu::ColorTargetState;
using wgpu::CompilationInfo;
using wgpu::ComputePipelineDescriptor;
using wgpu::DeviceDescriptor;
using wgpu::RenderPassDescriptor;
using wgpu::TextureViewDescriptor;
using wgpu::VertexState;
using wgpu::FragmentState;
using wgpu::RenderPipelineDescriptor;

// Free Functions
using wgpu::CreateInstance;
using wgpu::GetInstanceFeatures;
using wgpu::GetInstanceLimits;
using wgpu::HasInstanceFeature;
using wgpu::GetProcAddress;

}
