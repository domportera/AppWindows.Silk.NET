using System.Runtime.CompilerServices;
using Silk.NET.Windowing;

namespace AppWindows;

/// <summary>
/// Owns the lifecycle of a single surface: wires up the surface and window events, honors close requests and
/// releases the window once the surface terminates.
/// </summary>
/// <remarks>
/// Rendering and input are left to the derived type, which makes this the shared, toolkit-agnostic base for
/// concrete window implementations.
/// </remarks>
public abstract class SurfaceHost
{
    protected SurfaceHost()
    {
        _resizeAction = HandleDrawableSizeChanged;
        _coordinateAction = HandleCoordinatesChanged;
        _createAction = HandleCreated;
        _updateAction = HandleUpdate;
        _terminateAction = HandleTerminating;
        _renderAction = HandleRender;
        _lowMemoryAction = OnLowMemory;
        _fileDropAction = OnFileDrop;
        _focusChangedAction = OnFocusChanged;
        _pauseAction = HandlePaused;
        _resumeAction = HandleResumed;
        _closeRequestedAction = HandleCloseRequested;
    }

    /// <summary>
    /// Gets the surface this host manages. Assigning it initializes the host and starts listening for its events.
    /// </summary>
    public required Surface Surface
    {
        get => _surface!;
        init
        {
            _surface = value;
            OnInitialize(value);
            SetSubscriptions(value, true);
        }
    }
    

    /// <summary>Called before the surface starts delivering events.</summary>
    protected abstract void OnInitialize(Surface surface);

    /// <summary>Gets a value indicating whether this host has closed. A closed host no longer updates or renders.</summary>
    /// <remarks>No overrides will ever be invoked while this is True - so use this only for querying from background threads.</remarks>
    protected bool IsClosed { get; private set; }
    
    /// <summary>Called once per frame before drawing. This is where a UI is built, and is the same thread as the main Window and Input threads.</summary>
    protected abstract void OnFrame(SurfaceTimingEvent evt, int drawableWidth, int drawableHeight, int clientWidth, int clientHeight, float pixelDensity);

    /// <summary>Called once the surface exists and its rendering resources can be created.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual void OnCreated(SurfaceLifecycleEvent evt)
    {
    }


    /// <summary>Called once per render.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual void OnRender(SurfaceTimingEvent evt)
    {
    }

    /// <summary>Called while the surface terminates, before the host stops listening to it.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual void OnTerminated(SurfaceLifecycleEvent evt)
    {
    }

    /// <summary>Called when the surface is paused or resumed.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual void OnPaused(SurfaceLifecycleEvent evt, bool isPaused)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual void OnDrawableSizeChanged(SurfaceResizeEvent evt)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual void OnClientAreaChanged(WindowCoordinatesEvent evt)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual void OnFileDrop(WindowFileEvent evt)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual void OnFocusChanged(WindowToggleEvent evt)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual void OnLowMemory(SurfaceLifecycleEvent evt)
    {
    }

    /// <summary>
    /// Closes the host: releases its resources and takes its window off screen.
    /// </summary>
    /// <remarks>
    /// This is deliberately separate from <see cref="Surface.Terminate" />. The platform ticks child surfaces from a
    /// list that it mutates when one of them terminates, so terminating a child from inside its own tick is not safe.
    /// Closing stops the host instead; the surface itself is finished off when the application shuts down.
    /// </remarks>
    protected void Close()
    {
        if (_surface is not null)
        {
            HandleTerminating(new SurfaceLifecycleEvent(_surface));
        }
    }
    
    private void HandleTerminating(SurfaceLifecycleEvent evt)
    {
        if (IsClosed)
            return;

        IsClosed = true;
        
        // terminating only stops the surface from being ticked; the native window lives until the platform shuts down.
        // Hiding it is the only way to get it off the screen without tearing the rest of the application down, which is
        // what would otherwise leave a closed child window frozen in place.
        if (evt.Surface.Window is { IsVisible: true } window)
        {
            window.IsVisible = false;
        }

        SetSubscriptions(surface: evt.Surface, subscribe: false);
        OnTerminated(evt);
    }

    /// <summary>
    /// Terminates the surface when its window asks to close. The platform lifecycle only does this for the root surface,
    /// so without this a child window would ignore its close button.
    /// </summary>
    private static void HandleCloseRequested(WindowToggleEvent evt) => evt.Surface.Terminate();

    private void HandleCreated(SurfaceLifecycleEvent evt)
    {
        CaptureSizes(evt.Surface);
        OnCreated(evt);

        return;

        void CaptureSizes(Surface surface)
        {
            if (surface.Window is { } window)
            {
                _clientWidth = (int)Math.Round(window.ClientSize.X);
                _clientHeight = (int)Math.Round(window.ClientSize.Y);
            }

            var drawableSize = surface.DrawableSize;
            _drawableWidth = (int)Math.Round(drawableSize.X);
            _drawableHeight = (int)Math.Round(drawableSize.Y);
        }
    }

