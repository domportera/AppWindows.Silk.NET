using System.Numerics;
using AppWindows.OpenGL;
using ImGuiNET;
using ImGuiWindows;
using Silk.NET.Windowing;

namespace Test;

class Program
{
    static void Main(string[] args)
    {
        ISurfaceApplication.Run<TestWindow>();
        ImGuiLog.EndScope();
        ImGuiLog.Shutdown();
    }
}

internal class TestWindow : ISurfaceApplication
{

    public static void Initialize<TSurface>(TSurface surface) where TSurface : Surface
    {
        _ = new OpenGLWindow
        {
            Surface = surface,
            Drawer = new TestDrawer()
        };
    }
}


internal class TestDrawer : IImguiDrawer
{
    private Vector4 _color = Vector4.One;
    public void Init()
    {
    }

    public void Draw(double deltaSeconds, ImFonts fonts, float dpiScale, out bool shouldTerminate)
    {
        shouldTerminate = false;
        //ImGui.Text("Hello, world!");
        ImGui.Text("hello mudder, hello fadder");
        ImGui.Text("hello puter, i am bothered");
        
        ImGui.ColorEdit4("Change me.....", ref _color);
        ImGui.Text($"Color: {_color:F1}");
        
        ImGui.ShowDemoWindow();
    }


    public void OnClose()
    {
    }

    public void OnFileDrop(IReadOnlyList<string> filePaths)
    {
    }

    public void OnWindowFocusChanged(bool changedTo)
    {
    }
}