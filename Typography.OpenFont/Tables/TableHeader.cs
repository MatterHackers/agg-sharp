﻿//Apache2, 2017, WinterDev
//Apache2, 2014-2016, Samuel Carlsson, WinterDev

namespace Typography.OpenFont.Tables
{
    struct TableHeader
    {
        readonly uint _tag;
        readonly uint _checkSum;
        readonly uint _offset;
        readonly uint _length;

        public TableHeader(uint tag, uint checkSum, uint offset, uint len)
        {
            _tag = tag;
            _checkSum = checkSum;
            _offset = offset;
            _length = len;
        }
        public string Tag { get { return Utils.TagToString(_tag); } }

        //// TODO: Take offset parameter as commonly two seeks are made in a row
        //public BinaryReader GetDataReader()
        //{
        //    _input.BaseStream.Seek(_offset, SeekOrigin.Begin);
        //    // TODO: Limit reading to _length by wrapping BinaryReader (or Stream)?
        //    return _input;
        //}
        public uint Offset { get { return _offset; } }
        public uint CheckSum { get { return _checkSum; } }
        public uint Length { get { return _length; } }

        public override string ToString()
        {
            return "{" + Tag + "}";
        }


    }
}
