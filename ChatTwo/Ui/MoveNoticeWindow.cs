using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace ChatTwo.Ui;

public sealed class MoveNoticeWindow : Window
{
    public const string RepoUrl = "https://raw.githubusercontent.com/Requine-Moegi/MoegiPlugins/main/repo.json";

    public MoveNoticeWindow() : base("Chat Sharp has moved###chat2vanilla-move-notice")
    {
        Flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings;
        SizeCondition = ImGuiCond.Appearing;
        IsOpen = true;
    }

    public override void Draw()
    {
        using var wrap = ImRaii.TextWrapPos(ImGui.GetFontSize() * 36f);

        ImGui.TextUnformatted("Chat Sharp has moved to a new custom plugin repository. This is the last version under the old name.");
        ImGui.Spacing();
        ImGui.TextUnformatted("Add this address under Dalamud Settings > Experimental > Custom Plugin Repositories:");
        ImGui.Spacing();

        ImGui.TextUnformatted(RepoUrl);
        ImGui.SameLine();
        if (ImGui.Button("Copy"))
            ImGui.SetClipboardText(RepoUrl);

        ImGui.Spacing();
        ImGui.TextUnformatted("Install Chat Sharp from that list, then remove this old version. Your settings carry over automatically.");
        ImGui.Spacing();

        if (ImGui.Button("Close"))
            IsOpen = false;
    }

    public override void OnClose()
    {
        IsOpen = false;
        if (Plugin.Config.MoveNoticeDismissed)
            return;

        Plugin.Config.MoveNoticeDismissed = true;
        Plugin.Interface.SavePluginConfig(Plugin.Config);
    }
}