    private void HandleUpdate(SurfaceTimingEvent evt)
    {
        if (IsClosed) return;
        
        var pixelDensity = Surface.Scale?.PixelDensity ?? 0f;
        if (pixelDensity <= 0f)
        {
            pixelDensity = _clientWidth > 0 ? _drawableWidth / (float)_clientWidth : 1f;
        }

        OnFrame(evt, _drawableWidth, _drawableHeight, _clientWidth, _clientHeight, pixelDensity);
    }

    private void HandleRender(SurfaceTimingEvent evt)
    {
        if (!IsClosed)
        {
            OnRender(evt);
        }
    }

    private void HandleDrawableSizeChanged(SurfaceResizeEvent evt)
    {
        _drawableWidth = (int)Math.Round(evt.NewSize.X);
        _drawableHeight = (int)Math.Round(evt.NewSize.Y);
        OnDrawableSizeChanged(evt);
    }

    private void HandleCoordinatesChanged(WindowCoordinatesEvent evt)
    {
        _clientWidth = (int)Math.Round(evt.NewClientArea.Size.X);
        _clientHeight = (int)Math.Round(evt.NewClientArea.Size.Y);
        OnClientAreaChanged(evt);
    }

    private void HandlePaused(SurfaceLifecycleEvent evt) => OnPaused(evt, true);
    
    private void HandleResumed(SurfaceLifecycleEvent evt) => OnPaused(evt, false);

    private void SetSubscriptions(Surface surface, bool subscribe)
    {
        var window = surface.Window;
        if (subscribe)
        {
            surface.DrawableSizeChanged += _resizeAction;
            surface.Created += _createAction;
            surface.Update += _updateAction;
            surface.Terminating += _terminateAction;
            surface.Render += _renderAction;
            surface.LowMemory += _lowMemoryAction;
            surface.Pausing += _pauseAction;
            surface.Resuming += _resumeAction;
            if (window != null)
            {
                window.CoordinatesChanged += _coordinateAction;
                window.FileDrop += _fileDropAction;
                window.FocusChanged += _focusChangedAction;
                window.CloseRequested += _closeRequestedAction;
            }

            return;
        }

        surface.DrawableSizeChanged -= _resizeAction;
        surface.Created -= _createAction;
        surface.Update -= _updateAction;
        surface.Terminating -= _terminateAction;
        surface.Render -= _renderAction;
        surface.LowMemory -= _lowMemoryAction;
        surface.Pausing -= _pauseAction;
        surface.Resuming -= _resumeAction;

        if (window != null)
        {
            window.CoordinatesChanged -= _coordinateAction;
            window.FileDrop -= _fileDropAction;
            window.FocusChanged -= _focusChangedAction;
            window.CloseRequested -= _closeRequestedAction;
        }
    }

    
    private readonly Surface? _surface;
    
    #region Delegate cache
    private readonly Action<SurfaceResizeEvent> _resizeAction;
    private readonly Action<WindowCoordinatesEvent> _coordinateAction;
    private readonly Action<SurfaceLifecycleEvent> _createAction;
    private readonly Action<SurfaceTimingEvent> _updateAction;
    private readonly Action<SurfaceLifecycleEvent> _terminateAction;
    private readonly Action<SurfaceLifecycleEvent> _pauseAction;
    private readonly Action<SurfaceLifecycleEvent> _resumeAction;
    private readonly Action<SurfaceTimingEvent> _renderAction;
    private readonly Action<SurfaceLifecycleEvent> _lowMemoryAction;
    private readonly Action<WindowFileEvent> _fileDropAction;
    private readonly Action<WindowToggleEvent> _focusChangedAction;
    private readonly Action<WindowToggleEvent> _closeRequestedAction;
    #endregion Delegate cache
    
    
    /// <summary>Gets the width of the drawable (framebuffer) in pixels.</summary>
    private int _drawableWidth;

    /// <summary>Gets the height of the drawable (framebuffer) in pixels.</summary>
    private int _drawableHeight;

    /// <summary>Gets the width of the window client area in logical units.</summary>
    private int _clientWidth;

    /// <summary>Gets the height of the window client area in logical units.</summary>
    private int _clientHeight;
}

public readonly ref struct DisplayInfo(
    int windowWidth,
    int windowHeight,
    int drawableWidth,
    int drawableHeight,
    float windowScaling)
{
    public readonly int WindowWidth = windowWidth;
    public readonly int WindowHeight = windowHeight;
    public readonly int DrawableWidth = drawableWidth;
    public readonly int DrawableHeight = drawableHeight;
    public readonly float WindowScaling = windowScaling;
}

public readonly ref struct DrawArgs(
    DisplayInfo display,
    double deltaSeconds)
{
    public readonly DisplayInfo Display = display;
    public readonly double DeltaSeconds = deltaSeconds;
}
