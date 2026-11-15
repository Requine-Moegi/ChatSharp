using System.Numerics;
using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;

namespace ChatTwo.Ui.SettingsTabs;

public sealed class About : ISettingsTab
{
    public string Name => string.Format(Language.Options_About_Tab, Plugin.PluginName) + "###tabs-about";

    private readonly List<string> Translators =
    [
        "q673135110", "Akizem", "d0tiKs",
        "Moonlight_Everlit", "Dark32", "andreycout",
        "Button_", "Cali666", "cassandra308",
        "lokinmodar", "jtabox", "AkiraYorumoto",
        "MKhayle", "elena.space", "imlisa",
        "andrei5125", "ShivaMaheshvara", "aislinn87",
        "nishinatsu051", "lichuyuan", "Risu64",
        "yummypillow", "witchymary", "Yuzumi",
        "zomsakura", "Sirayuki"
    ];

    public About()
    {
        Translators.Sort((a, b) => string.Compare(a.ToLowerInvariant(), b.ToLowerInvariant(), StringComparison.Ordinal));
    }

    public void Draw(bool changed)
    {
        using var wrap = ImRaii.TextWrapPos(0.0f);

        ImGui.TextUnformatted("Chat Sharp (C#) is the game's own chat window, with unlimited tabs.");

        ImGuiHelpers.ScaledDummy(10.0f);

        ImGui.TextUnformatted("Chat Sharp is a fork of Chat 2. The original plugin is by Anna (ascclemens) and Infi (Infiziert90), licensed under the EUPL-1.2.");

        ImGui.Spacing();
        DrawKofiButtons();

        ImGuiHelpers.ScaledDummy(10.0f);

        ImGui.TextUnformatted(Language.Options_About_Authors);
        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.ParsedGold, Plugin.Interface.Manifest.Author);

        ImGui.TextUnformatted(Language.Options_About_Version);
        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.ParsedOrange, Plugin.Interface.Manifest.AssemblyVersion.ToString(3));

        ImGuiHelpers.ScaledDummy(10.0f);

        ImGui.TextUnformatted(Language.Options_About_Github_Issues);
        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.ExternalLinkAlt, "githubIssues"))
            Dalamud.Utility.Util.OpenLink("https://github.com/Requine-Moegi/ChatTwoVanilla/issues");

        ImGui.Spacing();

        var height = ImGui.GetContentRegionAvail().Y - ImGui.CalcTextSize("A").Y - ImGui.GetStyle().ItemSpacing.Y * 2;
        using (var aboutChild = ImRaii.Child("about", new Vector2(-1, height)))
        {
            if (aboutChild)
            {
                using var treeNode = ImRaii.TreeNode(Language.Options_About_Translators);
                if (treeNode)
                {
                    using var translatorChild = ImRaii.Child("translators");
                    if (translatorChild)
                    {
                        foreach (var translator in Translators)
                            ImGui.TextUnformatted(translator);
                    }
                }
            }
        }
        ImGui.Spacing();
    }

    private static void DrawKofiButtons()
    {
        using (ImRaii.PushColor(ImGuiCol.Button, ColourUtil.RgbaToAbgr(0xFF5E5BFF)))
        using (ImRaii.PushColor(ImGuiCol.ButtonHovered, ColourUtil.RgbaToAbgr(0xFF7775FF)))
        using (ImRaii.PushColor(ImGuiCol.ButtonActive, ColourUtil.RgbaToAbgr(0xFF4542FF)))
        using (ImRaii.PushColor(ImGuiCol.Text, 0xFFFFFFFF))
        {
            if (ImGui.Button("Infi's Ko-fi"))
                Dalamud.Utility.Util.OpenLink("https://ko-fi.com/infiii");

            ImGui.SameLine();

            if (ImGui.Button("Anna's Ko-fi"))
                Dalamud.Utility.Util.OpenLink("https://ko-fi.com/lojewalo");
        }
    }
}
