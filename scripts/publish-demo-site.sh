#!/usr/bin/env bash
#
# Copyright (c) 2026, Lars Brubaker
# All rights reserved.
#
# Redistribution and use in source and binary forms, with or without modification, are permitted
# provided that the conditions of the agg-sharp BSD 2-clause licence are met. See LICENSE.
#
# Publishes the agg-sharp demo site (examples/AggSharpDemo/AggSharpDemo.Browser) exactly as the
# GitHub Pages workflow (.github/workflows/pages.yml) does - the workflow calls this script, so a
# local run and CI build the same thing. Prints the folder to serve as its last line.
#
# Needs the wasm-tools workload (`dotnet workload install wasm-tools`): LinkEmdawnWebGpu=true is the
# emcc relink that makes the page paint, and AOT compilation needs it too. The first link seeds a ~215 MB Emscripten cache under
# WebGpu/Browser/emscripten-cache (see WebGpu/build/WebGpuBrowser.targets); later ones reuse it.
#
# Serve the output from any static server, at a domain root or under a subpath - index.html uses a
# relative <base href>, so /agg-sharp/ on GitHub Pages works the same as / locally.

set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$root/examples/AggSharpDemo/AggSharpDemo.Browser/AggSharpDemo.Browser.csproj"
site="$root/examples/AggSharpDemo/AggSharpDemo.Browser/bin/Release/net10.0/publish/wwwroot"

# Release, because publish then trims every assembly and compiles it to wasm ahead of time (the
# project's TrimMode and RunAOTCompilation). The browser providers are resolved by type name; the
# DynamicDependency attributes on BrowserHostBootstrap.InitializeAsync are what keep them.
dotnet publish "$project" -c Release -p:LinkEmdawnWebGpu=true >&2

# Jekyll drops every path that starts with an underscore - Blazor's whole _framework folder. The
# Actions deployment in pages.yml does not run Jekyll, but a branch-based Pages source would, and
# this marker keeps the site working whichever way it is served.
touch "$site/.nojekyll"

echo "$site"
