/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;

namespace MatterHackers.Agg
{
	/// <summary>
	/// C++ AGG's <c>sRGB_lut&lt;int8u&gt;</c>: the 8-bit tables C++ converts through whenever a color crosses
	/// between <c>rgba8</c> (linear) and <c>srgba8</c>. C++ does that silently on assignment, so an example that
	/// names <c>srgba8(127, 127, 127)</c> and renders into an rgba8 pixel format draws 54, and a linear color
	/// stored into an srgba8 and read back comes out shifted for dark channels. Ports that must match C++ byte
	/// for byte do those conversions explicitly through here.
	/// </summary>
	public static class SrgbLut
	{
		/// <summary>C++ <c>m_dir_table</c>: uround(255 * sRGB_to_linear(i / 255)).</summary>
		private static readonly byte[] LinearFromSrgbTable = BuildTable(x => x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4));

		/// <summary>C++ <c>m_inv_table</c>: uround(255 * linear_to_sRGB(i / 255)).</summary>
		private static readonly byte[] SrgbFromLinearTable = BuildTable(x => x <= 0.0031308 ? x * 12.92 : (1.055 * Math.Pow(x, 1 / 2.4)) - 0.055);

		/// <summary>C++ <c>sRGB_conv&lt;int8u&gt;::rgb_from_sRGB</c>.</summary>
		public static byte LinearFromSrgb(int srgb) => LinearFromSrgbTable[srgb];

		/// <summary>C++ <c>sRGB_conv&lt;int8u&gt;::rgb_to_sRGB</c>.</summary>
		public static byte SrgbFromLinear(int linear) => SrgbFromLinearTable[linear];

		/// <summary>
		/// C++ <c>rgba8(srgba8(r, g, b, a))</c>: the linear color an srgba8 draws as in an rgba8 pixel format.
		/// Alpha is not converted.
		/// </summary>
		public static Color FromSrgba8(int r, int g, int b, int a = 255)
		{
			return new Color(LinearFromSrgb(r), LinearFromSrgb(g), LinearFromSrgb(b), a);
		}

		/// <summary>C++ <c>rgba8(srgba8(color))</c>: a linear color stored as srgba8 and read back.</summary>
		public static Color RoundTripThroughSrgba8(Color linear)
		{
			return FromSrgba8(SrgbFromLinear(linear.red), SrgbFromLinear(linear.green), SrgbFromLinear(linear.blue), linear.alpha);
		}

		/// <summary>
		/// C++ <c>rgba8(srgba8(rgba(r, g, b, a)))</c>: a floating-point color assigned to an srgba8 and drawn into
		/// an rgba8 pixel format. C++ converts rgba to srgba8 through the <c>float</c> table (a binary search, not
		/// the 8-bit table), so this is not the same as <see cref="RoundTripThroughSrgba8"/> of the rgba8.
		/// </summary>
		public static Color FromRgbaThroughSrgba8(double r, double g, double b, double a = 1.0)
		{
			Color srgb = Srgba8FromRgba(r, g, b, a);
			return FromSrgba8(srgb.red, srgb.green, srgb.blue, srgb.alpha);
		}

		/// <summary>
		/// C++ <c>srgba8(rgba(r, g, b, a))</c>: the srgba8 bytes themselves, which is what an sRGB pixel format
		/// (<c>pixfmt_sbgr24</c> and friends, an example built with <c>AGG_SBGR24</c>) stores and shows.
		/// </summary>
		public static Color Srgba8FromRgba(double r, double g, double b, double a = 1.0)
		{
			// C++ alpha_to_sRGB(float): int8u(0.5 + x * 255), the product in float.
			float alpha = (float)a;
			int srgbAlpha = alpha < 0 ? 0 : alpha > 1 ? 255 : (int)(0.5 + (float)(alpha * 255f));
			return new Color(SrgbFromLinearFloat((float)r), SrgbFromLinearFloat((float)g), SrgbFromLinearFloat((float)b), srgbAlpha);
		}

		/// <summary>
		/// C++ <c>srgba8(rgba32)</c>: a float (AGG_BGRA128/RGBA128) pixel as the 8-bit sRGB bytes a C++ example
		/// shows it as - channels through the float inverse table, alpha <c>int8u(0.5 + a * 255)</c>.
		/// </summary>
		public static Color Srgba8FromRgba32(ColorF c)
		{
			return Srgba8FromRgba(c.red, c.green, c.blue, c.alpha);
		}

