using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MonoFusion.Exporter.Exporters
{
	public class MonoFusionAndroid : MonoFusionBase
	{
		public MonoFusionAndroid(string platformName, bool project) : base(platformName, "Android", project, "Android")
		{
		}

		public override string GetFileSelectorTitle()
		{
			if (_project)
				base.GetFileSelectorTitle();
			return "Save as Android Package";
		}

		public override string GetFileSelectorDefExt()
		{
			if (_project)
				return ".sln";
			return ".apk";
		}

		public override string GetFileSelectorFilter()
		{
			if (_project)
				return "VS2026 Solution File|*.sln|All files|*.*||";
			return "Android Package|*.apk|All files|*.*||";
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
			File.Copy(iconPath, Path.Combine(solutionDir, "Resources\\Drawable\\Icon.png"));

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
							   $"/p:PublishDir=\"{tempTargetDir}\" " +
								"-f net9.0-android",
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

			string finalPath = Path.Combine(tempTargetDir, "MonoFusion.Runtime.MonoFusion.Runtime-Signed.apk");
			Console.WriteLine("Searching for " + finalPath);

			if (!File.Exists(finalPath))
			{
				Console.WriteLine("Failed! Pausing for debugging. Press <ENTER> to continue.");
				Console.ReadLine();
				return false;
			}

			Console.WriteLine("Success! Moving to " + targetFilePath);
			File.Move(finalPath, targetFilePath);

			return process.ExitCode == 0;
		}
	}
}
