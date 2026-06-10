using System.IO.Compression;
using System.Text;
using MonoFusion.Exporter.Exporters.Boiler;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace MonoFusion.Exporter.Exporters
{
	public class MonoFusionBase : FusionExporter
	{
		public const ushort CHUNK_MFAPATH    = 0x222E;
		public const ushort CHUNK_EXTENSIONS = 0x2234;
		public const ushort CHUNK_SHADERBANK = 0x2243;
		public const ushort CHUNK_NAMEBANK   = 0x6660;
		public const ushort CHUNK_IMAGEBANK  = 0x6666;
		public const ushort CHUNK_FONTBANK   = 0x6667;
		public const ushort CHUNK_SOUNDBANK  = 0x6668;
		public const ushort CHUNK_MUSICBANK  = 0x6669;

		public string MonoFusionPath = string.Empty;
		protected readonly string _platformName;
		protected readonly string _packageName;
		protected readonly bool _project;
		protected readonly string _mgcbPlatform;

		public MonoFusionBase(string platformName, string packageName, bool project, string mgcbPlatform)
		{
			_platformName = platformName;
			_packageName = packageName;
			_project = project;
			_mgcbPlatform = mgcbPlatform;
		}

		public override string GetBuildTypeName()
		{
			return $"MonoFusion {_platformName} {(_project ? "Project" : "Application")}";
		}

		public override BuildFlag GetBuildOptions()
		{
			//return uint.MaxValue - BuildFlag.CompressAssets;
			return BuildFlag.ExternalImages | BuildFlag.ExternalSounds | BuildFlag.ExternalMusic | BuildFlag.UpdatedFormat | BuildFlag.AllowChildEvents;
		}

		public override string GetFileSelectorTitle()
		{
			return "Save to folder (Empty folder recommended)";
		}

		public override string GetFileSelectorDefExt()
		{
			return ".GENERIC_ERROR";
		}

		public override string GetFileSelectorFilter()
		{
			return "ERROR GENERIC FORMAT|*.GENERIC_ERROR";
		}

		public override bool Build(string targetFilePath, string ccnFilePath, uint buildFlags)
		{
			if (!File.Exists(Path.Combine(MonoFusionPath, $"_Runtime{_packageName}.zip")))
			{
				Console.WriteLine("Failed to find runtime package " + _packageName);
				return false;
			}

			string sourceDir = Path.GetDirectoryName(ccnFilePath)!;
			Console.WriteLine("Source Directory: " + sourceDir);

			// Ignore target completely
			string targetDir = Path.Combine(sourceDir, "Solution");
			if (Directory.Exists(targetDir))
				Directory.Delete(targetDir, true);
			Directory.CreateDirectory(targetDir);

			CCNFeeder ccnFeeder = new CCNFeeder(ccnFilePath);

			string mfaPath = "ERROR";
			if (ccnFeeder.HasChunk(CHUNK_MFAPATH))
			{
				FusionMemoryReader chunkReader = new FusionMemoryReader(ccnFeeder.GetChunkReader(CHUNK_MFAPATH));
				mfaPath = chunkReader.ReadUnicodeString();
				Console.WriteLine($"Found mfa at '{mfaPath}'");
			}

			List<string> extensions = [];
			if (ccnFeeder.HasChunk(CHUNK_EXTENSIONS))
			{
				FusionMemoryReader chunkReader = new FusionMemoryReader(ccnFeeder.GetChunkReader(CHUNK_EXTENSIONS));
				ushort count = chunkReader.ReadUInt16();
				chunkReader.BaseStream.Position += 2; // Max Handle

				Console.WriteLine($"Adding extensions from '{MonoFusionPath}'");
				for (int i = 0; i < count; i++)
				{
					long pos = chunkReader.BaseStream.Position;
					ushort size = (ushort)Math.Abs(chunkReader.ReadInt16()); // Negative
					chunkReader.BaseStream.Position += 14; // Skip to Name
					string name = chunkReader.ReadUnicodeString();
					chunkReader.BaseStream.Position = pos + size; // Skip to next

					string zipFileName = Path.ChangeExtension(name, ".zip");
					if (File.Exists(Path.Combine(MonoFusionPath, zipFileName)))
					{
						extensions.Add(name);
						Console.WriteLine($"Found extension '{Path.GetFileNameWithoutExtension(name)}'");
					}
					else
						Console.WriteLine($"Could not find extension '{Path.GetFileNameWithoutExtension(name)}'");
				}
			}

			List<(string, string)> shaders = [];
			if (ccnFeeder.HasChunk(CHUNK_SHADERBANK))
			{
				FusionMemoryReader chunkReader = new FusionMemoryReader(ccnFeeder.GetChunkReader(CHUNK_SHADERBANK));
				uint count = chunkReader.ReadUInt32();
				uint[] offsets = new uint[count];
				for (int i = 0; i < count; i++)
					offsets[i] = chunkReader.ReadUInt32();

				Console.WriteLine($"Adding {count} shaders");
				for (int i = 0; i < count; i++)
				{
					chunkReader.BaseStream.Position = offsets[i];
					uint nameOffset = chunkReader.ReadUInt32();
					uint dataOffset = chunkReader.ReadUInt32();

					chunkReader.BaseStream.Position = offsets[i] + nameOffset;
					string name = chunkReader.ReadASCII();
					Console.WriteLine($"Found shader '{name}'");

					chunkReader.BaseStream.Position = offsets[i] + dataOffset;
					string data = chunkReader.ReadASCII();

					shaders.Add((name, data));
				}
			}

			Dictionary<uint, FusionFont> fonts = [];
			if (ccnFeeder.HasChunk(CHUNK_FONTBANK))
			{
				FusionMemoryReader chunkReader = new FusionMemoryReader(ccnFeeder.GetChunkReader(CHUNK_FONTBANK));
				uint count = chunkReader.ReadUInt32();

				Console.WriteLine($"Adding {count} fonts");
				for (int i = 0; i < count; i++)
				{
					uint handle = chunkReader.ReadUInt32();
					FusionMemoryReader dataReader = chunkReader.ReadCompressedData();
					dataReader.Skip(12); // Checksum, References, and Size

					FusionFont font = new FusionFont()
					{
						lfHeight = dataReader.ReadInt32(),
						lfWidth = dataReader.ReadInt32(),
						lfEscapement = dataReader.ReadInt32(),
						lfOrientation = dataReader.ReadInt32(),
						lfWeight = dataReader.ReadInt32(),
						lfItalic = dataReader.ReadByte() != 0,
						lfUnderline = dataReader.ReadByte() != 0,
						lfStrikeOut = dataReader.ReadByte() != 0,
						lfCharSet = dataReader.ReadByte(),
						lfOutPrecision = dataReader.ReadByte(),
						lfClipPrecision = dataReader.ReadByte(),
						lfQuality = dataReader.ReadByte(),
						lfPitchAndFamily = dataReader.ReadByte(),

						// No need to use fixed size (32) as we're inside the compressed data
						lfFaceName = dataReader.ReadUnicodeString()
					};

					Console.WriteLine($"Found font '{font.lfFaceName}'");
					fonts.Add(handle, font);
				}
			}

			// Read/write custom sound/music name chunk
			AddNameBankChunk(ccnFeeder, mfaPath);

			ZipFile.ExtractToDirectory(Path.Combine(MonoFusionPath, "_RuntimeBase.zip"), targetDir);
			ZipFile.ExtractToDirectory(Path.Combine(MonoFusionPath, $"_Runtime{_packageName}.zip"), targetDir, overwriteFiles: true);

			MGCBWriter writer = new MGCBWriter(_mgcbPlatform);

			string ccnPath_mgcb = "Application.ccx";
			File.Copy(ccnFilePath, Path.Combine(targetDir, "Content", ccnPath_mgcb));
			writer.AddBinary(ccnPath_mgcb);

			string imagePath = Path.Combine(sourceDir, "Images");
			List<Task> imageTasks = [];
			if (Path.Exists(imagePath) && Directory.EnumerateFiles(imagePath).Any())
			{
				Directory.CreateDirectory(Path.Combine(targetDir, "Content\\Images"));
				foreach (string file in Directory.GetFiles(imagePath))
				{
					string outPath_mgcb = Path.Combine("Images", "Img" + Path.GetFileName(file));
					string outPath = Path.Combine(targetDir, "Content", outPath_mgcb);
					writer.AddImage(outPath_mgcb);

					imageTasks.Add(Task.Factory.StartNew(() =>
					{
						PremultiplyAndCopy(file, outPath);
						//ValidateAndFixImageData(outPath);
					}));
				}
			}

			string soundPath = Path.Combine(sourceDir, "Sounds");
			if (Path.Exists(soundPath) && Directory.EnumerateFiles(soundPath).Any())
			{
				Directory.CreateDirectory(Path.Combine(targetDir, "Content\\Sounds"));
				foreach (string file in Directory.GetFiles(soundPath))
				{
					string outPath_mgcb = Path.Combine("Sounds", "Snd" + Path.GetFileName(file));
					string outPath = Path.Combine(targetDir, "Content", outPath_mgcb);
					File.Copy(file, outPath);
					writer.AddSound(outPath_mgcb);
				}
			}

			string musicPath = Path.Combine(sourceDir, "Music");
			if (Path.Exists(musicPath) && Directory.EnumerateFiles(musicPath).Any())
			{
				Directory.CreateDirectory(Path.Combine(targetDir, "Content\\Music"));
				foreach (string file in Directory.GetFiles(musicPath))
				{
					string outPath_mgcb = Path.Combine("Music", "Mus" + Path.GetFileName(file));
					string outPath = Path.Combine(targetDir, "Content", outPath_mgcb);
					File.Copy(file, outPath);
					writer.AddMusic(outPath_mgcb);
				}
			}

			if (fonts.Count > 0)
			{
				Directory.CreateDirectory(Path.Combine(targetDir, "Content\\Fonts"));
				foreach (uint handle in fonts.Keys)
				{
					FontBuilder fontBuilder = new FontBuilder(fonts[handle]);
					if (!fontBuilder.IsBuildable()) // Default to Arial
					{
						FusionFont arialFont = fonts[handle];
						arialFont.lfFaceName = "Arial";
						fontBuilder = new FontBuilder(arialFont);
					}

					string outPath_mgcb = Path.Combine("Fonts", "Fnt" + handle.ToString("D4") + fontBuilder.GetExtension());
					string outPath = Path.Combine(targetDir, "Content", outPath_mgcb);
					File.WriteAllText(outPath, fontBuilder.Build());
					writer.AddFont(outPath_mgcb);
				}
			}

			if (shaders.Count > 0)
			{
				Directory.CreateDirectory(Path.Combine(targetDir, "Content\\Effects"));
				foreach ((string name, string data) in shaders)
				{
					string outPath_mgcb = Path.Combine("Effects", name);
					string outPath = Path.Combine(targetDir, "Content", outPath_mgcb);
					File.WriteAllText(outPath, data);
					writer.AddEffect(outPath_mgcb);
				}
			}

			// Add default files
			writer.AddFont("Arial27.spritefont");
			writer.AddEffect("invert.fx");
			writer.AddEffect("mono.fx");

			writer.WriteTo(Path.Combine(targetDir, "Content\\Content.mgcb"));

			// Add extensions
			List<string> extensionLoaders = [];
			foreach (string extension in extensions)
			{
				string zipFilePath = Path.Combine(MonoFusionPath, Path.ChangeExtension(extension, ".zip"));
				ZipFile.ExtractToDirectory(zipFilePath, targetDir, true);
				extensionLoaders.AddRange(GenerateExtensionLoader(extension));
			}
			PushExtensionLoaders(Path.Combine(targetDir, "Runtime\\Extensions\\CExtLoad.cs"), extensionLoaders);

			RenameSolution(targetDir, Path.GetFileNameWithoutExtension(targetFilePath), Path.GetFileNameWithoutExtension(mfaPath));

			Task.WaitAll(imageTasks);
			return true;
		}

		private static void AddNameBankChunk(CCNFeeder ccnFeeder, string mfaPath)
		{
			BinaryReader mfaReader = new BinaryReader(File.OpenRead(mfaPath));
			mfaReader.BaseStream.Position = 20;
			for (int i = 0; i < 3; i++)
			{
				ushort skip = mfaReader.ReadUInt16();
				mfaReader.BaseStream.Position += skip * 2 + 2;
			}
			uint stampSize = mfaReader.ReadUInt32();
			mfaReader.BaseStream.Position += stampSize + 4; // 4 = ATNF
			uint fontCount = mfaReader.ReadUInt32();
			mfaReader.BaseStream.Position += fontCount * 108; // 108 = Font Size

			List<(uint, string)> soundNames = [];
			mfaReader.BaseStream.Position += 4; // 4 = APMS
			uint soundCount = mfaReader.ReadUInt32();
			for (int i = 0; i < soundCount; i++)
			{
				uint handle = mfaReader.ReadUInt32();
				mfaReader.BaseStream.Position += 8;
				uint size = mfaReader.ReadUInt32();
				mfaReader.BaseStream.Position += 8;
				int nameLength = mfaReader.ReadInt32();
				string name = Encoding.Unicode.GetString(mfaReader.ReadBytes(nameLength * 2)).TrimEnd('\0');
				mfaReader.BaseStream.Position += size - nameLength * 2;

				soundNames.Add((handle, name));
			}

			List<(uint, string)> musicNames = [];
			mfaReader.BaseStream.Position += 4; // 4 = APMS
			uint musicCount = mfaReader.ReadUInt32();
			for (int i = 0; i < musicCount; i++)
			{
				uint handle = mfaReader.ReadUInt32();
				mfaReader.BaseStream.Position += 8;
				uint size = mfaReader.ReadUInt32();
				mfaReader.BaseStream.Position += 8;
				int nameLength = mfaReader.ReadInt32();
				string name = Encoding.Unicode.GetString(mfaReader.ReadBytes(nameLength * 2)).TrimEnd('\0');
				mfaReader.BaseStream.Position += size;

				musicNames.Add((handle, name));
			}

			mfaReader.Close();

			BinaryWriter nameChunk = new BinaryWriter(new MemoryStream());
			nameChunk.Write(soundNames.Count);
			foreach ((uint handle, string name) in soundNames)
			{
				nameChunk.Write(handle);
				nameChunk.Write(Encoding.Unicode.GetBytes(name));
				nameChunk.Write((ushort)0); // NTB
			}
			nameChunk.Write(musicNames.Count);
			foreach ((uint handle, string name) in musicNames)
			{
				nameChunk.Write(handle);
				nameChunk.Write(Encoding.Unicode.GetBytes(name));
				nameChunk.Write((ushort)0); // NTB
			}

			ccnFeeder.InsertChunk(CHUNK_NAMEBANK, (MemoryStream)nameChunk.BaseStream, CHUNK_IMAGEBANK, CHUNK_FONTBANK, CHUNK_SOUNDBANK, CHUNK_MUSICBANK);
			ccnFeeder.Resave();
		}

		public override string[] GetSupportedExtensions()
		{
			List<string> extensions = [];
			Console.WriteLine($"Looking for supported extensions in '{MonoFusionPath = Path.Combine(Directory.GetCurrentDirectory(), "MonoFusion")}'");
			foreach (string file in Directory.GetFiles(MonoFusionPath)) // Data/Runtime is the working directory
			{
				string fileName = Path.GetFileName(file);
				if (Path.GetExtension(fileName) == ".zip" && !fileName.StartsWith("_Runtime"))
				{
					fileName = Path.ChangeExtension(fileName, ".mfx");
					Console.WriteLine($"Listed supported extension '{fileName}'");
					extensions.Add(fileName);
				}
			}
			return [.. extensions];
		}

		public override string GetEffectExt()
		{
			return ".fx";
		}

		public override string GetEffectBuildCommandLine()
		{
			return "Data\\Runtime\\MonoFusion\\MonoFusion.ShaderBuilder.exe \"<inputfile>\" \"<outputfile>\"";
		}

		public static List<string> GenerateExtensionLoader(string name)
		{
			string indent = new(' ', 16);
			List<string> loader = [];
			name = Path.GetFileNameWithoutExtension(name);
			loader.Add(indent + $"case \"{name}\":");
			indent += new string(' ', 4);
			loader.Add(indent + $"return new CRun{name}();");
			return loader;
		}

		public static void PushExtensionLoaders(string targetFilePath, List<string> loaders)
		{
			List<string> CExtLoad = File.ReadAllLines(targetFilePath).ToList();
			for (int i = 0; i < CExtLoad.Count; i++)
			{
				string curLine = CExtLoad[i];
				if (!curLine.Contains("MONOFUSION_EXTENSIONS_HERE"))
					continue;

				CExtLoad.RemoveAt(i);
				CExtLoad.InsertRange(i, loaders);
			}
			File.WriteAllLines(targetFilePath, CExtLoad);
		}

		public static void CopyDirectory(string sourceDir, string destinationDir, bool recursive = true)
		{
			var dir = new DirectoryInfo(sourceDir);
			if (!dir.Exists) throw new DirectoryNotFoundException("Source not found");

			Directory.CreateDirectory(destinationDir);

			// Copy files
			foreach (FileInfo file in dir.GetFiles())
				file.CopyTo(Path.Combine(destinationDir, file.Name), true);

			// Recursive copy subdirectories
			if (recursive)
				foreach (DirectoryInfo subDir in dir.GetDirectories())
					CopyDirectory(subDir.FullName, Path.Combine(destinationDir, subDir.Name), true);
		}

		private void ValidateAndFixImageData(string imgPath)
		{
			using FileStream fs = File.Open(imgPath, FileMode.Open);
			if (fs.Length >= 24)
			{
				fs.Seek(-20, SeekOrigin.End);
				byte[] last20 = new byte[20];
				fs.ReadExactly(last20, 0, 20);

				if (last20[0] == 'I' && last20[1] == 'D' && last20[2] == 'A' && last20[3] == 'T')
				{
					byte[] last8 = new byte[8];
					Array.Copy(last20, 12, last8, 0, 8);

					fs.Seek(-20, SeekOrigin.End);
					fs.Write(last8, 0, 8);
					fs.SetLength(fs.Length - 12);

					Console.WriteLine("Fixed invalid 0 length IDAT header for " + Path.GetFileNameWithoutExtension(imgPath));
				}
			}
		}

		private void PremultiplyAndCopy(string inPath, string outPath)
		{
			var options = new DecoderOptions { SkipMetadata = true };
			using Image<Rgba32> image = Image.Load<Rgba32>(options, inPath);

			image.ProcessPixelRows(accessor =>
			{
				for (int y = 0; y < accessor.Height; y++)
				{
					Span<Rgba32> row = accessor.GetRowSpan(y);
					for (int x = 0; x < row.Length; x++)
					{
						ref Rgba32 pixel = ref row[x];
						if (pixel.A == 0) continue;
						float a = pixel.A / 255f;
						pixel.R = (byte)(pixel.R * a);
						pixel.G = (byte)(pixel.G * a);
						pixel.B = (byte)(pixel.B * a);
					}
				}
			});

			image.SaveAsPng(outPath);
		}

		public virtual void RenameSolution(string solutionDir, string saveName, string mfaName)
		{
			throw new NotImplementedException($"{_platformName} does not implement RenameSolution");
		}

		public void RenameMGCBAssemblyGeneric(string filePath, string assemblyName)
		{
			ReplaceStrGeneric(filePath, "MONOFUSION_ASSEMBLY", assemblyName);
		}

		public void RenameFileGeneric(string filePath, string newName)
		{
			File.Move(filePath, Path.Combine(Path.GetDirectoryName(filePath)!, newName + Path.GetExtension(filePath)));
		}

		public void ReplaceStrGeneric(string filePath, string oldStr, string newStr)
		{
			string content = File.ReadAllText(filePath);
			content = content.Replace(oldStr, newStr);
			File.WriteAllText(filePath, content);
		}
	}
}

