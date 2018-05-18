﻿//Apache2, 2017, WinterDev
//Apache2, 2014-2016, Samuel Carlsson, WinterDev

using System.IO;
using System.Text;
namespace Typography.OpenFont.Tables
{
    class NameEntry : TableEntry
    {
        public override string Name
        {
            get { return "name"; }
        }

        protected override void ReadContentFrom(BinaryReader reader)
        {

            ushort uFSelector = reader.ReadUInt16();
            ushort uNRCount = reader.ReadUInt16();
            ushort uStorageOffset = reader.ReadUInt16();

            TT_NAME_RECORD ttRecord = new TT_NAME_RECORD();

            uint offset = this.Header.Offset;
            for (int j = 0; j <= uNRCount; j++)
            {
                ttRecord = new TT_NAME_RECORD()
                {
                    uPlatformID = reader.ReadUInt16(),
                    uEncodingID = reader.ReadUInt16(),
                    uLanguageID = reader.ReadUInt16(),
                    uNameID = reader.ReadUInt16(),
                    uStringLength = reader.ReadUInt16(),
                    uStringOffset = reader.ReadUInt16(),
                };

                //if (ttRecord.uNameID > 2)
                //{

                //}

                long nPos = reader.BaseStream.Position;
                reader.BaseStream.Seek(offset + ttRecord.uStringOffset + uStorageOffset, SeekOrigin.Begin);

                byte[] buf = reader.ReadBytes(ttRecord.uStringLength);
                Encoding enc2;
                if (ttRecord.uEncodingID == 3 || ttRecord.uEncodingID == 1)
                {
                    
                    enc2 = Encoding.BigEndianUnicode;
                }
                else
                {
                    enc2 = Encoding.UTF8;
                }
                string strRet = enc2.GetString(buf, 0, buf.Length);
                switch (ttRecord.uNameID)
                {
                    case 1:
                        FontName = strRet;
                        break;
                    case 2:
                        FontSubFamily = strRet;
                        break;
                    default:

                        break;
                }
                //move to saved pos
                reader.BaseStream.Seek(nPos, SeekOrigin.Begin);
            }
        }

        public string FontName { get; private set; }
        public string FontSubFamily { get; private set; }

        struct TT_NAME_RECORD
        {
            public ushort uPlatformID;
            public ushort uEncodingID;
            public ushort uLanguageID;
            public ushort uNameID;
            public ushort uStringLength;
            public ushort uStringOffset;
        }
    }

}
