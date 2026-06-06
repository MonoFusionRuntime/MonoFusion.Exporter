namespace MonoFusion.Exporter
{
	public class PartialStream : Stream
	{
		private readonly Stream _base;
		private readonly long _start;
		private readonly long _length;
		private long _position;

		public PartialStream(Stream baseStream, long start, long length)
		{
			_base = baseStream;
			_start = start;
			_length = length;
			_position = 0;
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (_position >= _length)
				return 0;

			count = (int)Math.Min(count, _length - _position);

			_base.Position = _start + _position;

			int read = _base.Read(buffer, offset, count);

			_position += read;

			return read;
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			long newPos = origin switch
			{
				SeekOrigin.Begin => offset,
				SeekOrigin.Current => _position + offset,
				SeekOrigin.End => _length + offset,
				_ => throw new ArgumentOutOfRangeException()
			};

			if (newPos < 0 || newPos > _length)
				throw new IOException();

			_position = newPos;
			return _position;
		}

		public override long Length => _length;

		public override long Position
		{
			get => _position;
			set => Seek(value, SeekOrigin.Begin);
		}

		public override bool CanRead => true;
		public override bool CanSeek => true;
		public override bool CanWrite => false;

		public override void Flush() { }

		public override void SetLength(long value) =>
			throw new NotSupportedException();

		public override void Write(byte[] buffer, int offset, int count) =>
			throw new NotSupportedException();
	}
}