namespace MonoFusion.Exporter.Exporters.Boiler
{
	public static partial class ExporterHandler
	{
		static ExporterHandler()
		{
			AddExporterWindowsDX("Windows (DirectX)");
			AddExporterDesktopGL("Windows (OpenGL)");
			AddExporterUWP      ("Windows (UWP)");
			AddExporterDesktopGL("Mac (OpenGL)");
			AddExporterDesktopGL("Linux (OpenGL)");
			AddExporterAndroid  ("Android (OpenGL)");
			AddExporterUWP      ("Xbox One/Series (UWP)");
			AddExporterBlazorGL ("Web (BlazorGL)");
		}

		static void AddExporterWindowsDX(string platformName)
		{
			Exporters.Add(new MonoFusionWindowsDX(platformName, true));
			Exporters.Add(new MonoFusionWindowsDX(platformName, false));
		}

		static void AddExporterDesktopGL(string platformName)
		{
			Exporters.Add(new MonoFusionDesktopGL(platformName, true));
			Exporters.Add(new MonoFusionDesktopGL(platformName, false));
		}

		static void AddExporterUWP(string platformName)
		{
			Exporters.Add(new MonoFusionUWP(platformName, true));
			Exporters.Add(new MonoFusionUWP(platformName, false));
		}

		static void AddExporterAndroid(string platformName)
		{
			Exporters.Add(new MonoFusionAndroid(platformName, true));
			Exporters.Add(new MonoFusionAndroid(platformName, false));
		}

		static void AddExporterBlazorGL(string platformName)
		{
			Exporters.Add(new MonoFusionBlazorGL(platformName, true));
			Exporters.Add(new MonoFusionBlazorGL(platformName, false));
		}
	}
}