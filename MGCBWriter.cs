namespace MonoFusion.Exporter
{
	public class MGCBWriter
	{
		private const int HEADER_LENGTH = 76;
		private enum Type
		{
			Binary,
			Font,
			Image,
			Sound,
			Music,
			Effect
		}

		private bool _compress = false;
		private string _platform = "Windows";
		private List<(Type, string)> _content = [];

		public MGCBWriter(string platform, bool compress = false)
		{
			_platform = platform;
			_compress = compress;
		}

		public void AddBinary(string filePath)
		{
			_content.Add((Type.Binary, filePath));
		}

		public void AddFont(string filePath)
		{
			_content.Add((Type.Font, filePath));
		}

		public void AddImage(string filePath)
		{
			//_content.Add((Type.Image, filePath));
			_content.Add((Type.Binary, filePath));
		}

		public void AddSound(string filePath)
		{
			//_content.Add((Type.Sound, filePath));
			_content.Add((Type.Binary, filePath));
		}

		public void AddMusic(string filePath)
		{
			//_content.Add((Type.Music, filePath));
			_content.Add((Type.Binary, filePath));
		}

		public void AddEffect(string filePath)
		{
			_content.Add((Type.Effect, filePath));
		}

		public void WriteTo(string targetPath)
		{
			List<string> output =
			[
				"",
				CreateHeaderComment("Global Properties"),
				"",
				CreateParameter(name: "outputDir", "bin/$(Platform)"),
				CreateParameter("intermediateDir", "obj/$(Platform)"),
				CreateParameter("platform", _platform),
				CreateParameter("config", ""),
				CreateParameter("profile", "Reach"),
				CreateParameter("compress", _compress.ToString()),
				"",
				CreateHeaderComment("References"),
				"",
				CreateParameter("reference", "../MonoFusion.BinaryImporter.dll"),
				"",
				CreateHeaderComment("Content"),
				"",
			];
			foreach ((Type, string) content in _content)
			{
				output.AddRange(CreateContentEntry(content.Item1, content.Item2));

				if (content.Item1 == Type.Effect)
				{
					string ps = "ps_3_0";
					if (_platform == "Windows" || _platform == "WindowsUniversal")
						ps = "ps_4_0";
					string effectPath = Path.Combine(Path.GetDirectoryName(targetPath)!, content.Item2);
					string effectData = File.ReadAllText(effectPath);
					File.WriteAllText(effectPath, effectData.Replace("MONOFUSION_PS", ps));
				}
			}
			File.WriteAllLines(targetPath, output);
		}

		private string CreateHeaderComment(string str)
		{
			str = $" {str} ";
			float strLen = str.Length / 2.0f;
			int leftLen = (int)Math.Ceiling(HEADER_LENGTH / 2 - strLen);
			int rightLen = (int)Math.Floor(HEADER_LENGTH / 2 - strLen);
			string left = new('-', leftLen);
			string right = new('-', rightLen);
			return $"#{left}{str}{right}#";
		}

		private string CreateParameter(string name, string value)
		{
			return $"/{name}:{value}";
		}

		private string CreateProcessorParam(string name, string value)
		{
			return CreateParameter("processorParam", $"{name}={value}");
		}

		private string CreateContentHeader(string filePath)
		{
			return $"#begin {filePath}";
		}

		private List<string> CreateContentEntry(Type type, string filePath)
		{
			List<string> output = [CreateContentHeader(filePath)];
			string ext = Path.GetExtension(filePath);
			switch (type)
			{
				case Type.Binary:
					output.Add(CreateParameter("importer", "BinaryImporter"));
					output.Add(CreateParameter("processor", "BinaryProcessor"));
					output.Add(CreateProcessorParam("RuntimeAssembly", "MONOFUSION_ASSEMBLY"));
					break;
				case Type.Font:
					output.Add(CreateParameter("importer", "FontDescriptionImporter"));
					output.Add(CreateParameter("processor", "FontDescriptionProcessor"));
					output.Add(CreateProcessorParam("PremultiplyAlpha", "True"));
					output.Add(CreateProcessorParam("TextureFormat", "Compressed"));
					break;
				case Type.Image:
					output.Add(CreateParameter("importer", "TextureImporter"));
					output.Add(CreateParameter("processor", "TextureProcessor"));
					output.Add(CreateProcessorParam("ColorKeyColor", "255,0,255,255"));
					output.Add(CreateProcessorParam("ColorKeyEnabled", "True"));
					output.Add(CreateProcessorParam("GenerateMipmaps", "False"));
					output.Add(CreateProcessorParam("PremultiplyAlpha", "True"));
					output.Add(CreateProcessorParam("ResizeToPowerOfTwo", "False"));
					output.Add(CreateProcessorParam("MakeSquare", "False"));
					output.Add(CreateProcessorParam("TextureFormat", "Color"));
					break;
				case Type.Sound:
					if (ext == ".mp3")
						output.Add(CreateParameter("importer", "Mp3Importer"));
					else if (ext == ".ogg")
						output.Add(CreateParameter("importer", "OggImporter"));
					else // Default to .wav
						output.Add(CreateParameter("importer", "WavImporter"));
					output.Add(CreateParameter("processor", "SoundEffectProcessor"));
					output.Add(CreateProcessorParam("Quality", "Best"));
					break;
				case Type.Music:
					// TODO
					break;
				case Type.Effect:
					output.Add(CreateParameter("importer", "EffectImporter"));
					output.Add(CreateParameter("processor", "EffectProcessor"));
					output.Add(CreateProcessorParam("DebugMode", "Auto"));
					break;
			}
			output.Add(CreateParameter("build", filePath));
			output.Add("");
			return output;
		}
	}
}
