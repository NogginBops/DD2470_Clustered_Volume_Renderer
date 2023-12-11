using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AssetProcessor
{
    internal class DllResolver
    {
        static DllResolver()
        {
            NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), DllImportResolver);
        }

        /// <summary>
        /// Called to trigger the static constructor.
        /// </summary>
        public static void InitLoader() { }

        public static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (NameToDll.TryGetValue(libraryName, out string? dllName))
            {
                string libPath = Path.Combine("runtimes", RuntimeInformation.RuntimeIdentifier, "native", dllName);
                if (NativeLibrary.TryLoad(libPath, assembly, searchPath, out nint handle))
                {
                    return handle;
                }
                else
                {
                    throw new PlatformNotSupportedException($"Could not find native library at: '{libPath}'");
                }
            }
            else
            {
                return NativeLibrary.Load(libraryName, assembly, searchPath);
            }
        }

        private static readonly Dictionary<string, string> NameToDll = new Dictionary<string, string>()
        {
            ["bc7enc"] = "bc7enc.dll",
            ["meshoptimizer"] = "meshoptimizer.dll",
        };
    }
}
