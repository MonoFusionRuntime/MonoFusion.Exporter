using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace MonoFusion.Exporter.Exporters.Boiler
{
	public abstract class FusionExporter
	{
		// Required exports
		public abstract string GetBuildTypeName();
		public abstract BuildFlag GetBuildOptions();
		public abstract string GetFileSelectorFilter();
		public abstract string GetFileSelectorTitle();
		public abstract string GetFileSelectorDefExt();

		public abstract bool Build(string targetFilePath, string ccnFilePath, uint buildFlags);

		// Optional exports

		/// <summary>
		/// This should return a relative file path to an executable.<br/>
		/// The path MUST be relative to <c>mmf2u.exe</c>, absolute file paths will not work.<br/>
		/// <br/>
		/// The executable will be passed in three parameters:<br/>
		/// <list type="bullet">
		/// <item>
		/// <c>/X</c><br/>
		/// Usage is currently unknown
		/// </item>
		/// <item>
		/// <c>/FCCNFilePath</c><br/>
		/// The file path to the temporary ccn prepended by <c>/F</c>
		/// </item>
		/// <item>
		/// <c>/PRunCom...</c><br/>
		/// RunCom is a parameter prepended by <c>/P</c> and appended by a timestamp
		/// </item>
		/// </list>
		/// </summary>
		public virtual string GetEditorRuntimeName()
		{
			return "Data\\Runtime\\Unicode\\edrt.exe"; // Default: edrt
		}

		/// <summary>
		/// This should return an array of strings that are supported by this exporter.<br/>
		/// They should be formatted the exact same as the extension filenames, including the extension.<br/>
		/// <br/>
		/// For example; <c>kcini.mfx</c> for the Ini extension
		/// </summary>
		public virtual string[] GetSupportedExtensions()
		{
			return []; // Default: Empty;
		}

		/// <summary>
		///	This should return the file format of the shader to search for.<br/>
		///	The shader file should be adjacent to the shader's xml in your Clickteam Effects folder.<br/>
		///	<br/>
		/// For example, if you return <c>myfx</c> or <c>.myfx</c>, in the Clickteam Effects folder<br/>
		/// there should be both <c>ShaderName.xml</c> and <c>ShaderName.myfx</c> adjacent to each other.
		/// </summary>
		public virtual string GetEffectExt()
		{
			return ""; // Default: Empty
		}

		/// <summary>
		/// This should return the command line to run for each effect.<br/>
		/// The format should be <c>my/path/to.exe &quot;&lt;inputfile&gt;&quot; &quot;&lt;outputfile&gt;&quot;</c>
		/// <br/><br/>
		/// You MUST have &lt;inputfile&gt; and &lt;outputfile&gt; in the string
		/// </summary>
		public virtual string GetEffectBuildCommandLine()
		{
			return ""; // Default: Empty
		}

		/// <summary>
		/// If you return anything that is not 0,
		/// this value will replace the PAMU/PAME header in your ccn.
		/// </summary>
		public virtual int GetRuntimeType()
		{
			return 0; // Default: 0
		}

		public virtual int GetMosaicSize()
		{
			return 2048; // Default: 2048
		}

		/// <summary>
		/// Due to a bug in the engine, this and BuildFont can never be called
		/// </summary>
		public virtual bool SupportsBuildFont()
		{
			return false; // Default: false
		}

		/// <summary>
		/// Due to a bug in the engine, this and SupportsBuildFont can never be called
		/// </summary>
		public virtual void BuildFont(FusionFont font, UIntPtr unknown, string filePath)
		{
			
		}
	}

	public enum BuildFlag : uint
	{
		None             = 0x000,
		CompressAssets   = 0x001,
		ExternalImages   = 0x002,
		ExternalSounds   = 0x004,
		ExternalMusic    = 0x008,
		UpdatedFormat    = 0x020,
		UseMosaics       = 0x040,
		AllowChildEvents = 0x100, // Requires UpdatedFormat
	}

	public static partial class ExporterHandler
	{
		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool AllocConsole();

		[UnmanagedCallersOnly(EntryPoint = "GetNumberOfBuildTypes", CallConvs = [typeof(CallConvStdcall)])]
		public static int GetNumberOfBuildTypes()
		{
			AllocConsole();
			return Exporters.Count;
		}

		[UnmanagedCallersOnly(EntryPoint = "GetBuildTypeName", CallConvs = [typeof(CallConvStdcall)])]
		public static unsafe char* GetBuildTypeName(int index)
		{
			string str = Exporters[index].GetBuildTypeName();
			return AllocWString(str);
		}

		[UnmanagedCallersOnly(EntryPoint = "GetBuildType", CallConvs = [typeof(CallConvStdcall)])]
		public static uint GetBuildType(int index)
		{
			uint id = BitConverter.ToUInt32(Encoding.ASCII.GetBytes(ExtBaseID));
			return 0x10000000U + id + (uint)index;
		}

		[UnmanagedCallersOnly(EntryPoint = "GetBuildOptions", CallConvs = [typeof(CallConvStdcall)])]
		public static int GetBuildOptions(int index)
		{
			return (int)Exporters[index].GetBuildOptions();
		}

		[UnmanagedCallersOnly(EntryPoint = "GetFileSelectorFilter", CallConvs = [typeof(CallConvStdcall)])]
		public static unsafe char* GetFileSelectorFilter(int index)
		{
			return AllocWString(Exporters[index].GetFileSelectorFilter());
		}

		[UnmanagedCallersOnly(EntryPoint = "GetFileSelectorTitle", CallConvs = [typeof(CallConvStdcall)])]
		public static unsafe char* GetFileSelectorTitle(int index)
		{
			return AllocWString(Exporters[index].GetFileSelectorTitle());
		}

		[UnmanagedCallersOnly(EntryPoint = "GetFileSelectorDefExt", CallConvs = [typeof(CallConvStdcall)])]
		public static unsafe char* GetFileSelectorDefExt(int index)
		{
			return AllocWString(Exporters[index].GetFileSelectorDefExt());
		}

		[UnmanagedCallersOnly(EntryPoint = "GetEditorRuntimeName", CallConvs = [typeof(CallConvStdcall)])]
		public static unsafe char* GetEditorRuntimeName(int index)
		{
			string? str = Exporters[index].GetEditorRuntimeName();
			if (str == null) return null;
			return AllocWString(str);
		}

		[UnmanagedCallersOnly(EntryPoint = "GetSupportedExtensions", CallConvs = [typeof(CallConvStdcall)])]
		public static unsafe char** GetSupportedExtensions(int index)
		{
			string[]? exts = Exporters[index].GetSupportedExtensions();

			if (exts == null || exts.Length == 0)
			{
				char** empty = (char**)NativeMemory.Alloc((nuint)sizeof(char*));
				empty[0] = null;
				return empty;
			}

			char** arr = (char**)NativeMemory.Alloc((nuint)((exts.Length + 1) * sizeof(char*)));
			for (int i = 0; i < exts.Length; i++)
				arr[i] = AllocWString(exts[i]);
			arr[exts.Length] = null;
			return arr;
		}

		[UnmanagedCallersOnly(EntryPoint = "GetEffectExt", CallConvs = [typeof(CallConvStdcall)])]
		public static unsafe char* GetEffectExt(int index)
		{
			string? str = Exporters[index].GetEffectExt();
			if (str == null) return null;
			return AllocWString(str);
		}

		[UnmanagedCallersOnly(EntryPoint = "GetEffectBuildCommandLine", CallConvs = [typeof(CallConvStdcall)])]
		public static unsafe char* GetEffectBuildCommandLine(int index)
		{
			string? str = Exporters[index].GetEffectBuildCommandLine();
			if (str == null) return null;
			return AllocWString(str);
		}

		[UnmanagedCallersOnly(EntryPoint = "GetRuntimeType", CallConvs = [typeof(CallConvStdcall)])]
		public static int GetRuntimeType(int index)
		{
			return Exporters[index].GetRuntimeType();
		}

		[UnmanagedCallersOnly(EntryPoint = "GetMosaicSize", CallConvs = [typeof(CallConvStdcall)])]
		public static int GetMosaicSize(int index)
		{
			return Exporters[index].GetMosaicSize();
		}

		[UnmanagedCallersOnly(EntryPoint = "SupportsBuildFont", CallConvs = [typeof(CallConvStdcall)])]
		public static int SupportsBuildFont(int index)
		{
			return Exporters[index].SupportsBuildFont() ? 1 : 0;
		}

		[UnmanagedCallersOnly(EntryPoint = "BuildFont", CallConvs = [typeof(CallConvStdcall)])]
		public static unsafe void BuildFont(int index, FusionFontNative* font, nuint unknown, nint filePath)
		{
			string filePathS = Marshal.PtrToStringUni(filePath)!;
			Exporters[index].BuildFont(FusionFont.FromNative(font), unknown, filePathS);
		}

		[UnmanagedCallersOnly(EntryPoint = "Build", CallConvs = [typeof(CallConvStdcall)])]
		public static int Build(nint outPath, nint ccnPath, int index, uint buildFlags)
		{
			string outPathS = Marshal.PtrToStringUni(outPath)!;
			string ccnPathS = Marshal.PtrToStringUni(ccnPath)!;
			bool ret = Exporters[index].Build(outPathS, ccnPathS, buildFlags);

			return ret ? 1 : 0;
		}

		// Allocates a null-terminated wchar_t* copy of a C# string in unmanaged memory.
		// The C++ side owns this memory — if it never frees it you may want a
		// per-load arena or a CoTaskMemAlloc/CoTaskMemFree pair instead.
		private static unsafe char* AllocWString(string str)
		{
			int byteCount = (str.Length + 1) * sizeof(char);
			char* buf = (char*)NativeMemory.Alloc((nuint)byteCount);
			fixed (char* src = str)
				Unsafe.CopyBlockUnaligned(buf, src, (uint)(str.Length * sizeof(char)));
			buf[str.Length] = '\0';
			return buf;
		}
	}
}
