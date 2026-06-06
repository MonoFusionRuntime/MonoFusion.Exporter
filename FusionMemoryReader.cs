using System.Text;

namespace MonoFusion.Exporter
{
	internal class FusionMemoryReader : BinaryReader
	{
		private int _current;
		private int _bitsLeft;

		public FusionMemoryReader(PartialStream stream) : base(stream)
		{
		}

		public void Skip(long length)
		{
			BaseStream.Position += length;
		}

		public void Seek(long position)
		{
			BaseStream.Position = position;
		}

		public long Tell()
		{
			return BaseStream.Position;
		}

		public long Size()
		{
			return BaseStream.Length;
		}

		public bool End()
		{
			return BaseStream.Position == BaseStream.Length;
		}
		public bool HasMemory(int size)
		{
			return Size() - Tell() >= size;
		}

		public string ReadCString()
		{
			uint length = ReadUInt32();
			bool unicode = (length | 0x80000000) != 0;
			length &= 0x7FFFFFFF;
			if (unicode)
				return Encoding.Unicode.GetString(ReadBytes((int)length * 2));
			return Encoding.ASCII.GetString(ReadBytes((int)length));
		}

		public void SkipCString(uint count = 1)
		{
			for (uint i = 0; i < count; i++)
			{
				uint length = ReadUInt32();
				bool unicode = (length | 0x80000000) != 0;
				length &= 0x7FFFFFFF;
				Skip(length * (unicode ? 2 : 1));
			}
		}

		public string ReadUnicodeString(int length = -1)
		{
			string str = "";
			if (Tell() >= Size()) return str;
			if (length >= 0)
				for (int i = 0; i < length; i++)
					str += Convert.ToChar(ReadUInt16());
			else
			{
				var b = ReadUInt16();
				while (b != 0)
				{
					str += Convert.ToChar(b);
					if (!HasMemory(2)) break;
					b = ReadUInt16();
				}
			}

			return str;
		}

		public void SkipCValue(int count = 1)
		{
			for (int i = 0; i < count; i++)
			{
				SkipCString();
				uint type = ReadUInt32();
				if (type == 2)
					SkipCString();
				else
					Skip(4);
			}
		}

		public uint[] ReadUInt32Array(uint count)
		{
			uint[] output = new uint[count];
			for (int i = 0; i < count; i++)
				output[i] = ReadUInt32();
			return output;
		}

		public string ReadASCII(uint length)
		{
			byte[] data = ReadBytes((int)length);
			int nullIndex = Array.IndexOf(data, (byte)0);
			length = nullIndex == -1 ? (uint)data.Length : (uint)nullIndex;
			return Encoding.ASCII.GetString(data, 0, (int)length);
		}

		public string ReadASCII()
		{
			long start = Tell();
			while (ReadByte() != 0) {}
			uint length = (uint)(Tell() - start - 1);
			Seek(start);
			byte[] data = ReadBytes((int)length);
			Skip(1); // Null Terminator Byte
			return Encoding.ASCII.GetString(data, 0, (int)length);
		}

		public string ReadUnicode(uint length)
		{
			string result = Encoding.Unicode.GetString(ReadBytes((int)length * 2));
			int nullIndex = result.IndexOf('\0');
			return nullIndex == -1 ? result : result[..nullIndex];
		}
	}
}
