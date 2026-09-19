using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEditor.UnityLinker;

namespace VLiveKit.LiveLensFilters.Editor
{
    // Package link.xml files need to be supplied explicitly to UnityLinker.
    public sealed class LayerLightWrapLinkerProcessor : IUnityLinkerProcessor
    {
        const string LinkXmlGuid = "7e6ad3fe17a642d08ad0283d26194d00";

        public int callbackOrder => 0;

        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(LinkXmlGuid);
            if (string.IsNullOrEmpty(assetPath))
                throw new BuildFailedException("Layer Light Wrap's stripping preservation file is missing.");

            var package = PackageInfo.FindForAssetPath(assetPath);
            var path = package == null ? Path.GetFullPath(assetPath)
                : Path.Combine(package.resolvedPath, assetPath.Substring(package.assetPath.Length + 1));
            if (!File.Exists(path))
                throw new BuildFailedException("Layer Light Wrap's stripping preservation file was not found: " + path);
            return path;
        }
    }
}
