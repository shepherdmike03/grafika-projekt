using System;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace MyLavaRunner
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var nativeSettings = new NativeWindowSettings()
            {
                Size       = new Vector2i(800, 600),
                Title      = "Skybox Test",
                APIVersion = new Version(3, 3),                 // force GL 3.3 core
                Flags      = ContextFlags.ForwardCompatible,                // forward‐compatible context
            };

            using var game = new Game(GameWindowSettings.Default, nativeSettings);
            game.Run();
        }
    }
}