using System.Numerics;
using ChatTwo.Code;
using ChatTwo.GameFunctions.Types;
using ChatTwo.Resources;
using ChatTwo.Ui.Handler;
using ChatTwo.Ui.Style;
using ChatTwo.Util;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Lumina.Extensions;

namespace ChatTwo.Ui;

public class Popout : Window, IChatWindow
{
    private readonly Plugin Plugin;
    private Tab Tab;
    private int Idx;

    private long FrameTime; // set every frame
    private long LastActivityTime = Environment.TickCount64;

    private readonly string ChatChannelPicker = "chat-popout-channel-picker";

    public readonly InputHandler InputHandler;

    public Vector2 LastWindowPos { get; set; } = Vector2.Zero;
    public Vector2 LastWindowSize { get; set; } = Vector2.Zero;
    public HideState CurrentHideState { get; set; } = HideState.None;

    internal static string WindowTitle(Tab tab) => $"{tab.Name}###chat2vanilla-popout-{tab.Identifier:N}";

    public Popout(Plugin plugin, Tab tab, int idx) : base(WindowTitle(tab))
    {
        Plugin = plugin;
        Tab = tab;
        Idx = idx;

        InputHandler = new InputHandler(this, plugin, $"ChatLog{idx}-{tab.Name}");

        Size = new Vector2(350, 350);
        SizeCondition = ImGuiCond.FirstUseEver;
        if (Tab.HasPopOutBounds && Tab.PopOutW >= 8f && Tab.PopOutH >= 8f)
        {
            Size = new Vector2(Tab.PopOutW, Tab.PopOutH);
            SizeCondition = ImGuiCond.Once;
            Position = new Vector2(Tab.PopOutX, Tab.PopOutY);
            PositionCondition = ImGuiCond.Once;
        }

        IsOpen = true;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;

        ChatChannelPicker += $"-{idx}-{tab.Name}";
    }

    public override void PreOpenCheck()
    {
        // Saving settings replaces every tab object. Follow the tab that kept this
        // identifier so this window is not closed just because its old object was dropped.
        var tabs = Plugin.Config.Tabs;
        for (var i = 0; i < tabs.Count; i++)
        {
            if (tabs[i].Identifier != Tab.Identifier)
                continue;

            if (!ReferenceEquals(tabs[i], Tab))
                Retarget(tabs[i], i);
            break;
        }

        if (!Tab.PopOut || Plugin.Config.Tabs.All(t => t.Identifier != Tab.Identifier))
            IsOpen = false;
    }

    /// <summary>
    /// Point this window at the tab object that replaced the one it was opened with.
    /// The ImGui id stays on the tab identifier, so the saved position stays with this window.
    /// </summary>
    internal void Retarget(Tab tab, int idx)
    {
        Tab = tab;
        Idx = idx;
        var title = WindowTitle(tab);
        if (WindowName != title)
            WindowName = title;
    }

    public override bool DrawConditions()
    {
        FrameTime = Environment.TickCount64;

        var isHidden = Tab.IndependentHide
            ? HideStateHelper.HideStateCheck(this, Tab.HideInBattle, Tab.HideDuringCutscenes, Tab.HideWhenNotLoggedIn, false)
            : Plugin.ChatLog.IsHidden;

        if (isHidden)
            return false;

        if (!Plugin.Config.HideWhenInactive || (!Plugin.Config.InactivityHideActiveDuringBattle && Plugin.InBattle) || !Tab.UnhideOnActivity)
        {
            LastActivityTime = FrameTime;
            return true;
        }

        // Activity in the tab, this popout window, or the main chat log window.
        var lastActivityTime = Math.Max(Tab.LastActivity, LastActivityTime);
        lastActivityTime = Math.Max(lastActivityTime, InputHandler.LastActivityTime);
        return FrameTime - lastActivityTime <= 1000 * Plugin.Config.InactivityHideTimeout;
    }

    public override void PreDraw()
    {
        // The corner grip is the resize control. ImGui's border stays off so it
        // does not fight that grip. Lock pop-out size hides the grip as well.
        Flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoResize;
        if (!Tab.CanMove || Plugin.Config.LockPopOutPosition)
            Flags |= ImGuiWindowFlags.NoMove;

        if (!Tab.CanResize || Plugin.Config.LockPopOutSize)
            Flags |= ImGuiWindowFlags.NoResize;

        BgAlpha = 0f;
        ChatChrome.PushWindowStyle();
    }

