using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MonoFusion.Exporter.Exporters
{
	public class MonoFusionWindowsDX : MonoFusionBase
	{
		public enum WindowsDXArch
		{
			Cancel,
			x64,
			x86,
			arm,
			arm64,
			Count
		}

		public MonoFusionWindowsDX(string platformName, bool project) : base(platformName, "WindowsDX", project, "Windows")
		{
		}

		public override string GetFileSelectorDefExt()
		{
			if (_project)
				return ".sln";
			return ".exe";
		}

		public override string GetFileSelectorFilter()
		{
			if (_project)
				return "VS2026 Solution File|*.sln|All files|*.*||";
			return "Windows Executable|*.exe|All files|*.*||";
		}

		public override bool Build(string targetFilePath, string ccnFilePath, uint buildFlags)
		{
			// Ask for architecture
			WindowsDXArch arch = WindowsDXArch.Cancel;
			if (!_project)
			{
				arch = GetArch();
				if (arch <= WindowsDXArch.Cancel || arch >= WindowsDXArch.Count)
					return false;
			}

			// Create solution
			if (!base.Build(targetFilePath, ccnFilePath, buildFlags))
				return false;

			string solutionDir = Path.Combine(Path.GetDirectoryName(ccnFilePath)!, "Solution");
			string targetDir = Path.GetDirectoryName(targetFilePath)!;

			// Create Icon
			string iconPath = Path.Combine(Path.GetDirectoryName(ccnFilePath)!, "appicon.png");
			byte[] png = File.ReadAllBytes(iconPath);
			IcoWriter.WriteIco(png, Path.Combine(solutionDir, "Icon.ico"));

			if (_project)
			{
				CopyDirectory(solutionDir, targetDir);
				return true;
			}

			bool nativeAot = false; // Idk if I'll ever reimplement this

			using Process process = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = "dotnet",
					Arguments = "publish " +
								"-c Release " +
							   $"-r win-{arch} " +
								"--self-contained true " +
							   $"/p:PublishSingleFile={(nativeAot ? "false" : "true")} " +
								"/p:PublishReadyToRun=false " +
								"/p:UseSharedCompilation=false " + // Prevent Steam from thinking the app is still running
							   $"/p:PublishDir=\"{targetDir}\" " +
								"-f net8.0-windows",
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

			return process.ExitCode == 0;
		}

		public WindowsDXArch GetArch()
		{
			TaskDialog.TASKDIALOG_BUTTON[] radios =
			[
				new() { nButtonID = 100, pszButtonText = "x64" },
				new() { nButtonID = 101, pszButtonText = "x86" },
				new() { nButtonID = 102, pszButtonText = "ARM" },
				new() { nButtonID = 103, pszButtonText = "ARM64" },
			];

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
					return WindowsDXArch.Cancel;

				return radioPressed switch
				{
					100 => WindowsDXArch.x64,
					101 => WindowsDXArch.x86,
					102 => WindowsDXArch.arm,
					103 => WindowsDXArch.arm64,
					_ => WindowsDXArch.Cancel
				};
			}
			finally
			{
				Marshal.FreeHGlobal(pRadios);
			}
		}

		public override void RenameSolution(string solutionDir, string saveName, string mfaName)
		{
			// Rename the assembly
			RenameMGCBAssemblyGeneric(Path.Combine(solutionDir, "Content\\Content.mgcb"), saveName);
			ReplaceStrGeneric(Path.Combine(solutionDir, "app.manifest"), "MonoFusion.Runtime", saveName);
			ReplaceStrGeneric(Path.Combine(solutionDir, "MonoFusion.Runtime.csproj"), "MonoFusion.Runtime", saveName);
			RenameFileGeneric(Path.Combine(solutionDir, "MonoFusion.Runtime.csproj"), mfaName);
			ReplaceStrGeneric(Path.Combine(solutionDir, "MonoFusion.Runtime.slnx"), "MonoFusion.Runtime", mfaName);
			RenameFileGeneric(Path.Combine(solutionDir, "MonoFusion.Runtime.slnx"), mfaName);
		}
	}
}