		/// <summary>
		/// C++ <c>sRGB_conv&lt;float&gt;::rgb_from_sRGB</c>: the linear value, 0 to 1, of an srgba8 channel - what
		/// <c>rgba(srgba8)</c> and <c>rgba32(srgba8)</c> hold.
		/// </summary>
		public static float LinearFromSrgbFloat(int srgb) => BitConverter.UInt32BitsToSingle(LinearFromSrgbFloatBits[srgb]);

		/// <summary>
		/// C++ <c>sRGB_conv&lt;float&gt;::alpha_from_sRGB</c>: <c>float(x * (1 / 255.0))</c>, the product in double.
		/// Alpha is linear in sRGB, so it is only scaled.
		/// </summary>
		public static float AlphaFromSrgbFloat(int srgb) => (float)(srgb * (1 / 255.0));

		/// <summary>
		/// C++ <c>rgba32(srgba8(r, g, b, a))</c>: the float color an srgba8 draws as in a float (AGG_BGRA128/RGBA128)
		/// pixel format - channels through the float sRGB table, alpha only scaled.
		/// </summary>
		public static ColorF Rgba32FromSrgba8(int r, int g, int b, int a = 255)
		{
			return new ColorF(LinearFromSrgbFloat(r), LinearFromSrgbFloat(g), LinearFromSrgbFloat(b), AlphaFromSrgbFloat(a));
		}

