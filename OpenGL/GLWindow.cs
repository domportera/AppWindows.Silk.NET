using System.Diagnostics.CodeAnalysis;
using ImGuiWindows;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Color = System.Drawing.Color;

namespace SilkWindows.OpenGL;

// ReSharper disable once InconsistentNaming
internal sealed class GLWindow : IWindowImplementation
{
    // how do we dynamically bind the imgui stuff?
    private GLWindow? ParentWindow { get; }
    public ISharedDisposable<IInputContext>? InputContext => ParentWindow?.InputContext ?? _inputContext;
    public ISharedDisposable<GL>? GLContext => ParentWindow?.GLContext ?? _graphicsContext!;
    
    public GLWindow(WindowOptions options, ImGuiWindow? parent, Action<NativeAPI>? renderContent = null)
    {
        ParentWindow = parent?.WindowImpl as GLWindow;
        
        var graphicsApi = options.API.API;
        if (graphicsApi != ContextAPI.OpenGL && graphicsApi != ContextAPI.OpenGLES)
        {
            Console.WriteLine("This handler is for OpenGL only");
            var api = GraphicsAPI.Default;
            options.API = api;
        }

        WindowOptions = options;
        _renderContent = renderContent;
    }

    public WindowOptions WindowOptions { get; }
    public Color DefaultClearColor => Color.Black;

    public bool Render(in Color clearColor, double deltaTime)
    {
        var actualCtx = _graphicsContext!.Context!;
        actualCtx.ClearColor(clearColor);
        actualCtx.Clear(ClearBufferMask.ColorBufferBit);
        try
        {
            _renderContent?.Invoke(actualCtx);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error during rendering: {ex.Message}");
        }

        return true;
    }

    public void EndRender()
    {
    }

    public void Dispose()
    {
        // dispose contexts
        if (_inputContext is IDisposable disposableInputContext)
        {
            disposableInputContext.Dispose();
        }

        if (_graphicsContext is IDisposable disposableGraphicsContext)
        {
            disposableGraphicsContext.Dispose();
        }
    }

    public void InitializeGraphicsAndInputContexts(IWindow window)
    {
        _window = window;
        _inputContext = ParentWindow?.InputContext ?? new InputSharedDisposable();
        _inputContext.Use(window.CreateInput);
        _graphicsContext = ParentWindow?.GLContext ?? new GlSharedDisposable();
        _graphicsContext.Use(window.CreateOpenGL);
    }

    public IImguiImplementation GetImguiImplementation()
    {
        return new GLImguiHandler(_window!.Title, _graphicsContext!.Context!, _window!, _inputContext!.Context!);
    }

    public void OnWindowResize(Vector2D<int> size)
    {
        _graphicsContext!.Context!.Viewport(size);
    }

    public void RenderWindowContents(double deltaTime)
    {
        throw new NotImplementedException();
    }

    private ISharedDisposable<IInputContext>? _inputContext;
    private ISharedDisposable<GL>? _graphicsContext;
    private IWindow? _window;
    private readonly Action<GL>? _renderContent;


    private class GlSharedDisposable : ISharedDisposable<GL>, INativeContext
    {
        public GL? Context { get; set; }
        public int UseCount { get; set; }
        public nint GetProcAddress(string proc, int? slot = null)
        {
            return Context.Context.GetProcAddress(proc, slot);
        }

        public bool TryGetProcAddress(string proc, [UnscopedRef] out nint addr, int? slot = null)
        {
            return Context.Context.TryGetProcAddress(proc, out addr, slot);
        }
    }

    private class InputSharedDisposable : ISharedDisposable<IInputContext>, IInputContext
    {
        public nint Handle => Context.Handle;

        public IReadOnlyList<IGamepad> Gamepads => Context.Gamepads;

        public IReadOnlyList<IJoystick> Joysticks => Context.Joysticks;

        public IReadOnlyList<IKeyboard> Keyboards => Context.Keyboards;

        public IReadOnlyList<IMouse> Mice => Context.Mice;

        public IReadOnlyList<IInputDevice> OtherDevices => Context.OtherDevices;

        public event Action<IInputDevice, bool>? ConnectionChanged
        {
            add => Context.ConnectionChanged += value;
            remove => Context.ConnectionChanged -= value;
        }

        public IInputContext? Context { get; set; }
        public int UseCount { get; set; }
    }
}