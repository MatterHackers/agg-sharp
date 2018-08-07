@echo off

fxc /E VS /T vs_5_0 Vertex.hlsl /Fo Vertex.hlsl.bytes
fxc /E FS /T ps_5_0 Fragment.hlsl /Fo Fragment.hlsl.bytes