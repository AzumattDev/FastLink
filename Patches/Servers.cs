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
                        .AppendLine("  port: 9999"));
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