namespace MonoFusion.Exporter.Exporters
{
	public class MonoFusionUWP : MonoFusionBase
	{
		public MonoFusionUWP(string platformName, bool project) : base(platformName, "UWP", project, "WindowsUniversal")
		{
		}

		public override string GetFileSelectorDefExt()
		{
			if (_project)
				return ".sln";
			switch (_platformName)
			{
				case "Windows (UWP)":
					return ".msixbundle";
				case "Xbox One/Series (UWP)":
					return ".msix";
			}
			return ".ERROR";
		}

		public override string GetFileSelectorFilter()
		{
			if (_project)
				return "VS2017 Solution File|*.sln|All files|*.*||";
			switch (_platformName)
			{
				case "Windows (UWP)":
					return "App Package Bundle|*.msixbundle|All files|*.*||";
				case "Xbox One/Series (UWP)":
					return "MSIX Package|*.msix|All files|*.*||";
			}
			return "ERROR UNKNOWN FORMAT|*.ERROR";
		}
	}
}
