using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MonoFusion.Exporter.Exporters
{
	public class MonoFusionBlazorGL : MonoFusionBase
	{
		public MonoFusionBlazorGL(string platformName, bool project) : base(platformName, "BlazorGL", project, "BlazorGL")
		{
		}

		public override string GetFileSelectorTitle()
		{
			if (_project)
				return base.GetFileSelectorTitle();
			return base.GetFileSelectorTitle() + " (Warning: File name will always be forced to index.html)";
		}

		public override string GetFileSelectorDefExt()
		{
			if (_project)
				return ".sln";
			return ".html";
		}

		public override string GetFileSelectorFilter()
		{
			if (_project)
				return "VS2026 Solution File|*.sln|All files|*.*||";
			return "Index|*.html";
		}

		public override bool Build(string targetFilePath, string ccnFilePath, uint buildFlags)
		{
			// Create solution
			if (!base.Build(targetFilePath, ccnFilePath, buildFlags))
				return false;

			string solutionDir = Path.Combine(Path.GetDirectoryName(ccnFilePath)!, "Solution");
			string tempTargetDir = Path.Combine(Path.GetDirectoryName(ccnFilePath)!, "Compile");
			string targetDir = Path.GetFullPath(Path.GetDirectoryName(targetFilePath)!);

			// Create Icon
			string iconPath = Path.Combine(Path.GetDirectoryName(ccnFilePath)!, "appicon.png");
			byte[] png = File.ReadAllBytes(iconPath);
			IcoWriter.WriteIco(png, Path.Combine(solutionDir, "wwwroot\\favicon.ico"));

			if (_project)
			{
				CopyDirectory(solutionDir, targetDir);
				return true;
			}

			using Process process = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = "dotnet",
					Arguments = "publish " +
								"-c Release " +
								"--nodereuse:false " +
								"/p:UseSharedCompilation=false " + // Prevent Steam from thinking the app is still running
                                "/p:RunAOTCompilation=true" +
							   $"/p:PublishDir=\"{tempTargetDir}\"",
					WorkingDirectory = solutionDir,
					UseShellExecute = false,
				}
			};

			// Ensure Clickteam doesn't cry it's eyes out
			IntPtr filter = Coregister.RetryMessageFilter.CreateInstance();
			_ = Coregister.CoRegisterMessageFilter(filter, out IntPtr oldFilter);

			process.Start();
			process.WaitForExit();

			_ = Coregister.CoRegisterMessageFilter(oldFilter, out _);
			Marshal.FreeHGlobal(filter);

			string finalPath = Path.Combine(tempTargetDir, "wwwroot");
			Console.WriteLine("Searching for " + finalPath);

			if (!Directory.Exists(finalPath))
			{
				Console.WriteLine("Failed! Pausing for debugging. Press <ENTER> to continue.");
				Console.ReadLine();
				return false;
			}

			Console.WriteLine("Success! Moving to " + targetDir);
			CopyDirectory(finalPath, targetDir);

			return process.ExitCode == 0;
        }

        public override string GetExtensionCompatName()
        {
            return "Web";
        }

        public override void RenameSolution(string solutionDir, string saveName, string mfaName)
        {
            // Rename the assembly
            RenameMGCBAssemblyGeneric(Path.Combine(solutionDir, "Content\\Content.mgcb"), saveName);
            ReplaceStrGeneric(Path.Combine(solutionDir, "Pages", "Index.razor"), "MonoFusion.Runtime", saveName);
            ReplaceStrGeneric(Path.Combine(solutionDir, "Pages", "Index.razor.cs"), "MonoFusion.Runtime", saveName);
            ReplaceStrGeneric(Path.Combine(solutionDir, "wwwroot", "index.html"), "MonoFusion.Runtime", saveName);
            ReplaceStrGeneric(Path.Combine(solutionDir, "_Imports.razor"), "MonoFusion.Runtime", saveName);
            ReplaceStrGeneric(Path.Combine(solutionDir, "Program.cs"), "MonoFusion.Runtime", saveName);
            ReplaceStrGeneric(Path.Combine(solutionDir, "MonoFusion.Runtime.csproj"), "MonoFusion.Runtime", saveName);
            RenameFileGeneric(Path.Combine(solutionDir, "MonoFusion.Runtime.csproj"), mfaName);
            ReplaceStrGeneric(Path.Combine(solutionDir, "MonoFusion.Runtime.sln"), "MonoFusion.Runtime", mfaName);
            RenameFileGeneric(Path.Combine(solutionDir, "MonoFusion.Runtime.sln"), mfaName);
        }
    }
}
