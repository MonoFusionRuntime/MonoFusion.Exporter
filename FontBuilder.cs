using Microsoft.Win32;
using MonoFusion.Exporter.Exporters.Boiler;
using System.Buffers.Binary;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace MonoFusion.Exporter
{
	public class FontBuilder
	{
		private readonly FusionFont _font;

		public FontBuilder(FusionFont fontData)
		{
			_font = fontData;
		}

		/*public string Build()
		{
			return BuildSpriteFont();
		}

		private string BuildSpriteFont()
		{
			List<string> data =
			[
			   @"<?xml version=""1.0"" encoding=""utf-8""?>",
			   @"<XnaContent xmlns:Graphics=""Microsoft.Xna.Framework.Content.Pipeline.Graphics"">",
			   @"  <Asset Type=""Graphics:FontDescription"">",
			  $@"    <FontName>{_font.lfFaceName}</FontName>",
			  $@"    <Size>{Math.Abs(_font.lfHeight) * 72.0f / 96.0f}</Size>",
			   @"    <Spacing>0</Spacing>",
			   @"    <UseKerning>true</UseKerning>",
			  $@"    <Style>{(
						_font.lfWeight >= 700 && _font.lfItalic ? "Bold, Italic" : 
						_font.lfWeight >= 700 ? "Bold" : 
						_font.lfItalic ? "Italic" : "Regular"
				     )}</Style>",
			   @"    <CharacterRegions>",
			   @"      <CharacterRegion>",
			   @"        <Start>&#32;</Start>",
			   @"        <End>&#255;</End>",
			   @"      </CharacterRegion>",
			   @"    </CharacterRegions>",
			   @"  </Asset>",
			   @"</XnaContent>"
			];
			return string.Join('\n', data);
		}*/

		public string GetFileName()
        {
            string? path = GetFontFilePath();

            if (path == null)
                return string.Empty;

            return Path.GetFileName(path);
        }

		public string GetTypefaceName()
        {
            return _font.lfFaceName;
        }

		public string GetExtension()
        {
            string? path = GetFontFilePath();

            if (path == null)
                return string.Empty;

            return Path.GetExtension(path);
            //return ".spritefont";
        }

		public bool IsBuildable()
		{
			string ext = GetExtension();
			return ext.Equals(".ttf", StringComparison.OrdinalIgnoreCase)
				|| ext.Equals(".otf", StringComparison.OrdinalIgnoreCase);
		}

		public string GetFontFilePath()
		{
			return GetFontFilePath(_font)!;
		}

		private static string? GetFontFilePath(FusionFont logFont)
		{
			string? faceName = logFont.lfFaceName?.Trim('\0', ' ');

			if (string.IsNullOrEmpty(faceName))
				return null;

			string[] registryPaths =
			{
				@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts",
				@"SOFTWARE\WOW6432Node\Microsoft\Windows NT\CurrentVersion\Fonts"
			};

			RegistryKey[] hives =
			{
				Registry.LocalMachine,
				Registry.CurrentUser
			};

			bool italic = logFont.lfItalic;
			bool underscore = logFont.lfUnderline;
			bool strikeout = logFont.lfStrikeOut;

            Dictionary<int, string> weights = [];
            Dictionary<int, string> fallback = [];
            foreach (RegistryKey hive in hives)
			{
				foreach (string registryPath in registryPaths)
				{
					using RegistryKey? key = hive.OpenSubKey(registryPath);

					if (key == null)
						continue;

					foreach (string valueName in key.GetValueNames())
					{
						if (!valueName.StartsWith(faceName, StringComparison.OrdinalIgnoreCase))
							continue;

						// Only look at this font if it's a TrueType font
						if (!valueName.Contains("(TrueType)"))
							continue;

						string? file = key.GetValue(valueName) as string;

						if (string.IsNullOrEmpty(file))
							continue;

                        // Ignore TrueType Collections
                        if (Path.GetExtension(file) == ".ttc")
                            continue;

                        string? fullPath = ResolveFontPath(hive, file);

						if (fullPath != null && File.Exists(fullPath))
						{
							using BinaryReader reader = new BinaryReader(File.OpenRead(fullPath));
							reader.BaseStream.Position = 4; // sfntVersion
							ushort numTables = BinaryPrimitives.ReadUInt16BigEndian(reader.ReadBytes(2));
							reader.BaseStream.Position = 12; // searchRange, entrySelector, rangeShift
							for (int i = 0; i < numTables; i++)
							{
								string tableTag = Encoding.ASCII.GetString(reader.ReadBytes(4));
								if (tableTag == "OS/2")
								{
                                    reader.BaseStream.Position += 4; // checksum
                                    reader.BaseStream.Position = BinaryPrimitives.ReadUInt32BigEndian(reader.ReadBytes(4)); // offset

                                    reader.BaseStream.Position += 4; // version, xAvgCharWidth
                                    ushort usWeightClass = BinaryPrimitives.ReadUInt16BigEndian(reader.ReadBytes(2));
                                    reader.BaseStream.Position += 56; // usWidthClass, fsType, ySubscriptXSize, ySubscriptYSize
                                                                      // ySubscriptXOffset, ySubscriptYOffset, ySuperscriptXSize, ySuperscriptYSize
                                                                      // ySuperscriptXOffset, ySuperscriptYOffset, yStrikeoutSize, yStrikeoutPosition
                                                                      // sFamilyClass, panose, ulUnicodeRange[4], achVendID[4]
                                    ushort fsSelection = BinaryPrimitives.ReadUInt16BigEndian(reader.ReadBytes(2));

									bool isItalic = (fsSelection & 0x1) != 0;
									bool isUnderscore = (fsSelection & 0x2) != 0;
									bool isStrikeout = (fsSelection & 0x10) != 0;
									if (isItalic == italic && isUnderscore == underscore && isStrikeout && strikeout)
                                    {
                                        if (usWeightClass == logFont.lfWeight)
                                            return fullPath;
										weights.Add(usWeightClass, fullPath);
                                    }
									else
                                        fallback.Add(usWeightClass, fullPath);

                                    break;
								}
                                reader.BaseStream.Position += 12; // checksum, offset, length
                            }
						}
					}
				}
			}

			if (weights.Count == 0)
				weights = fallback;

			List<int> weightKeys = [.. weights.Keys];
			weightKeys.Sort();
			for (int i = weights.Count - 1; i >= 0; i--)
				if (weightKeys[i] <= logFont.lfWeight)
					return weights[weightKeys[i]];

			// Common GDI aliases
			/*if (faceName.Equals("System", StringComparison.OrdinalIgnoreCase))
			{
				string path = Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
					"vgasys.fon");

				if (File.Exists(path))
					return path;
			}*/

			return null;
		}

		private static string? ResolveFontPath(RegistryKey hive, string value)
		{
			if (Path.IsPathRooted(value))
				return File.Exists(value) ? value : null;

			string windowsFonts = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.Windows),
				"Fonts",
				value);

			if (File.Exists(windowsFonts))
				return windowsFonts;

			if (hive.Name.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase))
			{
				string userFonts = Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
					"Microsoft",
					"Windows",
					"Fonts",
					value);

				if (File.Exists(userFonts))
					return userFonts;
			}

			return null;
		}
	}
}
