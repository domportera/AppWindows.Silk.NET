// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using ImGuiNET;
using ImGuiWindows;
using Silk.NET.OpenGL;

namespace AppWindows.OpenGL;

public readonly record struct GLShaderInfo(uint Program, uint Vert, uint Frag);

/// <summary>
/// The Dear ImGui renderer for OpenGL, implemented as an <see cref="IImguiImplementation"/>.
/// </summary>
public class OpenGLImGuiController : IImguiImplementation
{
    private readonly int _attribLocationTex;
    private readonly int _attribLocationProjMtx;
    private readonly int _attribLocationVtxPos;
    private readonly int _attribLocationVtxUV;
    private readonly int _attribLocationVtxColor;
    private readonly uint _vboHandle;
    private readonly uint _elementsHandle;
    private uint _vertexArrayObject;

    private uint _fontTexture;
    private readonly GLShaderInfo _shader;
    private const bool _useGles = false;

    public OpenGLImGuiController(GLShaderInfo shaderInfo)
    {
        _shader = shaderInfo;
        
        // Get uniform locations
        _attribLocationTex = GL.GetUniformLocation(_shader.Program, "Texture");
        _attribLocationProjMtx = GL.GetUniformLocation(_shader.Program, "ProjMtx");
        
        // Get attribute locations
        _attribLocationVtxPos = GL.GetAttribLocation(_shader.Program, "Position");
        _attribLocationVtxUV = GL.GetAttribLocation(_shader.Program, "UV");
        _attribLocationVtxColor = GL.GetAttribLocation(_shader.Program, "Color");
        
        // Create buffers
        _vboHandle = GL.GenBuffer();
        _elementsHandle = GL.GenBuffer();
    }

    public void Init(ImGuiIOPtr io)
    {
        ImGuiLog.Debug($"[OPENGL] Init");
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
        _fontTexture = RecreateFontDeviceTexture(io);

        return;

        static unsafe uint RecreateFontDeviceTexture(ImGuiIOPtr io)
        {
            ImGuiLog.Debug("[OPENGL] Entering font creation");
            io.Fonts.GetTexDataAsRGBA32(
                out IntPtr pixels,
                out int width,
                out int height,
                out _);
            ImGuiLog.Debug($"[OPENGL] Creating font texture: size={width}x{height}");

            int lastTexture = 0;
            GL.GetError().Log();
            GL.GetInteger(GLEnum.TextureBinding2D, &lastTexture);
            GL.GetError().Log();

            var texId = GL.GenTexture();
            GL.GetError().Log();
            ImGuiLog.Debug($"[OPENGL] Font TexID = {(int)texId}");
            
            GL.BindTexture(GLEnum.Texture2D, texId);
            GL.GetError().Log();

            GL.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
            GL.GetError().Log();
            GL.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
            GL.GetError().Log();
            GL.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
            GL.GetError().Log();
            GL.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
            GL.GetError().Log();

            GL.PixelStore(GLEnum.UnpackRowLength, 0);
            GL.GetError().Log();
            GL.TexImage2D(
                GLEnum.Texture2D,
                0,
                (int)GLEnum.Rgba,
                (uint)width,
                (uint)height,
                0,
                GLEnum.Rgba,
                GLEnum.UnsignedByte,
                (void*)pixels);
            GL.GetError().Log();

            io.Fonts.SetTexID((nint)texId);

            GL.BindTexture(GLEnum.Texture2D, (uint)lastTexture);
            
            // Check for errors
            GL.GetError().Log();
            return texId;
        }
    }

