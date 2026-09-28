#!/usr/bin/env python3
#
# Copyright (c) 2026, Lars Brubaker
# All rights reserved.
#
# Redistribution and use in source and binary forms, with or without modification, are permitted
# provided that the conditions of the agg-sharp BSD 2-clause licence are met. See LICENSE.
"""Packs C++ AGG's embedded raster fonts (src/agg_embedded_raster_fonts.cpp) into agg/Font/EmbeddedRasterFonts.bin.

  scripts/gen-embedded-raster-fonts.py [path/to/agg_embedded_raster_fonts.cpp]

The default source is the AGG copy agg-rust keeps (~/Development/rust-apps/agg-rust/cpp-references/agg-src).
The fonts are ~34 byte arrays of up to 10k values each; as C# literals they would be several 800-line files
that the compiler has to chew through, so they ship as one embedded resource that EmbeddedRasterFonts reads.

Format, all little endian: for each font in the .cpp's order, a u8 name length, the ASCII name, a u32 data
length, then the font's bytes exactly as the C++ array holds them (glyph_raster_bin reads them unchanged).
"""
import os
import re
import struct
import sys

default_source = os.path.expanduser(
    "~/Development/rust-apps/agg-rust/cpp-references/agg-src/src/agg_embedded_raster_fonts.cpp")
source = sys.argv[1] if len(sys.argv) > 1 else default_source
repo_root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
out_path = os.path.join(repo_root, "agg", "Font", "EmbeddedRasterFonts.bin")

with open(source, encoding="latin-1") as f:
    text = f.read()

# Strip // comments first: they hold the glyph names, e.g. // 0x2c ',' - which would otherwise read as values.
text = re.sub(r"//[^\n]*", "", text)

out = bytearray()
count = 0
for match in re.finditer(r"const\s+int8u\s+(\w+)\s*\[\s*\]\s*=\s*\{(.*?)\};", text, re.S):
    name, body = match.group(1), match.group(2)
    values = []
    for token in body.split(","):
        token = token.strip()
        if not token:
            continue
        # The header's fourth value is written as 128-32 (a glyph count): evaluate plain integer arithmetic.
        if not re.fullmatch(r"[0-9a-fA-Fx+\- ]+", token):
            raise SystemExit(f"{name}: unexpected token {token!r}")
        value = eval(token, {"__builtins__": {}})
        if not 0 <= value <= 255:
            raise SystemExit(f"{name}: value {value} out of byte range")
        values.append(value)
    encoded_name = name.encode("ascii")
    out += struct.pack("<B", len(encoded_name)) + encoded_name
    out += struct.pack("<I", len(values)) + bytes(values)
    count += 1

if count == 0:
    raise SystemExit(f"no fonts found in {source}")

with open(out_path, "wb") as f:
    f.write(out)
print(f"wrote {count} fonts, {len(out)} bytes, to {out_path}")
