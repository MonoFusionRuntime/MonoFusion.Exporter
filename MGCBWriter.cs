using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

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
        private string _monoFusionPath;
        private List<(Type, string)> _content = [];
        private List<(string, JsonArray)> _extensions = [];
		private List<string> _references = [];

        public MGCBWriter(string platform, string monoFusionPath, bool compress = false)
		{
			_platform = platform;
            _monoFusionPath = monoFusionPath;
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
			_content.Add((Type.Sound, filePath));
			//_content.Add((Type.Binary, filePath));
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

		public void AddExtension(string extName, JsonArray content)
		{
			_extensions.Add((extName, content));
		}

		public void AddReference(string filePath)
		{
			_references.Add(filePath);
		}

		public void WriteTo(string targetPath)
		{
			Console.WriteLine($"Writing MGCB with {_content.Count} files, {_references.Count} references, and {_extensions.Count} extensions");

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
			];

			foreach (string reference in _references)
				output.Add(CreateParameter("reference", reference));

			output.AddRange(
			[
				"",
				CreateHeaderComment("Content"),
				"",
			]);

			foreach ((Type type, string path) in _content)
			{
				output.AddRange(CreateContentEntry(type, path));

				if (type == Type.Effect)
				{
					string ps = "ps_3_0";
					if (_platform == "Windows" || _platform == "WindowsUniversal")
						ps = "ps_4_0";
					string effectPath = Path.Combine(Path.GetDirectoryName(targetPath)!, path);
					string effectData = File.ReadAllText(effectPath);
					File.WriteAllText(effectPath, effectData.Replace("MONOFUSION_PS", ps));
				}
			}

			foreach ((string extName, JsonArray content) in _extensions)
            {
                Console.WriteLine($"Writing Extension '{extName}' to MGCB");
                output.AddRange(
                [
					CreateHeaderComment("Extension - " + extName),
					"",
				]);

                string contentPath = Path.Combine(_monoFusionPath, "Extensions", "Content", extName);
				string outPath = Path.Combine(Path.GetDirectoryName(targetPath)!, extName);
				if (!Directory.Exists(outPath))
					Directory.CreateDirectory(outPath);
				Console.WriteLine($"Created Directory '{outPath}'");

                foreach (JsonNode? j in content)
                {
					if (j == null)
                    {
                        Console.WriteLine("A Content entry was null!");
                        continue;
					}

					JsonNode? jName = j["name"];
					if (jName == null || jName.GetValueKind() != JsonValueKind.String)
                    {
                        Console.WriteLine("A Content entry's Name was null or invalid!");
                        continue;
                    }
					string name = jName.GetValue<string>();

                    JsonNode? jImporter = j["importer"];
                    if (jImporter == null || jImporter.GetValueKind() != JsonValueKind.String)
                    {
                        Console.WriteLine($"Content entry '{name}'s Importer was null or invalid!");
                        continue;
                    }
					string importer = jImporter.GetValue<string>();

                    JsonNode? jProcessor = j["processor"];
                    if (jProcessor == null || jProcessor.GetValueKind() != JsonValueKind.String)
                    {
                        Console.WriteLine($"Content entry '{name}'s Processor was null or invalid!");
                        continue;
                    }
					string processor = jProcessor.GetValue<string>();

                    // Copy file to Content folder
					string fullPath = Path.Combine(outPath, name);
					string shortPath = Path.Combine(extName, name);
                    File.Copy(
						Path.Combine(contentPath, name),
                        fullPath
                    );

                    // Preprocess Effects
                    if (importer == "EffectImporter" && processor == "EffectProcessor")
                    {
                        string ps = "ps_3_0";
                        if (_platform == "Windows" || _platform == "WindowsUniversal")
                            ps = "ps_4_0";
                        string effectData = File.ReadAllText(fullPath);
                        File.WriteAllText(fullPath, effectData.Replace("MONOFUSION_PS", ps));
                    }

                    output.Add(CreateContentHeader(shortPath));
                    output.Add(CreateParameter("importer", importer));
                    output.Add(CreateParameter("processor", processor));

					JsonNode? processorParams = j["processorParams"];
					if (processorParams != null && processorParams.GetValueKind() == JsonValueKind.Array)
					{
						foreach (JsonNode? jPp in processorParams.AsArray())
						{
							if (jPp == null)
								continue;

							JsonNode? jKey = jPp["key"];
							if (jKey == null || jKey.GetValueKind() != JsonValueKind.String)
								continue;
							string key = jKey.GetValue<string>();

							JsonNode? jValue = jPp["value"];
							if (jValue == null || jValue.GetValueKind() != JsonValueKind.String)
								continue;
							string value = jValue.GetValue<string>();

							output.Add(CreateProcessorParam(key, value));
						}
					}
					else
						Console.WriteLine("Could not write proccesor params for " + extName);

                    output.Add(CreateParameter("build", shortPath));
                    output.Add("");
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