    private unsafe void SetupRenderState(ImDrawDataPtr drawDataPtr, int framebufferWidth, int framebufferHeight)
    {
        GL.GetError().Log();
        GL.Viewport(0, 0, (uint)framebufferWidth, (uint)framebufferHeight);
        GL.GetError().Log();

        GL.Enable(GLEnum.Blend);
        GL.GetError().Log();
        GL.BlendEquation(GLEnum.FuncAdd);
        GL.GetError().Log();
        GL.BlendFuncSeparate(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha, GLEnum.One, GLEnum.OneMinusSrcAlpha);
        GL.GetError().Log();
        GL.Disable(GLEnum.CullFace);
        GL.GetError().Log();
        GL.Disable(GLEnum.DepthTest);
        GL.GetError().Log();
        GL.Disable(GLEnum.StencilTest);
        GL.GetError().Log();
        GL.Enable(GLEnum.ScissorTest);
        GL.GetError().Log();
        
        if (!_useGles)
        {
            GL.Disable(GLEnum.PrimitiveRestart);
            GL.PolygonMode(GLEnum.FrontAndBack, GLEnum.Fill);
        }
        
        var displayPos = drawDataPtr.DisplayPos;
        var displaySize = drawDataPtr.DisplaySize;
        var framebufferScale = drawDataPtr.FramebufferScale;

        if (displayPos.X < 0.0f || displayPos.Y < 0.0f || displaySize.X <= 0.0f || displaySize.Y <= 0.0f || framebufferScale.X <= 0.0f || framebufferScale.Y <= 0.0f)
        {
            ImGuiLog.Error($"Invalid values? " +
                           $"DisplayPos = {displayPos}, " +
                           $"DisplaySize = {displaySize}, " +
                           $"FramebufferScale = {drawDataPtr.FramebufferScale}," +
                           $"Provided framebuffer sizes = {framebufferWidth}x{framebufferHeight}");
        }

        var left = drawDataPtr.DisplayPos.X;
        var right = drawDataPtr.DisplayPos.X + drawDataPtr.DisplaySize.X;
        var top = drawDataPtr.DisplayPos.Y;
        var bottom = drawDataPtr.DisplayPos.Y + drawDataPtr.DisplaySize.Y;

        Span<float> orthoProjection =
        [
            2.0f / (right - left), 0.0f, 0.0f, 0.0f,
            0.0f, 2.0f / (top - bottom), 0.0f, 0.0f,
            0.0f, 0.0f, -1.0f, 0.0f,
            (right + left) / (left - right), (top + bottom) / (bottom - top), 0.0f, 1.0f
        ];

        GL.ActiveTexture(GLEnum.Texture0);
        GL.GetError().Log();
        GL.UseProgram(_shader.Program);
        GL.GetError().Log();
        GL.Uniform1(_attribLocationTex, 0);
        GL.GetError().Log();
        GL.UniformMatrix4(_attribLocationProjMtx, 1, false, orthoProjection);
        GL.GetError().Log();
        GL.BindSampler(0, 0);
        GL.GetError().Log();

        _vertexArrayObject = GL.GenVertexArray();
        GL.GetError().Log();
        GL.BindVertexArray(_vertexArrayObject);
        GL.GetError().Log();

        GL.BindBuffer(GLEnum.ArrayBuffer, _vboHandle);
        GL.GetError().Log();
        GL.BindBuffer(GLEnum.ElementArrayBuffer, _elementsHandle);
        GL.GetError().Log();

        GL.EnableVertexAttribArray((uint)_attribLocationVtxPos);
        GL.GetError().Log();
        GL.EnableVertexAttribArray((uint)_attribLocationVtxUV);
        GL.GetError().Log();
        GL.EnableVertexAttribArray((uint)_attribLocationVtxColor);
        GL.GetError().Log();

        GL.VertexAttribPointer((uint)_attribLocationVtxPos, 2, GLEnum.Float, false, (uint)sizeof(ImDrawVert), (void*)0);
        GL.GetError().Log();
        GL.VertexAttribPointer((uint)_attribLocationVtxUV, 2, GLEnum.Float, false, (uint)sizeof(ImDrawVert), (void*)8);
        GL.GetError().Log();
        GL.VertexAttribPointer((uint)_attribLocationVtxColor, 4, GLEnum.UnsignedByte, true, (uint)sizeof(ImDrawVert), (void*)16);
        GL.GetError().Log();
    }