		/// <summary>
		/// C++ <c>sRGB_lut&lt;float&gt;::m_dir_table</c>: float(sRGB_to_linear(i / 255)), entry 0 is 0. The bit patterns
		/// are copied from a C++ trace rather than computed with Math.Pow: pow is not correctly rounded, and
		/// the C runtimes .NET calls into on Windows, macOS and Linux may differ in the last place - enough to
		/// flip a float now and then. Baked, every platform draws what C++ draws.
		/// </summary>
		private static readonly uint[] LinearFromSrgbFloatBits =
		{
			0x00000000u, 0x399F22B4u, 0x3A1F22B4u, 0x3A6EB40Eu, 0x3A9F22B4u, 0x3AC6EB61u, 0x3AEEB40Eu, 0x3B0B3E5Du,
			0x3B1F22B4u, 0x3B33070Au, 0x3B46EB61u, 0x3B5B518Eu, 0x3B70F18Fu, 0x3B83E1C6u, 0x3B8FE616u, 0x3B9C87FDu,
			0x3BA9C9B6u, 0x3BB7AD6Fu, 0x3BC6354Au, 0x3BD56360u, 0x3BE539C1u, 0x3BF5BA71u, 0x3C0373B6u, 0x3C0C6153u,
			0x3C15A705u, 0x3C1F45BEu, 0x3C293E6Bu, 0x3C3391F7u, 0x3C3E4149u, 0x3C494D44u, 0x3C54B6C9u, 0x3C607EB4u,
			0x3C6CA5DFu, 0x3C792D22u, 0x3C830AA9u, 0x3C89AF9Fu, 0x3C9085DCu, 0x3C978DC6u, 0x3C9EC7C2u, 0x3CA63433u,
			0x3CADD37Du, 0x3CB5A602u, 0x3CBDAC21u, 0x3CC5E63Au, 0x3CCE54ACu, 0x3CD6F7D5u, 0x3CDFD010u, 0x3CE8DDBAu,
			0x3CF2212Du, 0x3CFB9AC3u, 0x3D02A56Au, 0x3D0798DDu, 0x3D0CA7E6u, 0x3D11D2AFu, 0x3D171964u, 0x3D1C7C30u,
			0x3D21FB3Cu, 0x3D2796B2u, 0x3D2D4EBBu, 0x3D332381u, 0x3D39152Bu, 0x3D3F23E4u, 0x3D454FD2u, 0x3D4B991Du,
			0x3D51FFECu, 0x3D588468u, 0x3D5F26B6u, 0x3D65E6FDu, 0x3D6CC563u, 0x3D73C20Eu, 0x3D7ADD24u, 0x3D810B65u,
			0x3D84B793u, 0x3D88732Eu, 0x3D8C3E48u, 0x3D9018F4u, 0x3D940344u, 0x3D97FD49u, 0x3D9C0715u, 0x3DA020BAu,
			0x3DA44A4Au, 0x3DA883D6u, 0x3DACCD6Fu, 0x3DB12727u, 0x3DB5910Fu, 0x3DBA0B38u, 0x3DBE95B3u, 0x3DC33090u,
			0x3DC7DBE0u, 0x3DCC97B4u, 0x3DD1641Du, 0x3DD6412Bu, 0x3DDB2EEEu, 0x3DE02D76u, 0x3DE53CD4u, 0x3DEA5D18u,
			0x3DEF8E51u, 0x3DF4D090u, 0x3DFA23E5u, 0x3DFF885Eu, 0x3E027F06u, 0x3E05427Fu, 0x3E080EA2u, 0x3E0AE377u,
			0x3E0DC104u, 0x3E10A753u, 0x3E13966Au, 0x3E168E51u, 0x3E198F0Fu, 0x3E1C98ACu, 0x3E1FAB30u, 0x3E22C6A1u,
			0x3E25EB07u, 0x3E29186Au, 0x3E2C4ED0u, 0x3E2F8E42u, 0x3E32D6C5u, 0x3E362862u, 0x3E39831Fu, 0x3E3CE703u,
			0x3E405417u, 0x3E43CA60u, 0x3E4749E6u, 0x3E4AD2AFu, 0x3E4E64C3u, 0x3E520029u, 0x3E55A4E7u, 0x3E595305u,
			0x3E5D0A89u, 0x3E60CB7Au, 0x3E6495DFu, 0x3E6869BEu, 0x3E6C471Fu, 0x3E702E07u, 0x3E741E7Eu, 0x3E78188Bu,
			0x3E7C1C33u, 0x3E8014BFu, 0x3E822039u, 0x3E84308Bu, 0x3E8645B8u, 0x3E885FC3u, 0x3E8A7EB0u, 0x3E8CA281u,
			0x3E8ECB3Bu, 0x3E90F8DFu, 0x3E932B72u, 0x3E9562F6u, 0x3E979F6Fu, 0x3E99E0E0u, 0x3E9C274Cu, 0x3E9E72B6u,
			0x3EA0C321u, 0x3EA31890u, 0x3EA57307u, 0x3EA7D288u, 0x3EAA3716u, 0x3EACA0B6u, 0x3EAF0F68u, 0x3EB18332u,
			0x3EB3FC15u, 0x3EB67A14u, 0x3EB8FD34u, 0x3EBB8576u, 0x3EBE12DEu, 0x3EC0A56Eu, 0x3EC33D2Au, 0x3EC5DA14u,
			0x3EC87C30u, 0x3ECB2380u, 0x3ECDD008u, 0x3ED081CAu, 0x3ED338C9u, 0x3ED5F508u, 0x3ED8B68Au, 0x3EDB7D52u,
			0x3EDE4963u, 0x3EE11ABFu, 0x3EE3F169u, 0x3EE6CD65u, 0x3EE9AEB5u, 0x3EEC955Bu, 0x3EEF815Cu, 0x3EF272B8u,
			0x3EF56974u, 0x3EF86593u, 0x3EFB6716u, 0x3EFE6E00u, 0x3F00BD2Bu, 0x3F02460Cu, 0x3F03D1A5u, 0x3F055FF7u,
			0x3F06F104u, 0x3F0884CDu, 0x3F0A1B54u, 0x3F0BB499u, 0x3F0D509Fu, 0x3F0EEF65u, 0x3F1090EFu, 0x3F12353Du,
			0x3F13DC50u, 0x3F15862Au, 0x3F1732CCu, 0x3F18E237u, 0x3F1A946Eu, 0x3F1C4970u, 0x3F1E0140u, 0x3F1FBBDEu,
			0x3F21794Du, 0x3F23398Cu, 0x3F24FC9Fu, 0x3F26C285u, 0x3F288B41u, 0x3F2A56D2u, 0x3F2C253Cu, 0x3F2DF67Fu,
			0x3F2FCA9Cu, 0x3F31A194u, 0x3F337B6Au, 0x3F35581Du, 0x3F3737B0u, 0x3F391A24u, 0x3F3AFF7Au, 0x3F3CE7B2u,
			0x3F3ED2CFu, 0x3F40C0D2u, 0x3F42B1BCu, 0x3F44A58Eu, 0x3F469C49u, 0x3F4895EFu, 0x3F4A9280u, 0x3F4C91FFu,
			0x3F4E946Cu, 0x3F5099C9u, 0x3F52A216u, 0x3F54AD56u, 0x3F56BB88u, 0x3F58CCAFu, 0x3F5AE0CCu, 0x3F5CF7DFu,
			0x3F5F11EAu, 0x3F612EEFu, 0x3F634EEEu, 0x3F6571E9u, 0x3F6797E0u, 0x3F69C0D5u, 0x3F6BECCAu, 0x3F6E1BBFu,
			0x3F704DB5u, 0x3F7282AEu, 0x3F74BAABu, 0x3F76F5AEu, 0x3F7933B6u, 0x3F7B74C6u, 0x3F7DB8DEu, 0x3F800000u,
		};

