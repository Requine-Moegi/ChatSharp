using ChatTwo.Resources;
using ChatTwo.Ui.Style;
using ChatTwo.Util;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace ChatTwo.Ui.SettingsTabs;

internal static class SettingsSections
{
    public static void Header(string title)
    {
        ImGui.Spacing();
        ImGui.TextUnformatted(title);
        ImGui.Separator();
        ImGui.Spacing();
    }
}

public sealed class GeneralPage(Configuration mutable) : ISettingsTab
{
    public string Name => "General###tabs-general";

    public void Draw(bool changed)
    {
        using var wrap = ImRaii.TextWrapPos(0.0f);

        SettingsSections.Header("Visibility");
        ImGuiUtil.OptionCheckbox(ref mutable.HideChat, Language.Options_HideChat_Name, Language.Options_HideChat_Description);
        ImGui.Spacing();
        ImGuiUtil.OptionCheckbox(ref mutable.HideDuringCutscenes, Language.Options_HideDuringCutscenes_Name, string.Format(Language.Options_HideDuringCutscenes_Description, Plugin.PluginName));
        ImGui.Spacing();
        ImGuiUtil.OptionCheckbox(ref mutable.HideWhenNotLoggedIn, Language.Options_HideWhenNotLoggedIn_Name, string.Format(Language.Options_HideWhenNotLoggedIn_Description, Plugin.PluginName));
        ImGui.Spacing();
        ImGuiUtil.OptionCheckbox(ref mutable.HideWhenUiHidden, Language.Options_HideWhenUiHidden_Name, string.Format(Language.Options_HideWhenUiHidden_Description, Plugin.PluginName));
        ImGui.Spacing();
        ImGuiUtil.OptionCheckbox(ref mutable.HideInLoadingScreens, Language.Options_HideInLoadingScreens_Name, string.Format(Language.Options_HideInLoadingScreens_Description, Plugin.PluginName));
        ImGui.Spacing();
        ImGuiUtil.OptionCheckbox(ref mutable.HideInBattle, Language.Options_HideInBattle_Name, Language.Options_HideInBattle_Description);
        ImGui.Spacing();

        SettingsSections.Header("Hide when idle");
        ImGuiUtil.OptionCheckbox(ref mutable.HideWhenInactive, Language.Options_HideWhenInactive_Name, Language.Options_HideWhenInactive_Description);
        ImGui.Spacing();
        if (mutable.HideWhenInactive)
        {
            using var indent = ImRaii.PushIndent();
            ImGuiUtil.InputIntVertical(Language.Options_InactivityHideTimeout_Name,
                Language.Options_InactivityHideTimeout_Description, ref mutable.InactivityHideTimeout, 1, 10);
            mutable.InactivityHideTimeout = Math.Max(2, mutable.InactivityHideTimeout);
            ImGui.Spacing();

            using (ImRaii.Disabled(mutable.HideInBattle))
            {
                ImGuiUtil.OptionCheckbox(ref mutable.InactivityHideActiveDuringBattle,
                    Language.Options_InactivityHideActiveDuringBattle_Name,
                    Language.Options_InactivityHideActiveDuringBattle_Description);
                ImGui.Spacing();
            }

            using var channelTree = ImRaii.TreeNode(Language.Options_InactivityHideChannels_Name);
            if (channelTree.Success)
            {
                if (ImGuiUtil.CtrlShiftButton(Language.Options_InactivityHideChannels_All_Label, Language.Options_InactivityHideChannels_Button_Tooltip))
                {
                    mutable.InactivityHideChannelsV2 = TabsUtil.AllChannels();
                    mutable.InactivityHideExtraChatAll = true;
                    mutable.InactivityHideExtraChatChannels = [];
                }

                ImGui.SameLine();
                if (ImGuiUtil.CtrlShiftButton(Language.Options_InactivityHideChannels_None_Label, Language.Options_InactivityHideChannels_Button_Tooltip))
                {
                    mutable.InactivityHideChannelsV2 = [];
                    mutable.InactivityHideExtraChatAll = false;
                    mutable.InactivityHideExtraChatChannels = [];
                }

                ImGui.Spacing();
                ImGuiUtil.ChannelSelector(Language.Options_Tabs_Channels, mutable.InactivityHideChannelsV2);
                ImGuiUtil.ExtraChatSelector(Language.Options_Tabs_ExtraChatChannels,
                    ref mutable.InactivityHideExtraChatAll, mutable.InactivityHideExtraChatChannels);
            }
        }

        SettingsSections.Header("Language");
        using (var combo = ImGuiUtil.BeginComboVertical(Language.Options_Language_Name, mutable.LanguageOverride.Name()))
        {
            if (combo.Success)
            {
                foreach (var language in Enum.GetValues<LanguageOverride>())
                    if (ImGui.Selectable(language.Name()))
                        mutable.LanguageOverride = language;
            }
        }

        ImGuiUtil.HelpText(string.Format(Language.Options_Language_Description, Plugin.PluginName));
    }
}

