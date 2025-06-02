using ImGuiWindows;
using Silk.NET.GLFW;
using Silk.NET.Input.Glfw;
using Silk.NET.Windowing;
using Silk.NET.Maths;
using Silk.NET.Windowing.Glfw;
using SilkWindows.OpenGL;

namespace SilkWindows;

//https://github.com/dotnet/Silk.NET/blob/main/examples/CSharp/OpenGL%20Tutorials/Tutorial%201.1%20-%20Hello%20Window/Program.cs
public sealed class SilkWindowProvider : IImguiWindowProvider
{
    static SilkWindowProvider()
    {
        // Ensure that the instance is set at least once
        GlfwWindowing.RegisterPlatform();
        GlfwInput.RegisterPlatform();
    }

    public object ContextLock { get; } = new();
    
    public void SetFonts(FontPack fontPack)
    {
        _fontPack = fontPack;
    }

    public void Show(string title, IImguiDrawer drawer, bool autoSize = false, in SimpleWindowOptions? options = null)
    {
        var fullOptions = ConstructWindowOptions(options, title);
        var window = new GLWindow(fullOptions);
        
        var windowHelper = new WindowHelper(window, drawer, _fontPack, ContextLock, GetWindowScale, autoSize);
        windowHelper.RunUntilClosed();
    }

    private static unsafe float GetWindowScale(IWindow window)
    {
        var glfw = Glfw.GetApi();
        var monitor = window.Monitor;
        if (monitor == null)
        {
            return 1f; // Default DPI if no monitor is available
        }

        var monitorIdx = monitor.Index;
        var glfwMonitors = glfw.GetMonitors(out int count);
        if (monitorIdx >= count)
        {
            Console.WriteLine("Monitor index is out of bounds, cannot get monitor DPI.");
            return 1f;
        }

        var monitorPtr = glfwMonitors[monitorIdx];

        // Get the monitor's current scaling
        glfw.GetMonitorContentScale(monitorPtr, out var xscale, out var yscale);
        return 1f / Math.Max(xscale, yscale);
    }
    
    
    private static WindowOptions ConstructWindowOptions(in SimpleWindowOptions? options, string title)
    {
        var fullOptions = DefaultOptions;
        if (options.HasValue)
        {
            var val = options.Value;
            fullOptions.Size = val.Size.ToVector2DInt();
            fullOptions.FramesPerSecond = val.Fps;
            fullOptions.VSync = val.Vsync;
            fullOptions.WindowBorder = val.IsResizable ? WindowBorder.Resizable : WindowBorder.Fixed;
            fullOptions.TopMost = val.AlwaysOnTop;
        }
        
        fullOptions.Title = title;
        
        return fullOptions;
    }

    public FontPack? FontPack => _fontPack;
    private FontPack? _fontPack;
    
    private static readonly WindowOptions DefaultOptions = new()
                                                               {
                                                                   API = GraphicsAPI.Default,
                                                                   IsEventDriven = true,
                                                                   ShouldSwapAutomatically = true,
                                                                   IsVisible = true,
                                                                   Position = new Vector2D<int>(600, 600),
                                                                   Size = new Vector2D<int>(400, 320),
                                                                   FramesPerSecond = 60,
                                                                   UpdatesPerSecond = 60,
                                                                   PreferredDepthBufferBits = 0,
                                                                   PreferredStencilBufferBits = 0,
                                                                   PreferredBitDepth = new Vector4D<int>(8, 8, 8, 8),
                                                                   Samples = 0,
                                                                   VSync = true,
                                                                   TopMost = true,
                                                                   WindowBorder = WindowBorder.Resizable
                                                               };
    
}