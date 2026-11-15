namespace ChatTwo;

/// <summary>
/// Copies an existing ChatTwoVanilla config onto ChatSharp the first time this
/// identity loads. The old files are left untouched.
/// </summary>
internal static class ConfigMigration
{
    private const string OldName = "ChatTwoVanilla";
    private const string NewName = "ChatSharp";

    public static void CopyFromChatTwoVanilla()
    {
        try
        {
            var configDir = Plugin.Interface.ConfigDirectory;
            var parent = configDir.Parent?.FullName;
            if (string.IsNullOrEmpty(parent))
                return;

            var newJson = Path.Combine(parent, $"{NewName}.json");
            var oldJson = Path.Combine(parent, $"{OldName}.json");
            if (!File.Exists(newJson) && File.Exists(oldJson))
            {
                File.Copy(oldJson, newJson, overwrite: false);
                Plugin.Log.Information("Copied {Old} to {New}.", oldJson, newJson);
            }

            var oldDir = Path.Combine(parent, OldName);
            if (!Directory.Exists(oldDir))
                return;

            Directory.CreateDirectory(configDir.FullName);
            CopyMissing(oldDir, configDir.FullName);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Could not copy ChatTwoVanilla settings");
        }
    }

    private static void CopyMissing(string source, string dest)
    {
        foreach (var file in Directory.EnumerateFiles(source))
        {
            var target = Path.Combine(dest, Path.GetFileName(file));
            if (File.Exists(target))
                continue;

            try
            {
                File.Copy(file, target, overwrite: false);
                Plugin.Log.Information("Copied {Old} to {New}.", file, target);
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning(ex, "Could not copy {File}", file);
            }
        }

        foreach (var dir in Directory.EnumerateDirectories(source))
        {
            var target = Path.Combine(dest, Path.GetFileName(dir));
            Directory.CreateDirectory(target);
            CopyMissing(dir, target);
        }
    }
}