public sealed class AppearancePage : ISettingsTab
{
    private readonly Plugin Plugin;
    private readonly ChatColours Colours;

    public string Name => "Appearance###tabs-appearance";

    public AppearancePage(Plugin plugin, Configuration mutable)
    {
        Plugin = plugin;
        Mutable = mutable;
        Colours = new ChatColours(plugin, mutable);
    }

    private Configuration Mutable { get; }

    public void Draw(bool changed)
    {
        using var wrap = ImRaii.TextWrapPos(0.0f);

        SettingsSections.Header("Window");
        ImGuiUtil.OptionCheckbox(ref Mutable.CanMove, Language.Options_CanMove_Name);
        ImGuiUtil.HelpText("Turn this off to pin the main window. Its saved position is kept.");
        ImGui.Spacing();
        ImGuiUtil.OptionCheckbox(ref Mutable.CanResize, Language.Options_CanResize_Name);
        ImGuiUtil.HelpText(Language.Options_VanillaStyle_ResizeNote);
        ImGui.Spacing();

        ImGui.TextUnformatted(Language.Options_AdjustPosition_Name);
        ImGui.SetNextItemWidth(-1);
        var pos = Plugin.ChatLog.LastWindowPos;
        if (ImGui.DragFloat2("##chat-position", ref pos, 1, 0, float.MaxValue, "%.0fpx"))
            Plugin.ChatLog.Position = pos;
        ImGuiUtil.WarningText(Language.Options_AdjustPosition_Warning);
        ImGui.Spacing();

        SettingsSections.Header("Log");
        ImGuiUtil.OptionCheckbox(ref Mutable.HighlightUnreadTabs, Language.Options_HighlightUnreadTabs_Name, Language.Options_HighlightUnreadTabs_Description);
        ImGui.Spacing();

        var followedGame = Mutable.UseGameLogOpacity;
        ImGuiUtil.OptionCheckbox(ref Mutable.UseGameLogOpacity, Language.Options_LogOpacity_UseGame_Name, Language.Options_LogOpacity_UseGame_Description);
        if (followedGame && !Mutable.UseGameLogOpacity)
            Mutable.LogBackgroundOpacity = Math.Clamp(VanillaGameLook.LogBackgroundAlpha() * 100f, 0f, 100f);

        if (Mutable.UseGameLogOpacity)
        {
            var gamePercent = Math.Clamp(VanillaGameLook.LogBackgroundAlpha() * 100f, 0f, 100f);
            ImGui.BeginDisabled();
            ImGuiUtil.DragFloatVertical(Language.Options_LogOpacity_Name, ref gamePercent, 1f, 0f, 100f, $"{gamePercent:N0}%%", ImGuiSliderFlags.AlwaysClamp);
            ImGui.EndDisabled();
        }
        else
        {
            ImGuiUtil.DragFloatVertical(Language.Options_LogOpacity_Name, ref Mutable.LogBackgroundOpacity, 1f, 0f, 100f, $"{Mutable.LogBackgroundOpacity:N0}%%", ImGuiSliderFlags.AlwaysClamp);
            ImGuiUtil.HelpText(Language.Options_LogOpacity_Description);
        }

        ImGui.Spacing();
        ImGuiUtil.HelpText(Language.Options_VanillaStyle_PerTabColor);
        ImGui.Spacing();

        SettingsSections.Header("Input");
        ImGuiUtil.OptionCheckbox(ref Mutable.TintInputByChannel, Language.Options_TintInputByChannel_Name, Language.Options_TintInputByChannel_Description);
        if (Mutable.TintInputByChannel)
        {
            ImGuiUtil.DragFloatVertical(Language.Options_TintInputIntensity_Name, ref Mutable.InputChannelTint, 1f, 0f, 100f, $"{Mutable.InputChannelTint:N0}%%", ImGuiSliderFlags.AlwaysClamp);
            ImGui.Spacing();
            ImGuiUtil.OptionCheckbox(ref Mutable.TintInputOnlyWhileTyping, Language.Options_TintInputOnlyWhileTyping_Name, Language.Options_TintInputOnlyWhileTyping_Description);
            ImGui.Spacing();
        }

        SettingsSections.Header("Font");
        ImGuiUtil.DragFloatVertical(Language.Options_VanillaFontSize_Name, Language.Options_VanillaFontSize_Description, ref Mutable.VanillaFontSizePx, 1f, 8f, 72f, $"{Mutable.VanillaFontSizePx:N0} px", ImGuiSliderFlags.AlwaysClamp);
        Mutable.VanillaFontSizePx = MathF.Round(Mutable.VanillaFontSizePx);
        ImGuiUtil.HelpText("This is the on-screen size of the chat text. It stays this size when Dalamud's scale changes. Tab and channel labels follow it.");
        ImGui.Spacing();

        SettingsSections.Header("Channel colors");
        ImGuiUtil.HelpText("A color set here replaces the game's color for that channel. Leave a channel alone to keep the game color.");
        ImGui.Spacing();
        Colours.Draw(changed);
    }
}

