using System;
using System.Collections.Generic;
using System.Text;

namespace MonoFusion.Exporter
{
	public static class IcoWriter
	{
		public static void WriteIco(byte[] pngData, string path, int size = 256)
		{
			using var fs = new FileStream(path, FileMode.Create);

			// ICONDIR
			fs.Write(BitConverter.GetBytes((ushort)0)); // reserved
			fs.Write(BitConverter.GetBytes((ushort)1)); // type = icon
			fs.Write(BitConverter.GetBytes((ushort)1)); // count

			byte w = (byte)(size == 256 ? 0 : size);
			byte h = (byte)(size == 256 ? 0 : size);

			// ICONDIRENTRY
			fs.WriteByte(w);
			fs.WriteByte(h);
			fs.WriteByte(0); // palette
			fs.WriteByte(0); // reserved
			fs.Write(BitConverter.GetBytes((ushort)0)); // color planes
			fs.Write(BitConverter.GetBytes((ushort)32)); // bpp
			fs.Write(BitConverter.GetBytes((uint)pngData.Length));
			fs.Write(BitConverter.GetBytes((uint)(6 + 16))); // offset

			fs.Write(pngData);
		}
	}
}
