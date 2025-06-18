using System.IO;
using System.Reflection;
using System.Drawing;
using System.Runtime.Versioning;

namespace CoreAPI.Core.Helpers;

public static class ResourceLoader
{
    [SupportedOSPlatform("windows")]
    public static Image? LoadImageFromAssembly(string resourceName, string assemblyName)
    {
        Assembly lab2Assembly = Assembly.Load(assemblyName);
        
        // Getting a stream with a resource
        using (Stream? stream = lab2Assembly.GetManifestResourceStream(resourceName))
        {
            return stream != null ? Image.FromStream(stream) : null;
        }
    }
}