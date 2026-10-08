using System.Text.Json;
using System.Text.Json.Nodes;

namespace MonoFusion.Exporter
{
    public class VersionChecker
    {
        private const string MonoFusionMetaURL = @"https://ilikefemboys.com/MonoFusion/metadata.json";
        private const string MonoFusionVersion = "0.0.1";

        public static bool Check()
        {
            try
            {
                HttpClient httpClient = new HttpClient();
                string meta = httpClient.GetStringAsync(MonoFusionMetaURL).Result;
                JsonNode? metaJ = JsonNode.Parse(meta);
                if (metaJ != null && metaJ.GetValueKind() == JsonValueKind.Object)
                {
                    JsonNode? versJ = metaJ["version"];
                    if (versJ != null && versJ.GetValueKind() == JsonValueKind.String)
                    {
                        string vers = versJ.GetValue<string>();
                        if (vers == MonoFusionVersion)
                            return true;
                        AlertVersion(vers);
                        return false;
                    }
                }
                AlertVerify();
                return false;
            }
            catch
            {
                AlertVerify();
                return false;
            }
        }

        private static void AlertVersion(string expected)
        {
            TaskDialog.MessageBoxW(
                IntPtr.Zero,
                "MonoFusion is not publicly released, and your current version is out of date.\n" +
                "Expected v" + expected + ", got v" + MonoFusionVersion,
                "MonoFusion | Outdated Version",
                TaskDialog.MB_OK | TaskDialog.MB_ICONEXCLAMATION
            );
        }

        private static void AlertVerify()
        {
            TaskDialog.MessageBoxW(
                IntPtr.Zero,
                "MonoFusion is not publicly released, and your current version could not be verified.\n" +
                "Ensure you're connected to the internet, or if you're in the future, you're shit outta luck.",
                "MonoFusion | Could Not Verify",
                TaskDialog.MB_OK | TaskDialog.MB_ICONEXCLAMATION
            );
        }
    }
}