public sealed class TabsPage(Plugin plugin, Configuration mutable) : ISettingsTab
{
    private readonly Tabs _tabs = new(plugin, mutable);
    public string Name => "Tabs###tabs-tabs-page";

    public void Draw(bool changed) => _tabs.Draw(changed);
}

public sealed class PopoutsPage(Configuration mutable) : ISettingsTab
{
    public string Name => "Pop-outs###tabs-popouts";

    public void Draw(bool changed)
    {
        using var wrap = ImRaii.TextWrapPos(0.0f);
        SettingsSections.Header("Lock");
        ImGuiUtil.OptionCheckbox(ref mutable.LockPopOutPosition, "Lock pop-out position");
        ImGuiUtil.HelpText("Pop-out windows cannot be dragged. The position already saved for each window stays where it is.");
        ImGui.Spacing();
        ImGuiUtil.OptionCheckbox(ref mutable.LockPopOutSize, "Lock pop-out size");
        ImGuiUtil.HelpText("Pop-out windows cannot be resized, and the corner grip is hidden. The size already saved for each window stays as it is.");
        ImGui.Spacing();
        ImGuiUtil.HelpText("Each tab can also turn off its own moving or resizing under Tabs. A lock here applies to every pop-out.");
    }
}

public sealed class ChatBehaviorPage : ISettingsTab
{
    private readonly Emote Emotes;

    public string Name => "Chat###tabs-chat-behavior";

    public ChatBehaviorPage(Plugin plugin, Configuration mutable)
    {
        Mutable = mutable;
        Emotes = new Emote(plugin, mutable);
    }

    private Configuration Mutable { get; }

    public void Draw(bool changed)
    {
        using var wrap = ImRaii.TextWrapPos(0.0f);

        SettingsSections.Header("Messages");
        ImGuiUtil.OptionCheckbox(ref Mutable.Use24HourClock, Language.Options_Use24HourClock_Name, Language.Options_Use24HourClock_Description);
        ImGuiUtil.OptionCheckbox(ref Mutable.PrettierTimestamps, Language.Options_PrettierTimestamps_Name, Language.Options_PrettierTimestamps_Description);
        if (Mutable.PrettierTimestamps)
        {
            using var indent = ImRaii.PushIndent();
            ImGuiUtil.OptionCheckbox(ref Mutable.MoreCompactPretty, Language.Options_MoreCompactPretty_Name, Language.Options_MoreCompactPretty_Description);
            ImGuiUtil.OptionCheckbox(ref Mutable.HideSameTimestamps, Language.Options_HideSameTimestamps_Name, Language.Options_HideSameTimestamps_Description);
        }

        ImGui.Spacing();
        ImGuiUtil.OptionCheckbox(ref Mutable.CollapseDuplicateMessages, Language.Options_CollapseDuplicateMessages_Name, Language.Options_CollapseDuplicateMessages_Description);
        if (Mutable.CollapseDuplicateMessages)
        {
            using var indent = ImRaii.PushIndent();
            ImGuiUtil.OptionCheckbox(ref Mutable.CollapseKeepUniqueLinks, Language.Options_CollapseDuplicateMsgUniqueLink_Name, Language.Options_CollapseDuplicateMsgUniqueLink_Description);
        }

        ImGui.Spacing();
        if (ImGuiUtil.InputIntVertical(Language.Options_MaxLinesToShow_Name, Language.Options_MaxLinesToShow_Description, ref Mutable.MaxLinesToRender))
            Mutable.MaxLinesToRender = Math.Clamp(Mutable.MaxLinesToRender, 1, 10_000);

        SettingsSections.Header("Input");
        ImGuiUtil.OptionCheckbox(ref Mutable.KeepInputFocus, Language.Options_KeepInputFocus_Name, Language.Options_KeepInputFocus_Description);
        ImGui.Spacing();
        ImGuiUtil.OptionCheckbox(ref Mutable.PlaySounds, Language.Options_PlaySounds_Name, Language.Options_PlaySounds_Description);
        ImGui.Spacing();
        ImGuiUtil.OptionCheckbox(ref Mutable.ShowNoviceNetwork, Language.Options_ShowNoviceNetwork_Name, Language.Options_ShowNoviceNetwork_Description);
        ImGui.Spacing();
        ImGuiUtil.OptionCheckbox(ref Mutable.ShowHideButton, Language.Options_ShowHideButton_Name, Language.Options_ShowHideButton_Description);
        ImGui.Spacing();
        ImGuiUtil.OptionCheckbox(ref Mutable.NativeItemTooltips, Language.Options_NativeItemTooltips_Name, string.Format(Language.Options_NativeItemTooltips_Description, Plugin.PluginName));
        if (Mutable.NativeItemTooltips)
            ImGuiUtil.DragFloatVertical(Language.Options_TooltipOffset_Name, Language.Options_TooltipOffset_Desc, ref Mutable.TooltipOffset, 1, 0f, 400f, $"{Mutable.TooltipOffset:N0}px", ImGuiSliderFlags.AlwaysClamp);

        SettingsSections.Header("Tab keybinds");
        ImGui.TextUnformatted(Language.Options_ChatTabForwardKeybind_Name);
        ImGui.SetNextItemWidth(-1);
        ImGuiUtil.KeybindInput("ChatTabForwardKeybind", ref Mutable.ChatTabForward);
        ImGui.TextUnformatted(Language.Options_ChatTabBackwardKeybind_Name);
        ImGui.SetNextItemWidth(-1);
        ImGuiUtil.KeybindInput("ChatTabBackwardKeybind", ref Mutable.ChatTabBackward);

        SettingsSections.Header("Command preview");
        using (var combo = ImGuiUtil.BeginComboVertical(Language.Options_Preview_Name, Mutable.PreviewPosition.Name()))
        {
            if (combo)
            {
                foreach (var position in Enum.GetValues<PreviewPosition>())
                    if (ImGui.Selectable(position.Name(), Mutable.PreviewPosition == position))
                        Mutable.PreviewPosition = position;
            }
        }

        ImGuiUtil.HelpText(Language.Options_Preview_Description);
        ImGui.Spacing();
        if (ImGuiUtil.InputIntVertical(Language.Options_PreviewMinimum_Name, Language.Options_PreviewMinimum_Description, ref Mutable.PreviewMinimum))
            Mutable.PreviewMinimum = Math.Clamp(Mutable.PreviewMinimum, 1, 250);
        ImGui.Spacing();
        ImGuiUtil.OptionCheckbox(ref Mutable.OnlyPreviewIf, Language.Options_PreviewOnlyIf_Name, Language.Options_PreviewOnlyIf_Description);

        SettingsSections.Header("Emotes");
        Emotes.Draw(changed);
    }
}