    public unsafe void RenderImDrawData(ImDrawDataPtr drawDataPtr)
    {
        var frameBufferSize = drawDataPtr.DisplaySize * drawDataPtr.FramebufferScale;
        var framebufferWidth = (int)frameBufferSize.X;
        var framebufferHeight = (int)frameBufferSize.Y;
        if (framebufferWidth <= 0 || framebufferHeight <= 0)
            return;
        
        // Backup GL state
        int lastActiveTexture = 0,
            lastProgram = 0,
            lastTexture = 0,
            lastSampler = 0,
            lastArrayBuffer = 0,
            lastVertexArrayObject = 0,
            lastBlendSrcRgb = 0,
            lastBlendDstRgb = 0,
            lastBlendSrcAlpha = 0,
            lastBlendDstAlpha = 0,
            lastBlendEquationRgb = 0,
            lastBlendEquationAlpha = 0;

        GL.GetInteger(GLEnum.ActiveTexture, &lastActiveTexture);
        GL.GetError().Log();
        GL.ActiveTexture(GLEnum.Texture0);
        GL.GetError().Log();

        GL.GetInteger(GLEnum.CurrentProgram, &lastProgram);
        GL.GetError().Log();
        GL.GetInteger(GLEnum.TextureBinding2D, &lastTexture);
        GL.GetError().Log();

        GL.GetInteger(GLEnum.SamplerBinding, &lastSampler);
        GL.GetError().Log();

        GL.GetInteger(GLEnum.ArrayBufferBinding, &lastArrayBuffer);
        GL.GetError().Log();
        GL.GetInteger(GLEnum.VertexArrayBinding, &lastVertexArrayObject);
        GL.GetError().Log();

        Span<int> lastPolygonMode = stackalloc int[2];
        if (!_useGles)
        {
            GL.GetInteger(GLEnum.PolygonMode, lastPolygonMode);
            GL.GetError().Log();
        }

        Span<int> lastScissorBox = stackalloc int[4];
        GL.GetInteger(GLEnum.ScissorBox, lastScissorBox);
        GL.GetError().Log();

        GL.GetInteger(GLEnum.BlendSrcRgb, &lastBlendSrcRgb);
        GL.GetError().Log();
        GL.GetInteger(GLEnum.BlendDstRgb, &lastBlendDstRgb);
        GL.GetError().Log();

        GL.GetInteger(GLEnum.BlendSrcAlpha, &lastBlendSrcAlpha);
        GL.GetError().Log();
        GL.GetInteger(GLEnum.BlendDstAlpha, &lastBlendDstAlpha);
        GL.GetError().Log();

        GL.GetInteger(GLEnum.BlendEquationRgb, &lastBlendEquationRgb);
        GL.GetError().Log();
        GL.GetInteger(GLEnum.BlendEquationAlpha, &lastBlendEquationAlpha);
        GL.GetError().Log();

        bool lastEnableBlend = GL.IsEnabled(GLEnum.Blend);
        bool lastEnableCullFace = GL.IsEnabled(GLEnum.CullFace);
        bool lastEnableDepthTest = GL.IsEnabled(GLEnum.DepthTest);
        bool lastEnableStencilTest = GL.IsEnabled(GLEnum.StencilTest);
        bool lastEnableScissorTest = GL.IsEnabled(GLEnum.ScissorTest);

        bool lastEnablePrimitiveRestart = !_useGles ? GL.IsEnabled(GLEnum.PrimitiveRestart) : false;


        SetupRenderState(drawDataPtr, framebufferWidth, framebufferHeight);

        // Will project scissor/clipping rectangles into framebuffer space
        Vector2 clipOff = drawDataPtr.DisplayPos; // (0,0) unless using multi-viewports
        Vector2 clipScale = drawDataPtr.FramebufferScale; // (1,1) unless using retina display which are often (2,2)

        // Render command lists
        for (int n = 0; n < drawDataPtr.CmdListsCount; n++)
        {
            var cmdListPtr = drawDataPtr.CmdLists[n];

            // Upload vertex/index buffers

            GL.BufferData(GLEnum.ArrayBuffer, (nuint)(cmdListPtr.VtxBuffer.Size * sizeof(ImDrawVert)),
                (void*)cmdListPtr.VtxBuffer.Data, GLEnum.StreamDraw);
            
            GL.GetError().Log();


            GL.BufferData(GLEnum.ElementArrayBuffer, (nuint)(cmdListPtr.IdxBuffer.Size * sizeof(ushort)),
                (void*)cmdListPtr.IdxBuffer.Data, GLEnum.StreamDraw);
            GL.GetError().Log();

            for (int iCmd = 0; iCmd < cmdListPtr.CmdBuffer.Size; iCmd++)
            {
                ImDrawCmdPtr cmdPtr = cmdListPtr.CmdBuffer[iCmd];

                if (cmdPtr.UserCallback != IntPtr.Zero)
                {
                    throw new NotImplementedException();
                }
                else
                {
                    Vector4 clipRect;
                    clipRect.X = (cmdPtr.ClipRect.X - clipOff.X) * clipScale.X;
                    clipRect.Y = (cmdPtr.ClipRect.Y - clipOff.Y) * clipScale.Y;
                    clipRect.Z = (cmdPtr.ClipRect.Z - clipOff.X) * clipScale.X;
                    clipRect.W = (cmdPtr.ClipRect.W - clipOff.Y) * clipScale.Y;

                    if (clipRect.X < framebufferWidth && clipRect.Y < framebufferHeight && clipRect.Z >= 0.0f &&
                        clipRect.W >= 0.0f)
                    {
                        // Apply scissor/clipping rectangle
                        GL.Scissor((int)clipRect.X, (int)(framebufferHeight - clipRect.W),
                            (uint)(clipRect.Z - clipRect.X), (uint)(clipRect.W - clipRect.Y));
                        GL.GetError().Log();

                        // Bind texture, Draw
                        var textureId = (uint)cmdPtr.TextureId;
                        GL.BindTexture(GLEnum.Texture2D, textureId);
                        GL.GetError().Log();

                        GL.DrawElementsBaseVertex(GLEnum.Triangles, cmdPtr.ElemCount, GLEnum.UnsignedShort,
                            (void*)(cmdPtr.IdxOffset * sizeof(ushort)), (int)cmdPtr.VtxOffset);
                        GL.GetError().Log();
                    }
                }
            }
        }

        // Destroy the temporary VAO
        GL.DeleteVertexArray(_vertexArrayObject);
        GL.GetError().Log();
        _vertexArrayObject = 0;

        // Restore modified GL state
        GL.UseProgram((uint)lastProgram);
        GL.GetError().Log();
        GL.BindTexture(GLEnum.Texture2D, (uint)lastTexture);
        GL.GetError().Log();

        GL.BindSampler(0, (uint)lastSampler);
        GL.GetError().Log();

        GL.ActiveTexture((GLEnum)lastActiveTexture);
        GL.GetError().Log();

        GL.BindVertexArray((uint)lastVertexArrayObject);
        GL.GetError().Log();

        GL.BindBuffer(GLEnum.ArrayBuffer, (uint)lastArrayBuffer);
        GL.GetError().Log();
        GL.BlendEquationSeparate((GLEnum)lastBlendEquationRgb, (GLEnum)lastBlendEquationAlpha);
        GL.GetError().Log();
        GL.BlendFuncSeparate((GLEnum)lastBlendSrcRgb, (GLEnum)lastBlendDstRgb, (GLEnum)lastBlendSrcAlpha,
            (GLEnum)lastBlendDstAlpha);
        GL.GetError().Log();

        if (lastEnableBlend)
        {
            GL.Enable(GLEnum.Blend);
            GL.GetError().Log();
        }
        else
        {
            GL.Disable(GLEnum.Blend);
            GL.GetError().Log();
        }

        if (lastEnableCullFace)
        {
            GL.Enable(GLEnum.CullFace);
            GL.GetError().Log();
        }
        else
        {
            GL.Disable(GLEnum.CullFace);
            GL.GetError().Log();
        }

        if (lastEnableDepthTest)
        {
            GL.Enable(GLEnum.DepthTest);
            GL.GetError().Log();
        }
        else
        {
            GL.Disable(GLEnum.DepthTest);
            GL.GetError().Log();
        }

        if (lastEnableStencilTest)
        {
            GL.Enable(GLEnum.StencilTest);
            GL.GetError().Log();
        }
        else
        {
            GL.Disable(GLEnum.StencilTest);
            GL.GetError().Log();
        }

        if (lastEnableScissorTest)
        {
            GL.Enable(GLEnum.ScissorTest);
            GL.GetError().Log();
        }
        else
        {
            GL.Disable(GLEnum.ScissorTest);
            GL.GetError().Log();
        }


        if (!_useGles)
        {
            if (lastEnablePrimitiveRestart)
            {
                GL.Enable(GLEnum.PrimitiveRestart);
                GL.GetError().Log();
            }
            else
            {
                GL.Disable(GLEnum.PrimitiveRestart);
                GL.GetError().Log();
            }

            GL.PolygonMode(GLEnum.FrontAndBack, (GLEnum)lastPolygonMode[0]);
            GL.GetError().Log();
        }

        GL.Scissor(lastScissorBox[0], lastScissorBox[1], (uint)lastScissorBox[2], (uint)lastScissorBox[3]);
        GL.GetError().Log();
    }

    /// <summary>
    /// Frees all graphics resources used by the renderer.
    /// </summary>
    public void Dispose() => Dispose(true);
    private void Dispose(bool disposing)
    {
        GL.DeleteBuffer(_vboHandle);
        GL.DeleteBuffer(_elementsHandle);
        GL.DeleteVertexArray(_vertexArrayObject);
        GL.DeleteTexture(_fontTexture);


        if (disposing)
        {
            try
            {
                Disposed?.Invoke(this);
            }
            catch (Exception e)
            {
                ImGuiLog.Error(e.ToString());
            }
        }
        else
        {
            ImGuiLog.DisposalWarning();
        }

        Disposed = null;
    }
    
    ~OpenGLImGuiController()
    {
        Dispose(false);
    }

    public event Action<object>? Disposed;

}