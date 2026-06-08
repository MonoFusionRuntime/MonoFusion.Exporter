using Microsoft.Win32;
using MonoFusion.Exporter.Exporters.Boiler;

namespace MonoFusion.Exporter
{
	public class FontBuilder
	{
		private readonly FusionFont _font;

		public FontBuilder(FusionFont fontData)
		{
			_font = fontData;
		}

		public string Build()
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
		}

		public string GetExtension()
		{
			return ".spritefont";
		}

		public bool IsBuildable()
		{
			string? path = GetFontFilePath(_font);

			if (path == null)
				return false;

			string ext = Path.GetExtension(path);

			return ext.Equals(".ttf", StringComparison.OrdinalIgnoreCase)
				|| ext.Equals(".otf", StringComparison.OrdinalIgnoreCase);
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

						string? file = key.GetValue(valueName) as string;

						if (string.IsNullOrEmpty(file))
							continue;

						string? fullPath = ResolveFontPath(hive, file);

						if (fullPath != null)
							return fullPath;
					}
				}
			}

			// Common GDI aliases
			if (faceName.Equals("System", StringComparison.OrdinalIgnoreCase))
			{
				string path = Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
					"vgasys.fon");

				if (File.Exists(path))
					return path;
			}

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
