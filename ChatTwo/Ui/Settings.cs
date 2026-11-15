using System.Numerics;
using ChatTwo.Resources;
using ChatTwo.Ui.SettingsTabs;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using Dalamud.Bindings.ImGui;

namespace ChatTwo.Ui;

public sealed class SettingsWindow : Window
{
    private readonly Plugin Plugin;

    private Configuration Mutable { get; }
    private List<ISettingsTab> Tabs { get; }
    private int CurrentTab;

    // PopOut on each tab when this draft was taken. A matching value means the checkbox
    // was not edited, so a pop-out opened afterwards must be kept.
    private readonly Dictionary<Guid, bool> PopOutAtDraft = new();

    public SettingsWindow(Plugin plugin) : base($"{Language.Settings_Title.Format(Plugin.PluginName)}###chat2vanilla-settings")
    {
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(475, 600),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;
        Mutable = new Configuration();

        Tabs =
        [
            new GeneralPage(Mutable),
            new AppearancePage(Plugin, Mutable),
            new TabsPage(Plugin, Mutable),
            new PopoutsPage(Mutable),
            new ChatBehaviorPage(Plugin, Mutable),
            new AdvancedPage(Plugin, Mutable),
            new About(),
        ];

        RespectCloseHotkey = false;
        DisableWindowSounds = true;

        Initialise();

        Plugin.Commands.Register(Plugin.MainCommand, "Open Chat Sharp settings. /chat2vanilla still works.").Execute += Command;
        Plugin.Commands.Register(Plugin.LegacyMainCommand, "Alias of /chatsharp.").Execute += Command;
        Plugin.Interface.UiBuilder.OpenConfigUi += Toggle;
    }

    public void Dispose()
    {
        Plugin.Interface.UiBuilder.OpenConfigUi -= Toggle;
        Plugin.Commands.Register(Plugin.MainCommand).Execute -= Command;
        Plugin.Commands.Register(Plugin.LegacyMainCommand).Execute -= Command;
    }

    private void Command(string command, string args)
    {
        if (string.IsNullOrWhiteSpace(args))
            Toggle();
    }

    private void Initialise()
    {
        Mutable.UpdateFrom(Plugin.Config, false);
        RememberPopOutDraft();
    }

    private void RememberPopOutDraft()
    {
        PopOutAtDraft.Clear();
        foreach (var tab in Mutable.Tabs)
            PopOutAtDraft[tab.Identifier] = tab.PopOut;
    }

    private void CarryLivePopOuts()
    {
        foreach (var tab in Mutable.Tabs)
        {
            var live = Plugin.Config.Tabs.Find(candidate => candidate.Identifier == tab.Identifier);
            if (live == null)
                continue;

            if (PopOutAtDraft.TryGetValue(tab.Identifier, out var atDraft) && tab.PopOut == atDraft)
                tab.PopOut = live.PopOut;

            if (!live.HasPopOutBounds)
                continue;

            tab.HasPopOutBounds = true;
            tab.PopOutX = live.PopOutX;
            tab.PopOutY = live.PopOutY;
            tab.PopOutW = live.PopOutW;
            tab.PopOutH = live.PopOutH;
        }
    }

    public override void Draw()
    {
        if (ImGui.IsWindowAppearing())
            Initialise();

        using (var table = ImRaii.Table("##chat2-settings-table", 2))
        {
            if (table.Success)
            {
                ImGui.TableSetupColumn("tab", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("settings", ImGuiTableColumnFlags.WidthStretch);

                ImGui.TableNextColumn();

                var changed = false;
                for (var i = 0; i < Tabs.Count; i++)
                {
                    if (!ImGui.Selectable($"{Tabs[i].Name}###tab-{i}", CurrentTab == i))
                        continue;

                    CurrentTab = i;
                    changed = true;
                }

                ImGui.TableNextColumn();

                var style = ImGui.GetStyle();
                var height = ImGui.GetContentRegionAvail().Y - style.FramePadding.Y * 2 - style.ItemSpacing.Y - style.ItemInnerSpacing.Y * 2 - ImGui.CalcTextSize("A").Y;

                using var child = ImRaii.Child("##chat2-settings", new Vector2(-1, height));
                if (child.Success)
                    Tabs[CurrentTab].Draw(changed);
            }
        }

        ImGui.Separator();

        var save = ImGui.Button(Language.Settings_Save);

        ImGui.SameLine();

        if (ImGui.Button(Language.Settings_SaveAndClose)) {
            save = true;
            IsOpen = false;
        }

        ImGui.SameLine();

        if (ImGui.Button(Language.Settings_Discard)) {
            IsOpen = false;
        }

        GameChatTabImportUi.DrawModal(Mutable);

        if (!save)
            return;

        // calculate all conditions before updating config
        var hideChanged = !Mutable.HideChat && Mutable.HideChat != Plugin.Config.HideChat;
        var languageChanged = Mutable.LanguageOverride != Plugin.Config.LanguageOverride;
        var fontSizeChanged = Math.Abs(Mutable.FontSizeV2 - Plugin.Config.FontSizeV2) > 0.001;

        CarryLivePopOuts();
        Plugin.Config.UpdateFrom(Mutable, true);
        Plugin.ChatLog.RebindPopOuts();

        // save after 60 frames have passed, which should hopefully not
        // commit any changes that cause a crash
        Plugin.DeferredSaveFrames = 60;
        Plugin.MessageManager.ClearAllTabs();
        Plugin.MessageManager.FilterAllTabsAsync();

        if (fontSizeChanged)
            Plugin.FontManager.BuildFonts();

        if (languageChanged)
            Plugin.LanguageChanged(Plugin.Interface.UiLanguage);

        if (hideChanged)
            GameFunctions.GameFunctions.SetChatInteractable(true);

        if (Plugin.Config.ShowEmotes)
            Task.Run(EmoteCache.LoadData);

        Initialise();
    }
}