    public override void Draw()
    {
        using var font = ChatChrome.PushContentFont();
        using var id = ImRaii.PushId($"popout-{Tab.Identifier}");

        LastWindowSize = ImGui.GetWindowSize();
        LastWindowPos = ImGui.GetWindowPos();
        RememberBounds();

        if (ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows))
            LastActivityTime = FrameTime;

        var chromeAlpha = Tab.IndependentOpacity ? Tab.Opacity / 100f : VanillaGameLook.LogBackgroundOpacityFactor();
        var inputAlpha = Tab.IndependentOpacity ? chromeAlpha : VanillaGameLook.LogInputAlpha();
        DrawVanillaPopout(chromeAlpha, inputAlpha);
    }

    private void DrawVanillaPopout(float chromeAlpha, float inputAlpha)
    {
        var frame = ChatChrome.Frame(Tab.SupportsInput);
        ChatChrome.BeginScrollDrag(frame.Scroll);
        ChatChrome.DrawLogBackground(frame, chromeAlpha, VanillaGameLook.TabLogBackgroundTint(Tab));

        var content = ChatChrome.LogContentRect(frame);
        var messages = MessageListRect(frame, content);
        ImGui.SetCursorScreenPos(messages.Min);
        Plugin.ChatLog.DrawMessageLog(Tab, InputHandler.PayloadHandler, messages.H, false, messages.W, content);
        ChatChrome.PaintScrollbar(frame.Scroll);

        if (Tab.SupportsInput)
        {
            if (Tab.Channel is not null)
                Tab.CurrentChannel.SetChannel(Tab.Channel.Value);

            var tellSpecial = false;
            Plugin.ChatLog.DrawVanillaChannelAndInput(
                Tab,
                ChatChannelPicker,
                false,
                GetValidPopupChannels(),
                channel => Tab.CurrentChannel.SetChannel(channel),
                InputHandler,
                frame,
                ref tellSpecial,
                inputAlpha);
        }

        DrawVanillaPopoutStrip(frame);

        if (Tab.CanResize && !Plugin.Config.LockPopOutSize)
            ChatChrome.DrawGrip(frame);
    }

    /// <summary>
    /// The message child is drawn after the tab and input bars. Its top and bottom stay
    /// inside the log texture's opaque center, not the nine-grid's transparent border.
    /// </summary>
    private VanillaRect MessageListRect(VanillaFrame frame, VanillaRect content)
    {
        var messages = frame.Messages;
        var top = Math.Max(messages.Y, content.Y);
        var bottom = Math.Min(messages.Y + messages.H, content.Y + content.H);
        if (!Tab.SupportsInput)
            bottom = content.Y + content.H;

        var chromeTop = frame.TabStrip.Y;
        if (Tab.SupportsInput)
            chromeTop = Math.Min(chromeTop, frame.Input.Y);
        bottom = Math.Min(bottom, chromeTop);

        return new VanillaRect(messages.X, top, messages.W, Math.Max(frame.Scale * 20f, bottom - top));
    }

    private void DrawVanillaPopoutStrip(VanillaFrame frame)
    {
        var right = frame.Gear.X - 21f * frame.Scale;
        var strip = new VanillaRect(frame.TabStrip.X, frame.TabStrip.Y, Math.Max(36f * frame.Scale, right - frame.TabStrip.X), frame.TabStrip.H);
        var tab = strip;
        ChatChrome.DrawTabFace(tab, false);
        ChatChrome.DrawTabCaps(tab);
        using (Plugin.FontManager.PushFixedAxis(12f))
        {
            var textSize = ImGui.CalcTextSize(Tab.Name);
            var textPos = new Vector2(
                MathF.Round(tab.X + (tab.W - textSize.X) * 0.5f),
                MathF.Round(tab.Y + (tab.H - textSize.Y) * 0.5f));
            VanillaText.DrawAt(textPos, Tab.Name, Vector4.One);
        }

        ChatChrome.DrawTabMarker(tab);

        if (ChatChrome.DrawCircleIcon("###popout-settings", ChatChrome.Uld.ButtonGear, frame.Gear, ChatChrome.VanillaGlyph.Gear))
            Plugin.SettingsWindow.Toggle();
        if (ImGui.IsItemHovered())
            ImGuiUtil.Tooltip(string.Format(Language.Settings_Title, Plugin.PluginName));

        if (ChatChrome.DrawCircleIcon("###popout-close", ChatChrome.Uld.ButtonClose, frame.Close, ChatChrome.VanillaGlyph.Close))
            OnClose();
        if (ImGui.IsItemHovered())
            ImGuiUtil.Tooltip(Language.ChatLog_Tabs_Dock);
    }

    public override void PostDraw()
    {
        Plugin.ChatLog.PopOutDocked[Idx] = ImGui.IsWindowDocked();

        ChatChrome.PopWindowStyle();
    }

    internal Guid TabIdentifier => Tab.Identifier;

    public override void OnClose()
    {
        IsOpen = false;
        Plugin.ChatLog.UnregisterPopOut(this);

        Tab.PopOut = false;
        Plugin.SaveConfig();
    }

    /// <summary>
    /// Keep this tab's pop-out where the player left it. Dalamud does not keep imgui.ini
    /// for these windows, so the position is stored on the tab and applied once on open.
    /// </summary>
    private void RememberBounds()
    {
        if (ImGui.IsWindowDocked())
            return;

        var scale = ImGuiHelpers.GlobalScale;
        if (!float.IsFinite(scale) || scale < 0.05f)
            scale = 1f;

        var pos = ImGui.GetWindowPos();
        var size = ImGui.GetWindowSize() / scale;
        if (size.X < 8f || size.Y < 8f)
            return;

        if (Tab.HasPopOutBounds
            && Math.Abs(Tab.PopOutX - pos.X) < 0.5f
            && Math.Abs(Tab.PopOutY - pos.Y) < 0.5f
            && Math.Abs(Tab.PopOutW - size.X) < 0.5f
            && Math.Abs(Tab.PopOutH - size.Y) < 0.5f)
            return;

        Tab.HasPopOutBounds = true;
        Tab.PopOutX = pos.X;
        Tab.PopOutY = pos.Y;
        Tab.PopOutW = size.X;
        Tab.PopOutH = size.Y;
        Plugin.DeferredSaveFrames = 30;
    }

    /// <summary>
    /// Drop the window after a settings save already stored the new PopOut flag.
    /// Does not write that flag again, so a tab that is still popped out cannot be docked by the close.
    /// </summary>
    internal void CloseForConfigSwap()
    {
        IsOpen = false;
        Plugin.ChatLog.UnregisterPopOut(this);
    }

    private Dictionary<string, InputChannel> GetValidPopupChannels()
    {
        var channels = new Dictionary<string, InputChannel>();
        foreach (var channel in Enum.GetValues<InputChannel>())
        {
            if (channel is InputChannel.Invalid or InputChannel.Tell)
                continue;

            var name = Sheets.LogFilterSheet.FirstOrNull(row => row.LogKind == (byte) channel.ToChatType())?.Name.ToString() ?? channel.ToChatType().Name();
            if (channel.IsLinkshell())
            {
                var lsName = GameFunctions.Chat.GetLinkshellName(channel.LinkshellIndex());
                if (string.IsNullOrWhiteSpace(lsName))
                    continue;

                name += $": {lsName}";
            }

            if (channel.IsCrossLinkshell())
            {
                var lsName = GameFunctions.Chat.GetCrossLinkshellName(channel.LinkshellIndex());
                if (string.IsNullOrWhiteSpace(lsName))
                    continue;

                name += $": {lsName}";
            }

            // Check if the linkshell with this index is registered in
            // the ExtraChat plugin by seeing if the command is
            // registered. The command gets registered only if a
            // linkshell is assigned (and even gets unassigned if the
            // index changes!).
            if (channel.IsExtraChatLinkshell() && !Plugin.CommandManager.Commands.ContainsKey(channel.Prefix()))
                continue;

            channels.Add(name, channel);
        }

        return channels;
    }
}
