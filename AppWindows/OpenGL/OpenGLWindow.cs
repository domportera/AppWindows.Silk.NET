using ImGuiWindows;
using Silk.NET.Core;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace AppWindows.OpenGL;

public sealed class OpenGLWindow : SurfaceRenderer
{
    protected override void OnInitialize(Surface surface)
    {
        if (surface.OpenGL is null)
        {
            throw new PlatformNotSupportedException("OpenGL is not supported.");
        }

        if (surface.OpenGL == null)
            throw new NullReferenceException("OpenGL is null");

        surface.OpenGL.Profile = OpenGLContextProfile.Core;
        surface.OpenGL.Version = new Version32(3, 2);
    }

    protected override void BeginFrame(SurfaceTimingEvent evt, int frameBufferWidth, int frameBufferHeight)
    {
        evt.Surface.MakeCurrent();
        GL.Viewport(0, 0, (uint)frameBufferWidth, (uint)frameBufferHeight);
        GL.GetError().Log();
        GL.Clear(ClearBufferMask.ColorBufferBit);
        GL.GetError().Log();
    }

    protected override void OnPause(SurfaceLifecycleEvent evt, bool isPaused)
    {
        
    }

    protected override void OnTerminate(SurfaceLifecycleEvent evt)
    {
        evt.Surface.MakeCurrent();
        GL.DeleteVertexArray(_vao);
        GL.GetError().Log();
        GL.DeleteBuffer(_vbo);
        GL.GetError().Log();
        GL.DeleteProgram(_prog);
        GL.GetError().Log();
        GL.DeleteShader(_vert);
        GL.GetError().Log();
        GL.DeleteShader(_frag);
        GL.GetError().Log();
        evt.Surface.OpenGL!.Dispose();
    }

    private uint _vbo, _vao, _prog, _vert, _frag;

    /// <summary>
    /// Create the ImGui implementation for OpenGL
    /// </summary>
    protected override IImguiImplementation Create(SurfaceLifecycleEvent evt)
    {
        ImGuiLog.Debug($"Creating {nameof(OpenGLImGuiController)}");
        evt.Surface.MakeCurrent();
        InitializeOpenGLRenderer(out _vao, out _vbo, out _prog, out _vert, out _frag);
        return new OpenGLImGuiController(new GLShaderInfo(_prog, _vert, _frag));
    }

    /// <summary>
    /// Initialize the general-purpose OpenGL renderer.
    /// </summary>
    /// <param name="vao"></param>
    /// <param name="vbo"></param>
    /// <param name="prog"></param>
    /// <param name="vert"></param>
    /// <param name="frag"></param>
    /// <exception cref="InvalidOperationException"></exception>
    private static void InitializeOpenGLRenderer(out uint vao, out uint vbo, out uint prog, out uint vert,
        out uint frag)
    {
        GL.GetError().Log();
        ImGuiLog.Debug("=== BEGIN OPENGL INFORMATION");
        foreach (var val in Enum.GetValues<Silk.NET.OpenGL.StringName>())
        {
            ImGuiLog.Debug($"{val} = {GL.GetString(val).ReadToString()}");
            GL.GetError().Log();
        }

        ImGuiLog.Debug("=== END OPENGL INFORMATION");

        vbo = GL.GenBuffer();
        GL.GetError().Log();
        GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.GetError().Log();

        float[] vertices = [-0.5f, -0.5f, 0.0f, 0.5f, -0.5f, 0.0f, 0.0f, 0.5f, 0.0f];
        GL.BufferData(BufferTarget.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)),
            vertices[0].AsRef(),
            BufferUsage.StaticDraw);
        GL.GetError().Log();
        vao = GL.GenVertexArray();
        GL.GetError().Log();
        GL.BindVertexArray(vao);
        GL.GetError().Log();
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), default);
        GL.GetError().Log();
        GL.EnableVertexAttribArray(0);
        GL.GetError().Log();
        vert = GL.CreateShader(ShaderType.VertexShader);
        GL.GetError().Log();
        frag = GL.CreateShader(ShaderType.FragmentShader);
        GL.GetError().Log();

        const string vertSource = """
                                  #version 150

                                  in vec2 Position;
                                  in vec2 UV;
                                  in vec4 Color;

                                  uniform mat4 ProjMtx;

                                  out vec2 Frag_UV;
                                  out vec4 Frag_Color;

                                  void main()
                                  {
                                      Frag_UV = UV;
                                      Frag_Color = Color;
                                      gl_Position = ProjMtx * vec4(Position.xy, 0.0, 1.0);
                                  }
                                  """;

        const string fragSource = """
                                  #version 150

                                  in vec2 Frag_UV;
                                  in vec4 Frag_Color;

                                  uniform sampler2D Texture;

                                  out vec4 Out_Color;

                                  void main()
                                  {
                                      Out_Color = Frag_Color * texture(Texture, Frag_UV.st);
                                  }
                                  """;

        var vertSourceLength = vertSource.Length;
        GL.ShaderSource(vert, 1, new[] { vertSource }, vertSourceLength.AsRef());
        GL.GetError().Log();
        var fragSourceLength = fragSource.Length;
        GL.ShaderSource(frag, 1, new[] { fragSource }, fragSourceLength.AsRef());
        GL.GetError().Log();

        GL.CompileShader(vert);
        GL.GetError().Log();
        var vertCompiled = 0;
        GL.GetShader(vert, ShaderParameterName.CompileStatus, vertCompiled.AsRef());
        GL.GetError().Log();
        if (vertCompiled == 0)
        {
            var logLen = 0u;
            GL.GetError().Log();
            var res = GL.GetShaderInfoLog(vert, logLen.AsRef());
            GL.GetError().Log();
            throw new InvalidOperationException($"Vertex shader compilation failed: {res} | {logLen}");
        }

        GL.CompileShader(frag);
        GL.GetError().Log();
        var fragCompiled = 0;
        GL.GetShader(frag, ShaderParameterName.CompileStatus, fragCompiled.AsRef());
        GL.GetError().Log();
        if (fragCompiled == 0)
        {
            var logLen = 0u;
            var res = GL.GetShaderInfoLog(vert, logLen.AsRef());
            GL.GetError().Log();
            throw new InvalidOperationException($"Fragment shader compilation failed: {res} | {logLen}");
        }

        prog = GL.CreateProgram();
        GL.AttachShader(prog, vert);
        GL.GetError().Log();
        GL.AttachShader(prog, frag);
        GL.GetError().Log();
        GL.LinkProgram(prog);
        GL.GetError().Log();

       // GL.DeleteShader(vert);
       // GL.DeleteShader(frag);
        GL.UseProgram(prog);
        GL.GetError().Log();
    }
}