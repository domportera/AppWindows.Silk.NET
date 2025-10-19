using ImGuiWindows;
using Silk.NET.Core.Native;
using Silk.NET.GLFW;
using Silk.NET.Input.Glfw;
using Silk.NET.Input.Sdl;
using Silk.NET.Windowing;
using Silk.NET.Maths;
using Silk.NET.SDL;
using Silk.NET.Windowing.Glfw;
using Silk.NET.Windowing.Sdl;
using SilkWindows.OpenGL;

namespace SilkWindows;

//https://github.com/dotnet/Silk.NET/blob/main/examples/CSharp/OpenGL%20Tutorials/Tutorial%201.1%20-%20Hello%20Window/Program.cs
public sealed class SilkWindowProvider : IImguiWindowProvider
{
    static SilkWindowProvider()
    {
        // Ensure that the instance is set at least once
        if (OperatingSystem.IsWindows())
        {
            GlfwWindowing.RegisterPlatform();
            GlfwInput.RegisterPlatform();
        }
        else
        {
            SdlWindowing.RegisterPlatform();
            SdlInput.RegisterPlatform();
        }
    }

    public object ContextLock { get; } = new();

    public void SetFonts(FontPack fontPack)
    {
        _fontPack = fontPack;
    }

    public void Show(string title, IImguiDrawer drawer, in SimpleWindowOptions? options = null)
    {
        var fullOptions = ConstructWindowOptions(options, title);
        var window = new GLWindow(fullOptions);

        var sizeFlags = options?.SizeFlags ?? DefaultSizeFlags;
        var windowHelper = new WindowHelper(window, drawer, _fontPack, ContextLock, GetWindowScale, sizeFlags);
        windowHelper.RunUntilClosed();
    }

    private static unsafe float GetWindowScale(IWindow window)
    {
        if (OperatingSystem.IsWindows())
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

        using var sdl = Sdl.GetApi();
        var sdlWindow = (Silk.NET.SDL.Window*)window.Handle;
        int displayIndex = sdl.GetWindowDisplayIndex(sdlWindow);

        float ddpi = 0, hdpi = 0, vdpi = 0;
        if (sdl.GetDisplayDPI(displayIndex, &ddpi, &hdpi, &vdpi) == 0)
        {
            // Standard DPI is usually 96
            return 96f / ddpi;
        }
        else
        {
            // Fallback if DPI can't be retrieved
            return 1f;
        } 
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
            fullOptions.WindowBorder = val.SizeFlags.HasFlag(WindowSizeFlags.ResizeWindow)
                ? WindowBorder.Resizable
                : WindowBorder.Fixed;
            fullOptions.TopMost = val.AlwaysOnTop;
            fullOptions.IsEventDriven = val.RenderLoopWaitsForEvents;
        }

        fullOptions.Title = title;

        return fullOptions;
    }

    public FontPack? FontPack => _fontPack;
    private FontPack? _fontPack;

    private static readonly WindowSizeFlags DefaultSizeFlags = WindowSizeFlags.ResizeWindow | WindowSizeFlags.ResizeGui;

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
        TopMost = false,
        WindowBorder = WindowBorder.Resizable
    };
}