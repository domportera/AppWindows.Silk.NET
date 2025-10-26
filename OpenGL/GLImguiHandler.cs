using System.Text;
using ImGuiWindows;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.SDL;
using Silk.NET.Windowing;

namespace SilkWindows;

public sealed class GLImguiHandler : IImguiImplementation
{
    public GLImguiHandler(string title, GL graphicsContext, IWindow window, IInputContext inputContext)
    {
        Title = title;
        _graphicsContext = graphicsContext;
        _window = window;
        _inputContext = inputContext;
    }

    public bool StartImguiFrame(float deltaSeconds)
    {
        try
        {
            _imguiController!.Update(deltaSeconds);
            return true;
        }
        catch (SdlException e)
        {
            var log = e.Message.Contains("HID") ? e.Message : e.ToString();
            Console.Error.WriteLine($"{nameof(SdlException)}: {log}");
            return false;
        }                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           
    }

    public void EndImguiFrame()
    {
        _imguiController!.Render();
    }

    public void Dispose()
    {
        _imguiController!.Dispose();
    }

    public string Title { get; }

    public IntPtr InitializeControllerContext(Action onConfigureIO)
    {
        _imguiController = new ImGuiController(gl: _graphicsContext, _window, _inputContext, null, onConfigureIO);
        return _imguiController.Context;
    }

    private ImGuiController? _imguiController;
    private readonly GL _graphicsContext;
    private readonly IWindow _window;
    private readonly IInputContext _inputContext;
}