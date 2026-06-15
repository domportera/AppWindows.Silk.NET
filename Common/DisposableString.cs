using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using JetBrains.Annotations;

namespace Common;

public static class DisposableStringExtensions
{
    [MustDisposeResource]
    public static DisposableString ToDisposableString(this StringBuilder sb, bool clear)
    {
        var s = DisposableString.Create(sb);
        if (clear)
        {
            sb.Clear();
        }

        return s;
    }
    
    [MustDisposeResource, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DisposableString ToDisposableString(this StringBuilder sb) => DisposableString.Create(sb);

    [MustDisposeResource, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DisposableString ToDisposableString<T>(this T value, string? format = null,
        int maxNumericChars = 32)
        where T : ISpanFormattable
    {
        return DisposableString.Create(value, format, maxNumericChars);
    }

    [MustDisposeResource, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DisposableString ToDisposableString(this Vector2 value, string? format = null,
        int maxNumericChars = DisposableString.DefaultCharMaxCount)
    {
        using var x = value.X.ToDisposableString(format, maxNumericChars);
        using var y = value.Y.ToDisposableString(format, maxNumericChars);
        return "(" + x + "," + y + ")";
    }
}

// todo - make this a MutableString?
public ref struct DisposableString : IDisposable
{
    public const int DefaultCharMaxCount = 64;
    private const int MinimumInitialStringBuilderCapacity = 128;
    private static readonly ArrayPool<char> _charArrayPool = ArrayPool<char>.Create();
    [ThreadStatic] private static StringBuilder? _sb;

    private readonly int _length;
    private readonly char[] _array;
    public bool IsDisposed { get; private set; }

    public static implicit operator ReadOnlySpan<char>(DisposableString s) => new(s._array, 0, s._length);

    private DisposableString(char[] array, int length)
    {
        _array = array;
        _length = length;
    }

    [MustDisposeResource]
    internal static DisposableString Create(StringBuilder value)
    {
        var length = value.Length;
        var arr = _charArrayPool.Rent(length);
        value.CopyTo(0, arr, 0, length);
        return new DisposableString(arr, length);
    }

    [MustDisposeResource, MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static DisposableString CreateAndClear(StringBuilder value)
    {
        var val = Create(value);
        value.Clear();
        return val;
    }

    [MustDisposeResource]
    internal static DisposableString Create<T>(T value, string? format = null,
        int maxNumericChars = DefaultCharMaxCount, IFormatProvider? formatProvider = null)
        where T : ISpanFormattable
    {
        var formatSpan = format is null ? ReadOnlySpan<char>.Empty : format.AsSpan();
        var length = formatSpan.Length + maxNumericChars;
        var array = _charArrayPool.Rent(length);
        if (value.TryFormat(destination: array.AsSpan(), out var strLen, formatSpan, formatProvider))
        {
            return new DisposableString(array, strLen);
        }

        _charArrayPool.Return(array);

        // try upping the char count if that would be useful
        if (formatSpan.IsEmpty && formatProvider is null && length == strLen)
        {
            var newLen = Math.Max(maxNumericChars + DefaultCharMaxCount, maxNumericChars << 1);
            array = _charArrayPool.Rent(newLen);
            if (value.TryFormat(destination: array.AsSpan(), out strLen, formatSpan, formatProvider))
            {
                return new DisposableString(array, strLen);
            }
            
            _charArrayPool.Return(array);
        }

        // unhappy path 
        _sb ??= new StringBuilder(length, MinimumInitialStringBuilderCapacity);
        _sb.AppendFormat(formatProvider, format ?? "", value);
        var str = CreateAndClear(_sb);
        return str;
    }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        _charArrayPool.Return(_array);
    }

    // todo - extension operator for ReadOnlySpan<char>?
    [MustDisposeResource, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DisposableString Concatenate(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
    {
        _sb ??= new StringBuilder(Math.Max(a.Length + b.Length, MinimumInitialStringBuilderCapacity));
        return CreateAndClear(_sb.Append(a).Append(b));
    }

    [MustDisposeResource, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DisposableString operator +(DisposableString a, DisposableString b) => Concatenate(a, b);

    [MustDisposeResource, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DisposableString operator +(DisposableString a, ReadOnlySpan<char> b) => Concatenate(a, b);

    [MustDisposeResource, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DisposableString operator +(ReadOnlySpan<char> a, DisposableString b) => Concatenate(a, b);
    
    [MustDisposeResource, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DisposableString operator +(string a, DisposableString b) => Concatenate(a, b);
    
    [MustDisposeResource, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DisposableString operator +(DisposableString a, string b) => Concatenate(a, b);
}