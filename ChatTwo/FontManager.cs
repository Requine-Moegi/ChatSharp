using System.Text;
using ChatTwo.Http;
using Dalamud.Interface;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using Dalamud.Bindings.ImGui;

namespace ChatTwo;

public class FontManager
{
    public IFontHandle Axis = null!;
    public IFontHandle AxisItalic = null!;

    private IFontHandle? _vanillaAxis;
    private IFontHandle? _vanillaAxisItalic;
    private float _vanillaAxisPt = -1f;
    private float _vanillaItalicPt = -1f;
    private float _vanillaAxisBuiltPx = -1f;
    private float _vanillaItalicBuiltPx = -1f;
    private long _vanillaAxisBuiltAt;
    private long _vanillaItalicBuiltAt;
    private readonly Dictionary<int, FixedAxis> _fixedAxis = new();
    private bool _loggedLogPush;
    private string? _loggedLine;
    private VanillaFontReport _lastLogFont;

    private sealed class FixedAxis
    {
        public required IFontHandle Handle;
        public float Pt;
        public float BuiltPx;
        public long BuiltAt;
    }

    /// <summary>
    /// What the last vanilla log-text push actually put on the ImGui font stack.
    /// </summary>
    public readonly record struct VanillaFontReport(
        float RequestedPt,
        float RequestedPx,
        bool Ready,
        string? LoadError,
        bool Pushed,
        float DrawnPx,
        float GlobalScale,
        string Source);

    public IFontHandle FontAwesome = null!;

    public readonly byte[] GameSymFont;

    public static readonly HashSet<float> AxisFontSizeList =
    [
        9.6f, 10f, 12f, 14f, 16f,
        18f, 18.4f, 20f, 23f, 34f,
        36f, 40f, 45f, 46f, 68f, 90f,
    ];

    public FontManager()
    {
        var filePath = Path.Combine(Plugin.Interface.ConfigDirectory.FullName, "FFXIV_Lodestone_SSF.ttf");
        if (File.Exists(filePath))
        {
            GameSymFont = File.ReadAllBytes(filePath);
        }
        else
        {
            GameSymFont = ServerCore.HttpClient.GetAsync("https://img.finalfantasyxiv.com/lds/pc/global/fonts/FFXIV_Lodestone_SSF.ttf")
                .Result
                .Content
                .ReadAsByteArrayAsync()
                .Result;

            Dalamud.Utility.FilesystemUtil.WriteAllBytesSafe(filePath, GameSymFont);
        }
    }

