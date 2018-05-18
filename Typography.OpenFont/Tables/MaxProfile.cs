﻿//Apache2, 2017, WinterDev
//Apache2, 2014-2016, Samuel Carlsson, WinterDev

using System.IO;
namespace Typography.OpenFont.Tables
{
    class MaxProfile : TableEntry
    {

        public override string Name
        {
            get { return "maxp"; }
        }
        public uint Version { get; private set; }
        public ushort GlyphCount { get; private set; }
        public ushort MaxPointsPerGlyph { get; private set; }
        public ushort MaxContoursPerGlyph { get; private set; }
        public ushort MaxPointsPerCompositeGlyph { get; private set; }
        public ushort MaxContoursPerCompositeGlyph { get; private set; }
        public ushort MaxZones { get; private set; }
        public ushort MaxTwilightPoints { get; private set; }
        public ushort MaxStorage { get; private set; }
        public ushort MaxFunctionDefs { get; private set; }
        public ushort MaxInstructionDefs { get; private set; }
        public ushort MaxStackElements { get; private set; }
        public ushort MaxSizeOfInstructions { get; private set; }
        public ushort MaxComponentElements { get; private set; }
        public ushort MaxComponentDepth { get; private set; }

        protected override void ReadContentFrom(BinaryReader input)
        {
            Version = input.ReadUInt32(); // 0x00010000 == 1.0
            GlyphCount = input.ReadUInt16();
            MaxPointsPerGlyph = input.ReadUInt16();
            MaxContoursPerGlyph = input.ReadUInt16();
            MaxPointsPerCompositeGlyph = input.ReadUInt16();
            MaxContoursPerCompositeGlyph = input.ReadUInt16();
            MaxZones = input.ReadUInt16();
            MaxTwilightPoints = input.ReadUInt16();
            MaxStorage = input.ReadUInt16();
            MaxFunctionDefs = input.ReadUInt16();
            MaxInstructionDefs = input.ReadUInt16();
            MaxStackElements = input.ReadUInt16();
            MaxSizeOfInstructions = input.ReadUInt16();
            MaxComponentElements = input.ReadUInt16();
            MaxComponentDepth = input.ReadUInt16();
        }
    }
}
