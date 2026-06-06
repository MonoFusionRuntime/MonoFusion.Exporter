using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace MonoFusion.Exporter.Exporters
{
	public enum DesktopGLArch
	{
		Cancel,
		x64,
		x86,
		arm,
		arm64,
		Count
	}

	public class MonoFusionDesktopGL : MonoFusionBase
	{
		private const string NATIVEAOT_ENABLED = "<PublishAot>true</PublishAot>";
		private const string NATIVEAOT_DISABLED = "<PublishAot>false</PublishAot>";

		public MonoFusionDesktopGL(string platformName, bool project) : base(platformName, "DesktopGL", project, "DesktopGL")
		{
		}

		public override string GetFileSelectorDefExt()
		{
			if (_project)
				return ".sln";
			switch (_platformName)
			{
				case "Windows (OpenGL)":
					return ".exe";
				case "Mac (OpenGL)":
					return ".zip";
				case "Linux (OpenGL)":
					return "";
			}
			return ".ERROR";
		}

		public override string GetFileSelectorFilter()
		{
			if (_project)
				return "VS2026 Solution File|*.sln|All files|*.*||";
			switch (_platformName)
			{
				case "Windows (OpenGL)":
					return "Windows Executable|*.exe|All files|*.*||";
				case "Mac (OpenGL)":
					return "Archived Application Bundle|*.zip|All files|*.*||";
				case "Linux (OpenGL)":
					return "All files|*.*||";
			}
			return "ERROR UNKNOWN FORMAT|*.ERROR";
		}

		public override bool Build(string targetFilePath, string ccnFilePath, uint buildFlags)
		{
			// Ask for architecture
			DesktopGLArch arch = DesktopGLArch.Cancel;
			if (!_project)
			{
				arch = GetArch();
				if (arch <= DesktopGLArch.Cancel || arch >= DesktopGLArch.Count)
					return false;
			}

			// Create solution
			if (!base.Build(targetFilePath, ccnFilePath, buildFlags))
				return false;

			string solutionDir = Path.Combine(Path.GetDirectoryName(ccnFilePath)!, "Solution");
			string targetDir = Path.GetFullPath(Path.GetDirectoryName(targetFilePath)!);

			// Create Icon
			string iconPath = Path.Combine(Path.GetDirectoryName(ccnFilePath)!, "appicon.png");
			byte[] png = File.ReadAllBytes(iconPath);
			IcoWriter.WriteIco(png, Path.Combine(solutionDir, "Icon.ico"));
			CreateAppIconBitmap(iconPath, Path.Combine(solutionDir, "Icon.bmp"));

			if (_project)
			{
				CopyDirectory(solutionDir, targetDir);
				return true;
			}

			string cmdDotnet = "dotnet";
			string cmdPublish = "publish ";
			string cmdPrepend = "";
			string? wslTempDir = null;
			bool nativeAot = false; // Idk if I'll ever reimplement this

			if (_platformName == "Linux (OpenGL)")
			{
				// Use WSL, Allows NativeAOT
				if (IsWslInstalled() && TryFindWslDotnet(out string wslDotnetPath))
				{
					//Console.WriteLine(value: "Using WSL for NativeAOT");
					wslTempDir = WslCopyToTemp(solutionDir);
					cmdDotnet = "wsl";
					cmdPublish = $"bash -i -c \"cd {wslTempDir} && {wslDotnetPath} publish ";
					cmdPrepend = "\"";
					targetDir = ToWslPath(targetDir);
				}
				// Disable NativeAOT
				else
				{
					//Console.WriteLine("Disabling NativeAOT");
					//nativeAot = false;
					string csprojPath = Path.Combine(solutionDir, "MonoFusion.Runtime.csproj");
					string csprojData = File.ReadAllText(csprojPath);
					File.WriteAllText(csprojPath, csprojData.Replace(NATIVEAOT_ENABLED, NATIVEAOT_DISABLED));
				}
			}
			// Disable NativeAOT & Setup .app
			else if (_platformName == "Mac (OpenGL)")
			{
				//Console.WriteLine("Disabling NativeAOT");
				//nativeAot = false;
				string csprojPath = Path.Combine(solutionDir, "MonoFusion.Runtime.csproj");
				string csprojData = File.ReadAllText(csprojPath);
				File.WriteAllText(csprojPath, csprojData.Replace(NATIVEAOT_ENABLED, NATIVEAOT_DISABLED));

				string appPath = Path.Combine(Path.GetDirectoryName(ccnFilePath)!, "Compile", Path.ChangeExtension(Path.GetFileName(targetFilePath), ".app"));
				if (Directory.Exists(appPath))
					Directory.Delete(appPath, true);
				Directory.CreateDirectory(appPath);
				targetDir = Path.Combine(appPath, "Contents\\MacOS");

				CreatePList(appPath);
				CreateIcns(appPath, Path.GetDirectoryName(ccnFilePath)!);
			}

			using Process process = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = cmdDotnet,
					Arguments = cmdPublish +
								"-c Release " +
							   $"-r {GetDotnetPlatformName()}-{arch} " +
								"--self-contained true " +
							   $"/p:PublishSingleFile={(nativeAot ? "false" : "true")} " +
								"/p:PublishReadyToRun=false " +
								"/p:UseSharedCompilation=false " + // Prevent Steam from thinking the app is still running
							   $"/p:PublishDir=\"{targetDir}\" " +
								"-f net8.0" + cmdPrepend,
					WorkingDirectory = solutionDir,
					UseShellExecute = false,
				}
			};

			try
			{
				// Ensure Clickteam doesn't cry it's eyes out
				IntPtr filter = Coregister.RetryMessageFilter.CreateInstance();
				_ = Coregister.CoRegisterMessageFilter(filter, out IntPtr oldFilter);

				process.Start();
				process.WaitForExit();

				_ = Coregister.CoRegisterMessageFilter(oldFilter, out _);
				Marshal.FreeHGlobal(filter);

				if (_platformName == "Mac (OpenGL)")
				{
					string appName = Path.ChangeExtension(Path.GetFileName(targetFilePath), ".app");
					string zipPath = Path.Combine(Path.GetDirectoryName(ccnFilePath)!, "Compile");
					string appPath = Path.Combine(zipPath, appName);

					string contentPath = Path.Combine(targetDir, "Content");
					Console.WriteLine("Searching for " + contentPath);
					if (!Directory.Exists(contentPath))
					{
						Console.WriteLine("Failed! Pausing for debugging. Press <ENTER> to continue.");
						Console.ReadLine();
						return false;
					}

					string destPath = Path.Combine(appPath, "Contents\\Resources\\Content");
					Console.WriteLine("Success! Moving to " + destPath);
					Directory.Move(contentPath, destPath);

					ZipFile.CreateFromDirectory(zipPath, targetFilePath);
					SetUnixPermissions(targetFilePath, new Dictionary<string, int>
					{
						[$"{appName}/Contents/MacOS/MonoFusion.Runtime"] = 0b111_101_101 // rwxr-xr-x
					});
				}
			}
			finally
			{
				if (!string.IsNullOrEmpty(wslTempDir))
					WslCleanupTemp(wslTempDir);
			}

			return process.ExitCode == 0;
		}

		public DesktopGLArch GetArch()
		{
			TaskDialog.TASKDIALOG_BUTTON[] radios;
			if (_platformName == "Windows (OpenGL)")
			{
				radios =
				[
					new() { nButtonID = 100, pszButtonText = "x64" },
					new() { nButtonID = 101, pszButtonText = "x86" },
					new() { nButtonID = 102, pszButtonText = "ARM" },
					new() { nButtonID = 103, pszButtonText = "ARM64" },
				];
			}
			else if (_platformName == "Mac (OpenGL)")
			{
				radios =
				[
					new() { nButtonID = 100, pszButtonText = "x64" },
					new() { nButtonID = 103, pszButtonText = "ARM64" },
				];
			}
			else if (_platformName == "Linux (OpenGL)")
			{
				radios =
				[
					new() { nButtonID = 100, pszButtonText = "x64" },
					new() { nButtonID = 102, pszButtonText = "ARM" },
					new() { nButtonID = 103, pszButtonText = "ARM64" },
				];
			}
			else
			{
				Console.WriteLine("Unknown Platform");
				return DesktopGLArch.Cancel;
			}

			int buttonSize = Marshal.SizeOf<TaskDialog.TASKDIALOG_BUTTON>();
			IntPtr pRadios = Marshal.AllocHGlobal(buttonSize * radios.Length);
			try
			{
				for (int i = 0; i < radios.Length; i++)
					Marshal.StructureToPtr(radios[i], pRadios + i * buttonSize, false);

				var config = new TaskDialog.TASKDIALOGCONFIG
				{
					cbSize = (uint)Marshal.SizeOf<TaskDialog.TASKDIALOGCONFIG>(),
					pszWindowTitle = "Select Architecture",
					pszMainInstruction = "Select target architecture",
					pszContent = "Choose the architecture to publish for:",
					dwCommonButtons = TaskDialog.TDCBF_OK_BUTTON | TaskDialog.TDCBF_CANCEL_BUTTON,
					cRadioButtons = (uint)radios.Length,
					pRadioButtons = pRadios,
					nDefaultRadioButton = 100,
				};

				TaskDialog.TaskDialogIndirect(ref config, out int button, out int radioPressed, out _);

				if (button != TaskDialog.IDOK)
					return DesktopGLArch.Cancel;

				return radioPressed switch
				{
					100 => DesktopGLArch.x64,
					101 => DesktopGLArch.x86,
					102 => DesktopGLArch.arm,
					103 => DesktopGLArch.arm64,
					_ => DesktopGLArch.Cancel
				};
			}
			finally
			{
				Marshal.FreeHGlobal(pRadios);
			}
		}

		public string GetDotnetPlatformName()
		{
			return _platformName switch
			{
				"Windows (OpenGL)" => "win",
				"Mac (OpenGL)"     => "osx",
				"Linux (OpenGL)"   => "linux",
				_                  => "error",
			};
		}

		public void CreateAppIconBitmap(string inPath, string outPath)
		{
			var options = new DecoderOptions { SkipMetadata = true };
			using Image<Rgba32> image = Image.Load<Rgba32>(options, inPath);
			image.SaveAsBmp(outPath);
		}

		public override void RenameSolution(string solutionDir, string saveName, string mfaName)
		{
			// Rename the assembly
			RenameMGCBAssemblyGeneric(Path.Combine(solutionDir, "Content\\Content.mgcb"), saveName);
			ReplaceStrGeneric(Path.Combine(solutionDir, "app.manifest"), "MonoFusion.Runtime", saveName);
			ReplaceStrGeneric(Path.Combine(solutionDir, "rd.xml"), "MonoFusion.Runtime", saveName);
			ReplaceStrGeneric(Path.Combine(solutionDir, "MonoFusion.Runtime.csproj"), "MonoFusion.Runtime", saveName);
			RenameFileGeneric(Path.Combine(solutionDir, "MonoFusion.Runtime.csproj"), mfaName);
			ReplaceStrGeneric(Path.Combine(solutionDir, "MonoFusion.Runtime.slnx"), "MonoFusion.Runtime", mfaName);
			RenameFileGeneric(Path.Combine(solutionDir, "MonoFusion.Runtime.slnx"), mfaName);
		}

		#region LINUX WSL SYSTEMS
		static bool IsWslInstalled()
		{
			try
			{
				var process = new Process
				{
					StartInfo = new ProcessStartInfo
					{
						FileName = "wsl",
						Arguments = "--status",
						UseShellExecute = false,
						RedirectStandardOutput = true,
						RedirectStandardError = true,
						CreateNoWindow = true
					}
				};
				process.Start();
				process.WaitForExit();
				if (process.ExitCode != 0)
					Console.WriteLine("Failed to find wsl; Exit Code " + process.ExitCode);
				return process.ExitCode == 0;
			}
			catch (System.ComponentModel.Win32Exception e)
			{
				Console.WriteLine("Failed to find wsl; " + e.Message);
				return false;
			}
		}

		static bool TryFindWslDotnet(out string dotnetPath)
		{
			var process = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = "wsl",
					Arguments = "bash -i -c \"which dotnet\"",
					UseShellExecute = false,
					RedirectStandardOutput = true,
					CreateNoWindow = true
				}
			};
			process.Start();
			string path = process.StandardOutput.ReadToEnd().Trim();
			process.WaitForExit();
			dotnetPath = path;
			if (string.IsNullOrEmpty(path))
				Console.WriteLine("Failed to find wsl dotnet");
			return !string.IsNullOrEmpty(path);
		}

		static string ToWslPath(string windowsPath)
		{
			var process = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = "wsl",
					Arguments = $"wslpath \"{windowsPath.Replace('\\', '/')}\"",
					UseShellExecute = false,
					RedirectStandardOutput = true,
					CreateNoWindow = true
				}
			};
			process.Start();
			string path = process.StandardOutput.ReadToEnd().Trim();
			process.WaitForExit();
			return $"\"{path}\"";
		}

		static string WslCopyToTemp(string solutionDir)
		{
			string wslSolutionDir = ToWslPath(solutionDir);
			string wslTempDir = $"/tmp/monofusion_build_{Guid.NewGuid():N}";

			var process = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = "wsl",
					Arguments = $"cp -r {wslSolutionDir} {wslTempDir}",
					UseShellExecute = false
				}
			};

			Console.WriteLine($"Copying Solution into '{wslTempDir}', This may take a while.");
			process.Start();
			process.WaitForExit();
			Console.WriteLine($"Copied solution successfully, probably.");

			return wslTempDir;
		}

		static void WslCleanupTemp(string wslTempDir)
		{
			var process = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = "wsl",
					Arguments = $"rm -rf {wslTempDir}",
					UseShellExecute = false
				}
			};
			process.Start();
			process.WaitForExit();
		}
		#endregion

		#region MAC BUNDLE SYSTEMS
		public void CreatePList(string targetPath)
		{
			string path = Path.Combine(targetPath, "Contents");
			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);
			string plistPath = Path.Combine(targetPath, "Contents\\Info.plist");
			List<string> plist =
			[
				"<?xml version=\"1.0\" encoding=\"UTF-8\"?>",
				"<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">",
				"<plist version=\"1.0\">",
				"<dict>",
				"    <key>CFBundleExecutable</key>",
				"     <string>MonoFusion.Runtime</string>",
				"    <key>CFBundleIdentifier</key>",
				"    <string>com.yunivers.monofusionruntime</string>",
				"    <key>CFBundleName</key>",
				"    <string>MonoFusionRuntime</string>",
				"    <key>CFBundleIconFile</key>",
				"    <string>MonoFusionRuntime</string>",
				"    <key>CFBundleVersion</key>",
				"    <string>1.0</string>",
				"    <key>CFBundlePackageType</key>",
				"    <string>APPL</string>",
				"</dict>",
				"</plist>"
			];
			File.WriteAllLines(plistPath, plist);
		}

		void CreateIcns(string targetPath, string tempPath)
		{
			byte[] png = File.ReadAllBytes(Path.Combine(tempPath, "appicon.png"));
			int blockSize = png.Length + 8;

			string path = Path.Combine(targetPath, "Contents\\Resources");
			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);
			using var stream = File.Create(Path.Combine(path, "MonoFusionRuntime.icns"));
			using var writer = new BinaryWriter(stream);

			writer.Write(Encoding.ASCII.GetBytes("icns"));
			writer.Write(BinaryPrimitives.ReverseEndianness(8 + blockSize));
			writer.Write(Encoding.ASCII.GetBytes("ic07"));
			writer.Write(BinaryPrimitives.ReverseEndianness(blockSize)); 
			writer.Write(png);
		}

		void SetUnixPermissions(string zipPath, Dictionary<string, int> entryPermissions)
		{
			byte[] zipBytes = File.ReadAllBytes(zipPath);

			// Find central directory by scanning for signature 0x02014b50
			for (int i = 0; i < zipBytes.Length - 4; i++)
			{
				if (zipBytes[i] == 0x50 && zipBytes[i + 1] == 0x4B &&
					zipBytes[i + 2] == 0x01 && zipBytes[i + 3] == 0x02)
				{
					// "Version made by" is at offset +4, high byte = OS (3 = Unix)
					zipBytes[i + 5] = 0x03; // Unix

					// Get entry name length and extra field length
					int nameLength = zipBytes[i + 28] | (zipBytes[i + 29] << 8);
					string entryName = Encoding.UTF8.GetString(zipBytes, i + 46, nameLength);

					if (entryPermissions.TryGetValue(entryName, out int perms))
					{
						// External attributes at offset +38, Unix perms in high 16 bits
						int attr = perms << 16;
						zipBytes[i + 38] = (byte)(attr & 0xFF);
						zipBytes[i + 39] = (byte)((attr >> 8) & 0xFF);
						zipBytes[i + 40] = (byte)((attr >> 16) & 0xFF);
						zipBytes[i + 41] = (byte)((attr >> 24) & 0xFF);
					}
				}
			}

			File.WriteAllBytes(zipPath, zipBytes);
		}
		#endregion
	}
}
