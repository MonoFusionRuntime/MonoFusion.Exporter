namespace MonoFusion.Exporter
{
	public class CCNFeeder
	{
		private string _filePath;
		private BinaryReader _reader;
		private Dictionary<ushort, long> _chunkPositions;

		public CCNFeeder(string ccnFilePath)
		{
			_filePath = ccnFilePath;
			_reader = new BinaryReader(File.Open(_filePath, FileMode.Open));
			_chunkPositions = [];
			LoadCCN();
		}

		/// <summary>
		/// This function assumes that the BinaryReader is a valid ccn
		/// </summary>
		public void LoadCCN()
		{
			_reader.BaseStream.Position = 16; // Skip header
			while (true)
			{
				long pos = _reader.BaseStream.Position;
				ushort chunkId = _reader.ReadUInt16();
				_reader.BaseStream.Position += 2; // Skip flags
				uint chunkSize = _reader.ReadUInt32();
				_reader.BaseStream.Position += chunkSize;

				_chunkPositions.TryAdd(chunkId, pos);
				if (chunkId == 0x7F7F) // Last Chunk
					break;
			}
		}

		public bool HasChunk(ushort chunkId)
		{
			return _chunkPositions.ContainsKey(chunkId);
		}

		public PartialStream GetChunkReader(ushort chunkId)
		{
			_reader.BaseStream.Position = _chunkPositions[chunkId];
			_reader.BaseStream.Position += 4; // Skip id and flags
			uint chunkSize = _reader.ReadUInt32();
			return new PartialStream(_reader.BaseStream, _reader.BaseStream.Position, chunkSize);
		}
	}
}
