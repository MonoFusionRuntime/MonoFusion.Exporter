using System.Runtime.InteropServices;

namespace MonoFusion.Exporter.Exporters.Boiler
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public unsafe struct FusionFontNative
	{
		public int lfHeight;
		public int lfWidth;
		public int lfEscapement;
		public int lfOrientation;
		public int lfWeight;
		public byte lfItalic;
		public byte lfUnderline;
		public byte lfStrikeOut;
		public byte lfCharSet;
		public byte lfOutPrecision;
		public byte lfClipPrecision;
		public byte lfQuality;
		public byte lfPitchAndFamily;
		public fixed ushort lfFaceName[32];
	}

	public struct FusionFont
	{
		public int lfHeight;
		public int lfWidth;
		public int lfEscapement;
		public int lfOrientation;
		public int lfWeight;
		public bool lfItalic;
		public bool lfUnderline;
		public bool lfStrikeOut;
		public byte lfCharSet;
		public byte lfOutPrecision;
		public byte lfClipPrecision;
		public byte lfQuality;
		public byte lfPitchAndFamily;
		public string lfFaceName;

		public unsafe static FusionFont FromNative(FusionFontNative* native)
		{
			FusionFont font = new FusionFont
			{
				lfHeight         = native->lfHeight,
				lfWidth          = native->lfWidth,
				lfEscapement     = native->lfEscapement,
				lfOrientation    = native->lfOrientation,
				lfWeight         = native->lfWeight,
				lfItalic         = native->lfItalic != 0,
				lfUnderline      = native->lfUnderline != 0,
				lfStrikeOut      = native->lfStrikeOut != 0,
				lfCharSet        = native->lfCharSet,
				lfOutPrecision   = native->lfOutPrecision,
				lfClipPrecision  = native->lfClipPrecision,
				lfQuality        = native->lfQuality,
				lfPitchAndFamily = native->lfPitchAndFamily
			};

			font.lfFaceName = new string((char*)native->lfFaceName);
			int nullIndex = font.lfFaceName.IndexOf('\0');
			if (nullIndex >= 0)
				font.lfFaceName = font.lfFaceName.Substring(0, nullIndex);

			return font;
		}
	}
}
