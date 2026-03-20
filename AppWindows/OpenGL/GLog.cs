using System.Diagnostics;
using System.Runtime.CompilerServices;
using ImGuiWindows;
using Silk.NET.Core;
using Silk.NET.OpenGL;

namespace AppWindows.OpenGL;

internal static class GLog
{
    [DebuggerHidden, StackTraceHidden]
    public static bool Log(this Constant<uint, GLEnum, ErrorCode> err, [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0, [CallerMemberName] string? member = null)
    {
        //var err = GL.GetError();
        if (err != GLEnum.NoError)
        {
            ImGuiLog.Error($"OpenGL error: {(GLEnum)err} ({(uint)err})", path, line, member);
            return true;
        }

        return false;
    }
}