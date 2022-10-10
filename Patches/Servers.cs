using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;

namespace FastLink.Patches;

internal class Servers
{
    internal static string ConfigFileName = FastLinkPlugin.Author + "." +
                                            $"{FastLinkPlugin.ModName}_servers.yml";

    public static string ConfigPath = Paths.ConfigPath +
                                      Path.DirectorySeparatorChar + ConfigFileName;

    public static List<Definition> entries = new();

    public static void Init()
    {
        entries.Clear();
        try
        {
            if (!File.Exists(ConfigPath))
            {
                using StreamWriter streamWriter = File.CreateText(ConfigPath);
                streamWriter.Write(new StringBuilder()
                    .AppendLine("# Configure your servers for Azumatt's FastLink mod in this file.")
                    .AppendLine("# Servers are automatically sorted alphabetically when shown in the list.")
                    .AppendLine(
                        "# This file live updates the in-game listing. Feel free to change it while in the main menu.")
                    .AppendLine("")
                    .AppendLine("Example Server:")
                    .AppendLine("  address: example.com")
                    .AppendLine("  port: 1234")
                    .AppendLine("  password: somepassword")
                    .AppendLine("")
                    .AppendLine("Some IPv6 Server:")
                    .AppendLine("  address: 2606:2800:220:1:248:1893:25c8:1946")
                    .AppendLine("  port: 4023")
                    .AppendLine("  password: a password with spaces")
                    .AppendLine("")
                    .AppendLine("Passwordless IPv4 Server:")
                    .AppendLine("  address: 93.184.216.34")
                    .AppendLine("  port: 9999")
                    .AppendLine("")
                    .AppendLine(
                        "# You can optionally change the color of your server name. Does not work for the address and port. Also, can show PvP status.")
                    .AppendLine("<color=red>Another IPv4 Server</color>:")
                    .AppendLine("  address: 192.0.2.146")
                    .AppendLine("  port: 9999")
                    .AppendLine("  ispvp: true")
                    .AppendLine(
                        "# You can specify if a server is crossplay or not. This will show the crossplay's \"Shuffle\" icon. Please note, this is not fully supported at this time.")
                    .AppendLine("Crossplay Server:")
                    .AppendLine("  address: 92.183.211.42")
                    .AppendLine("  port: 9999")
                    .AppendLine("  password: somepassword")
                    .AppendLine("  iscrossplay: true")
                    .AppendLine("  ispvp: true"));
                streamWriter.Close();
            }

            if (File.Exists(ConfigPath))
            {
                entries.AddRange(Definition.Parse(File.ReadAllText(ConfigPath)));

                FastLinkPlugin.FastLinkLogger.LogDebug($"Loaded {entries.Count} server entries");
            }
        }
        catch (Exception ex)
        {
            FastLinkPlugin.FastLinkLogger.LogError($"Error loading config {ex}");
        }
    }
}