public sealed class AdvancedPage : ISettingsTab
{
    private readonly Database Database;
    private readonly Webinterface Web;
    private readonly Changelog Changelog;

    public string Name => "Advanced###tabs-advanced";

    public AdvancedPage(Plugin plugin, Configuration mutable)
    {
        Mutable = mutable;
        Database = new Database(plugin, mutable);
        Web = new Webinterface(plugin, mutable);
        Changelog = new Changelog(mutable);
    }

    private Configuration Mutable { get; }

    public void Draw(bool changed)
    {
        using var wrap = ImRaii.TextWrapPos(0.0f);

        SettingsSections.Header("Commands");
        using (var combo = ImGuiUtil.BeginComboVertical(Language.Options_CommandHelpSide_Name, Mutable.CommandHelpSide.Name()))
        {
            if (combo.Success)
            {
                foreach (var side in Enum.GetValues<CommandHelpSide>())
                    if (ImGui.Selectable(side.Name(), Mutable.CommandHelpSide == side))
                        Mutable.CommandHelpSide = side;
            }
        }

        ImGuiUtil.HelpText(string.Format(Language.Options_CommandHelpSide_Description, Plugin.PluginName));
        ImGui.Spacing();

        using (var combo = ImGuiUtil.BeginComboVertical(Language.Options_KeybindMode_Name, Mutable.KeybindMode.Name()))
        {
            if (combo.Success)
            {
                foreach (var mode in Enum.GetValues<KeybindMode>())
                {
                    if (ImGui.Selectable(mode.Name(), Mutable.KeybindMode == mode))
                        Mutable.KeybindMode = mode;

                    if (ImGui.IsItemHovered())
                        ImGuiUtil.Tooltip(mode.Tooltip() ?? "");
                }
            }
        }

        ImGuiUtil.HelpText(string.Format(Language.Options_KeybindMode_Description, Plugin.PluginName));
        ImGui.Spacing();
        ImGui.Checkbox(Language.Options_SortAutoTranslate_Name, ref Mutable.SortAutoTranslate);
        ImGuiUtil.HelpText(Language.Options_SortAutoTranslate_Description);

        SettingsSections.Header("History");
        Database.Draw(changed);

        SettingsSections.Header("Browser");
        Web.Draw(changed);

        SettingsSections.Header("Changelog");
        Changelog.Draw(changed);
    }
}