    public void BuildFonts()
    {
        Axis = Plugin.Interface.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.Axis, SizeInPx(Plugin.Config.FontSizeV2)));
        AxisItalic = Plugin.Interface.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.Axis, SizeInPx(Plugin.Config.FontSizeV2))
        {
            SkewStrength = SizeInPx(Plugin.Config.FontSizeV2) / 6
        });

        FontAwesome = Plugin.Interface.UiBuilder.FontAtlas.NewDelegateFontHandle(e =>
        {
            e.OnPreBuild(tk => tk.AddFontAwesomeIconFont(new SafeFontConfig { SizePx = GetFontSize() }));
            e.OnPostBuild(tk => tk.FitRatio(tk.Font));
        });
    }

    public static float SizeInPt(float px) => (float) (px * 3.0 / 4.0);
    public static float SizeInPx(float pt) => (float) (pt * 4.0 / 3.0);

    /// <summary>
    /// On-screen pixel size of the vanilla chat text. The setting is what gets drawn.
    /// </summary>
    public static float VanillaFontScreenPx()
    {
        var px = Plugin.Config.VanillaFontSizePx;
        if (!float.IsFinite(px) || px < 1f)
            return 16f;
        return px;
    }

    /// <summary>
    /// Pixel size to pass to <see cref="IFontAtlas.NewGameFontHandle"/> so the glyphs come out
    /// <paramref name="screenPx"/> tall. Dalamud's game-font build and ImGui's global scale
    /// multiply the requested size, so the handle is given <paramref name="screenPx"/> divided by that scale.
    /// </summary>
    public static float HandlePixels(float screenPx)
    {
        var dalamud = ImGuiHelpers.GlobalScale;
        if (!float.IsFinite(dalamud) || dalamud < 0.05f)
            dalamud = 1f;
        return screenPx / dalamud;
    }
    public static float GetFontSize() => VanillaFontScreenPx();

    /// <summary>
    /// Axis at the game's chat-log size. The handle is created with an explicit pixel size and
    /// pushed only after the atlas has built it. Until then the startup Axis face is used, never
    /// the saved custom font. A handle that is still missing after a few seconds is built again.
    /// </summary>
    public IDisposable PushVanillaAxis()
    {
        EnsureVanillaAxis();
        if (_vanillaAxis!.Available)
            return PushIfReady(_vanillaAxis, _vanillaAxisPt, _vanillaAxisBuiltPx, recordLog: true, "game Axis");

        // The startup Axis face stays underneath this push until the log-sized face is ready.
        if (Axis is { Available: true })
            return PushIfReady(Axis, VanillaFontScreenPx(), 0f, recordLog: true, "fallback Axis at the saved size; log-size handle not ready");

        Remember(VanillaFontScreenPx(), 0f, false, _vanillaAxis.LoadException?.Message, false, ImGui.GetFontSize(), ImGuiHelpers.GlobalScale, "fallback Dalamud default; no Axis handle is ready");
        return NopDispose.Instance;
    }

    /// <summary>
    /// Same Axis handle, without replacing the chat window's last-push readout.
    /// </summary>
    public IDisposable PushVanillaAxisPreview()
    {
        EnsureVanillaAxis();
        return PushIfReady(_vanillaAxis!, _vanillaAxisPt, _vanillaAxisBuiltPx, recordLog: false, "game Axis");
    }

    /// <summary>
    /// Italic Axis at the same log size. Returns a no-op until that handle is built, so the
    /// regular Axis pushed around the line stays up for that frame.
    /// </summary>
    public IDisposable PushVanillaAxisItalic()
    {
        EnsureVanillaAxis();
        return PushIfReady(_vanillaAxisItalic!, _vanillaAxisPt, _vanillaItalicBuiltPx, recordLog: false, "game Axis italic");
    }

    /// <summary>
    /// Axis for tab labels (12pt) and the channel name (14pt). Those point sizes match the
    /// default 16px chat font at Dalamud scale 1, and they grow with the Font size setting
    /// so the labels stay that size next to the chat text at any global scale.
    /// </summary>
    public IDisposable PushFixedAxis(float pt)
    {
        var key = (int)Math.Round(pt * 100f);
        var screen = VanillaFontScreenPx() * SizeInPx(pt) / 16f;
        var px = HandlePixels(screen);
        if (!_fixedAxis.TryGetValue(key, out var entry) || NeedsRebuild(entry.Handle, entry.BuiltPx, px, entry.BuiltAt))
        {
            if (entry != null)
            {
                if (entry.Handle.LoadException != null)
                    Plugin.Log.Information($"Vanilla Axis {pt:0.##}pt failed ({entry.Handle.LoadException.Message}). Building it again.");
                entry.Handle.Dispose();
            }

            entry = new FixedAxis
            {
                Handle = NewAxis(px),
                Pt = pt,
                BuiltPx = px,
                BuiltAt = Environment.TickCount64,
            };
            _fixedAxis[key] = entry;
        }

        return PushIfReady(entry.Handle, screen, entry.BuiltPx, recordLog: false, "game Axis");
    }

    /// <summary>
    /// Readout for <c>/chat2vanillaDebugger</c>. Includes the handle the chat last pushed, not only
    /// the font the debugger window itself is using.
    /// </summary>
    public string DescribeVanillaFont()
    {
        EnsureVanillaAxis();
        var log = _vanillaAxis!;
        var italic = _vanillaAxisItalic!;
        var report = new StringBuilder();
        report.AppendLine($"setting {_vanillaAxisPt:0.##} px; handle {_vanillaAxisBuiltPx:0.##} px via IFontAtlas.NewGameFontHandle(Axis)");
        report.AppendLine($"handle ready={log.Available} italic ready={italic.Available} globalScale={ImGuiHelpers.GlobalScale:0.##}");
        if (log.LoadException != null)
            report.AppendLine($"load error: {log.LoadException.Message}");
        if (_loggedLogPush)
            report.AppendLine($"last log push: {_lastLogFont.Source}, drawn {_lastLogFont.DrawnPx:0.##} px, ready={_lastLogFont.Ready}, pushed={_lastLogFont.Pushed}");
        else
            report.AppendLine("last log push: the chat window has not drawn yet");
        report.AppendLine(Ui.Style.VanillaGameLook.DescribeLogFont());
        foreach (var (key, entry) in _fixedAxis.OrderBy(pair => pair.Key))
            report.AppendLine($"fixed Axis {key / 100f:0.##} pt ready={entry.Handle.Available} error={entry.Handle.LoadException?.Message ?? "none"}");
        return report.ToString().TrimEnd();
    }

    public IFontHandle VanillaAxisItalic
    {
        get
        {
            EnsureVanillaAxis();
            return _vanillaAxisItalic!;
        }
    }

    private void EnsureVanillaAxis()
    {
        var screenPx = VanillaFontScreenPx();
        var px = HandlePixels(screenPx);
        if (NeedsRebuild(_vanillaAxis, _vanillaAxisBuiltPx, px, _vanillaAxisBuiltAt))
        {
            if (_vanillaAxis?.LoadException != null)
                Plugin.Log.Information($"Vanilla log Axis failed ({_vanillaAxis.LoadException.Message}). Building {screenPx:0.##}px again.");
            _vanillaAxis?.Dispose();
            _vanillaAxis = NewAxis(px);
            _vanillaAxisPt = screenPx;
            _vanillaAxisBuiltPx = px;
            _vanillaAxisBuiltAt = Environment.TickCount64;
            _loggedLogPush = false;
        }

        if (NeedsRebuild(_vanillaAxisItalic, _vanillaItalicBuiltPx, px, _vanillaItalicBuiltAt))
        {
            if (_vanillaAxisItalic?.LoadException != null)
                Plugin.Log.Information($"Vanilla italic Axis failed ({_vanillaAxisItalic.LoadException.Message}). Building {screenPx:0.##}px again.");
            _vanillaAxisItalic?.Dispose();
            _vanillaAxisItalic = NewAxis(px, italicSkew: px / 6f);
            _vanillaItalicPt = screenPx;
            _vanillaItalicBuiltPx = px;
            _vanillaItalicBuiltAt = Environment.TickCount64;
        }
    }

    /// <summary>
    /// A handle that is still waiting on the atlas is left alone. One that is still
    /// missing after a few seconds, or whose size changed, is built again.
    /// Disposing it every frame is what kept the fallback font on screen.
    /// </summary>
    private static bool NeedsRebuild(IFontHandle? handle, float builtPt, float wantedPt, long builtAt)
    {
        if (handle == null || Math.Abs(builtPt - wantedPt) >= 0.05f)
            return true;
        if (handle.Available)
            return false;
        return Environment.TickCount64 - builtAt > 5000;
    }

    /// <summary>
    /// <paramref name="sizePx"/> is the size after dividing out Dalamud's global scale.
    /// </summary>
    private static IFontHandle NewAxis(float sizePx, float italicSkew = 0f)
    {
        var style = new GameFontStyle(GameFontFamily.Axis, sizePx);
        if (italicSkew > 0f)
            style.SkewStrength = italicSkew;
        return Plugin.Interface.UiBuilder.FontAtlas.NewGameFontHandle(style);
    }

    private IDisposable PushIfReady(IFontHandle handle, float settingPx, float handlePx, bool recordLog, string source)
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (!handle.Available)
        {
            if (recordLog)
                Remember(settingPx, handlePx, false, handle.LoadException?.Message, false, ImGui.GetFontSize(), scale, "fallback (atlas not ready, keeping the current font)");
            return NopDispose.Instance;
        }

        var pushed = handle.Push();
        var drawn = ImGui.GetFontSize();
        var match = FontMatches(handle) ? source : "pushed, but the active font pointer differs";
        if (recordLog)
            Remember(settingPx, handlePx, true, handle.LoadException?.Message, true, drawn, scale, match);
        return pushed;
    }

    private void Remember(float settingPx, float handlePx, bool ready, string? error, bool pushed, float drawn, float scale, string source)
    {
        _loggedLogPush = true;
        _lastLogFont = new VanillaFontReport(settingPx, handlePx, ready, error, pushed, drawn, scale, source);
        var line = $"Chat Sharp font: {source}; setting {settingPx:0.##}px; handle {handlePx:0.##}px; drawn {drawn:0.##}px; globalScale {scale:0.##}; ready={ready}; pushed={pushed}; error={error ?? "none"}; language={Plugin.DataManager.Language}; {Ui.Style.VanillaGameLook.DescribeLogFont()}";
        if (line == _loggedLine)
            return;

        _loggedLine = line;
        Plugin.Log.Information(line);
    }

    private static unsafe bool FontMatches(IFontHandle handle)
    {
        try
        {
            using var locked = handle.Lock();
            return ImGui.GetFont().Handle == locked.ImFont.Handle;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Vanilla font push could not be compared");
            return false;
        }
    }

    private sealed class NopDispose : IDisposable
    {
        public static readonly NopDispose Instance = new();
        public void Dispose()
        {
        }
    }
}
