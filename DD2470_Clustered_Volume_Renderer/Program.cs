using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace DD2470_Clustered_Volume_Renderer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // FIXME: Better way to do this?
            Directory.SetCurrentDirectory("../../../Assets");

            GameWindowSettings gws = new GameWindowSettings()
            {
                UpdateFrequency = 144,
            };

            NativeWindowSettings nws = new NativeWindowSettings()
            {
                API = ContextAPI.OpenGL,
                APIVersion = new Version(4, 6),
                Title = "Clustered Volume Renderer",
                Size = (1600, 900),
                Flags = ContextFlags.ForwardCompatible,
                Profile = ContextProfile.Core,
            };

            Window window = new Window(gws, nws);

            window.Run();
        }
    }
}