		/// <summary>
		/// C++ <c>sRGB_lut&lt;float&gt;::m_inv_table</c>: float(sRGB_to_linear((i - 0.5) / 255)), entry 0 is 0 - the
		/// thresholds the float-to-sRGB search compares against. Baked from the same C++ trace as
		/// <see cref="LinearFromSrgbFloatBits"/>, for the same reason: a pow a last place off moves a threshold,
		/// and a float frame's pixel lying on it comes out a level off.
		/// </summary>
		private static readonly uint[] FloatInverseBits =
		{
			0x00000000u, 0x391F22B4u, 0x39EEB40Eu, 0x3A46EB61u, 0x3A8B3E5Du, 0x3AB3070Au, 0x3ADACFB7u, 0x3B014C32u,
			0x3B153089u, 0x3B2914DFu, 0x3B3CF935u, 0x3B50F2D0u, 0x3B65FB9Au, 0x3B7C3403u, 0x3B89D05Fu, 0x3B962333u,
			0x3BA314BDu, 0x3BB0A730u, 0x3BBEDCB6u, 0x3BCDB76Du, 0x3BDD3967u, 0x3BED64AFu, 0x3BFE3B45u, 0x3C07DF91u,
			0x3C10F91Au, 0x3C1A6B31u, 0x3C2436C7u, 0x3C2E5CC7u, 0x3C38DE19u, 0x3C43BBA4u, 0x3C4EF648u, 0x3C5A8EE4u,
			0x3C668654u, 0x3C72DD70u, 0x3C7F950Fu, 0x3C865702u, 0x3C8D148Eu, 0x3C940395u, 0x3C9B247Bu, 0x3CA277A6u,
			0x3CA9FD77u, 0x3CB1B652u, 0x3CB9A298u, 0x3CC1C2A8u, 0x3CCA16E2u, 0x3CD29FA4u, 0x3CDB5D4Au, 0x3CE45031u,
			0x3CED78B4u, 0x3CF6D72Eu, 0x3D0035FBu, 0x3D051BB4u, 0x3D0A1CECu, 0x3D0F39D0u, 0x3D14728Au, 0x3D19C745u,
			0x3D1F382Bu, 0x3D24C567u, 0x3D2A6F21u, 0x3D303584u, 0x3D3618B7u, 0x3D3C18E3u, 0x3D423631u, 0x3D4870C9u,
			0x3D4EC8D2u, 0x3D553E72u, 0x3D5BD1D2u, 0x3D628318u, 0x3D69526Au, 0x3D703FEEu, 0x3D774BCAu, 0x3D7E7623u,
			0x3D82DF90u, 0x3D869372u, 0x3D8A56CAu, 0x3D8E29ABu, 0x3D920C26u, 0x3D95FE4Eu, 0x3D9A0035u, 0x3D9E11EBu,
			0x3DA23384u, 0x3DA6650Fu, 0x3DAAA6A0u, 0x3DAEF847u, 0x3DB35A14u, 0x3DB7CC1Bu, 0x3DBC4E6Au, 0x3DC0E114u,
			0x3DC58428u, 0x3DCA37B9u, 0x3DCEFBD5u, 0x3DD3D08Eu, 0x3DD8B5F5u, 0x3DDDAC18u, 0x3DE2B309u, 0x3DE7CAD8u,
			0x3DECF395u, 0x3DF22D4Fu, 0x3DF77817u, 0x3DFCD3FCu, 0x3E012087u, 0x3E03DFAEu, 0x3E06A77Bu, 0x3E0977F6u,
			0x3E0C5126u, 0x3E0F3313u, 0x3E121DC5u, 0x3E151143u, 0x3E180D95u, 0x3E1B12C1u, 0x3E1E20D1u, 0x3E2137CAu,
			0x3E2457B5u, 0x3E278099u, 0x3E2AB27Cu, 0x3E2DED67u, 0x3E313160u, 0x3E347E70u, 0x3E37D49Cu, 0x3E3B33ECu,
			0x3E3E9C67u, 0x3E420E14u, 0x3E4588FBu, 0x3E490D21u, 0x3E4C9A8Fu, 0x3E50314Bu, 0x3E53D15Du, 0x3E577ACAu,
			0x3E5B2D9Au, 0x3E5EE9D3u, 0x3E62AF7Du, 0x3E667E9Fu, 0x3E6A573Eu, 0x3E6E3961u, 0x3E722510u, 0x3E761A52u,
			0x3E7A192Bu, 0x3E7E21A4u, 0x3E8119E1u, 0x3E8327C7u, 0x3E853A86u, 0x3E875221u, 0x3E896E9Du, 0x3E8B8FFCu,
			0x3E8DB641u, 0x3E8FE16Fu, 0x3E92118Bu, 0x3E944696u, 0x3E968094u, 0x3E98BF89u, 0x3E9B0377u, 0x3E9D4C61u,
			0x3E9F9A4Bu, 0x3EA1ED38u, 0x3EA4452Au, 0x3EA6A226u, 0x3EA9042Du, 0x3EAB6B44u, 0x3EADD76Cu, 0x3EB048AAu,
			0x3EB2BF00u, 0x3EB53A71u, 0x3EB7BB00u, 0x3EBA40B0u, 0x3EBCCB85u, 0x3EBF5B80u, 0x3EC1F0A6u, 0x3EC48AF9u,
			0x3EC72A7Cu, 0x3EC9CF32u, 0x3ECC791Du, 0x3ECF2842u, 0x3ED1DCA2u, 0x3ED49640u, 0x3ED75521u, 0x3EDA1945u,
			0x3EDCE2B1u, 0x3EDFB167u, 0x3EE2856Au, 0x3EE55EBDu, 0x3EE83D62u, 0x3EEB215Du, 0x3EEE0AB0u, 0x3EF0F95Eu,
			0x3EF3ED6Au, 0x3EF6E6D7u, 0x3EF9E5A7u, 0x3EFCE9DEu, 0x3EFFF37Du, 0x3F018144u, 0x3F030B81u, 0x3F049877u,
			0x3F062826u, 0x3F07BA91u, 0x3F094FB9u, 0x3F0AE79Fu, 0x3F0C8244u, 0x3F0E1FAAu, 0x3F0FBFD2u, 0x3F1162BDu,
			0x3F13086Du, 0x3F14B0E4u, 0x3F165C22u, 0x3F180A28u, 0x3F19BAF9u, 0x3F1B6E95u, 0x3F1D24FEu, 0x3F1EDE35u,
			0x3F209A3Bu, 0x3F225912u, 0x3F241ABBu, 0x3F25DF37u, 0x3F27A688u, 0x3F2970AFu, 0x3F2B3DACu, 0x3F2D0D82u,
			0x3F2EE032u, 0x3F30B5BDu, 0x3F328E23u, 0x3F346968u, 0x3F36478Bu, 0x3F38288Eu, 0x3F3A0C73u, 0x3F3BF339u,
			0x3F3DDCE4u, 0x3F3FC974u, 0x3F41B8EAu, 0x3F43AB48u, 0x3F45A08Eu, 0x3F4798BEu, 0x3F4993DAu, 0x3F4B91E2u,
			0x3F4D92D8u, 0x3F4F96BDu, 0x3F519D91u, 0x3F53A758u, 0x3F55B410u, 0x3F57C3BDu, 0x3F59D65Fu, 0x3F5BEBF6u,
			0x3F5E0486u, 0x3F60200Du, 0x3F623E8Fu, 0x3F64600Cu, 0x3F668485u, 0x3F68ABFBu, 0x3F6AD670u, 0x3F6D03E4u,
			0x3F6F345Au, 0x3F7167D1u, 0x3F739E4Cu, 0x3F75D7CCu, 0x3F781451u, 0x3F7A53DDu, 0x3F7C9671u, 0x3F7EDC0Eu,
		};

		private static float FloatInverseTable(int i) => BitConverter.UInt32BitsToSingle(FloatInverseBits[i]);

		/// <summary>C++ <c>sRGB_lut_base&lt;float&gt;::inv</c>: the unrolled binary search over the float table.</summary>
		private static int SrgbFromLinearFloat(float v)
		{
			int x = 0;
			if (v > FloatInverseTable(128)) x = 128;
			if (v > FloatInverseTable(x + 64)) x += 64;
			if (v > FloatInverseTable(x + 32)) x += 32;
			if (v > FloatInverseTable(x + 16)) x += 16;
			if (v > FloatInverseTable(x + 8)) x += 8;
			if (v > FloatInverseTable(x + 4)) x += 4;
			if (v > FloatInverseTable(x + 2)) x += 2;
			if (v > FloatInverseTable(x + 1)) x += 1;
			return x;
		}

		private static byte[] BuildTable(Func<double, double> convert)
		{
			// Entry 0 stays 0, as C++ sets it apart from the loop.
			var table = new byte[256];
			for (int i = 1; i <= 255; i++)
			{
				table[i] = (byte)Util.uround(255.0 * convert(i / 255.0));
			}

			return table;
		}
	}
}
