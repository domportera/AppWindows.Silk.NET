using ImGuiWindows;
using Silk.NET.Input.Glfw;
using Silk.NET.Input.Sdl;
using Silk.NET.Windowing;
using Silk.NET.Maths;
using Silk.NET.Windowing.Glfw;
using Silk.NET.Windowing.Sdl;
using SilkWindows.OpenGL;

namespace SilkWindows;

//https://github.com/dotnet/Silk.NET/blob/main/examples/CSharp/OpenGL%20Tutorials/Tutorial%201.1%20-%20Hello%20Window/Program.cs
public sealed class SilkWindowProvider : IImguiWindowProvider
{
    public SilkWindowProvider()
    {
    }

    public object RenderContextLock { get; } = new();

    // todo - implement updating font pack during window update
    public void SetFonts(FontPack fontPack) => FontPack = fontPack;

    // todo - multi-backend
    public IWindowImplementation CreateWindow(in WindowOptions options) => new GLWindow(options);

    public FontPack? FontPack { get; private set; }
}