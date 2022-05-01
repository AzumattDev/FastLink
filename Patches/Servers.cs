using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;

namespace FastLink.Patches
{
    internal class Servers
    {
        public static string ConfigPath = Paths.ConfigPath +
                                          Path.DirectorySeparatorChar + FastLinkPlugin.Author + "." +
                                          $"{FastLinkPlugin.ModName}_servers.cfg";

        public static List<Entry?> entries = new();

        public static void Init()
        {
            entries.Clear();
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    using StreamWriter streamWriter = File.CreateText(ConfigPath);
                    streamWriter.Write("# Config file for FastLink mod" + Environment.NewLine + Environment.NewLine +
                                       "# Lines starting with #, // and empty lines are ignored" +
                                       Environment.NewLine +
                                       "# Put one server per line" + Environment.NewLine + Environment.NewLine +
                                       "# name:addr:port[:password]" + Environment.NewLine +
                                       "# addr can be ether IP or a fully qualified domain name" +
                                       Environment.NewLine + Environment.NewLine +
                                       "Valheim Test:fastlink.us:2496:Uzc5cGee" + Environment.NewLine +
                                       Environment.NewLine +
                                       "# password is optional, you can skip it if your server doesn't need a password" +
                                       Environment.NewLine +
                                       "# or if you don't want to write it down" + Environment.NewLine +
                                       Environment.NewLine +
                                       "#No Password Server:127.0.0.1:2456:76453");
                    streamWriter.Close();
                }

                if (File.Exists(ConfigPath))
                {
                    using StreamReader streamReader = File.OpenText(ConfigPath);
                    string str1;
                    while ((str1 = streamReader.ReadLine()) != null)
                    {
                        string str2 = str1.Trim();
                        if (str2.Length == 0 || str2.StartsWith("#") || str2.StartsWith("//")) continue;
                        string[] strArray = str2.Split(':');
                        if (strArray.Length >= 3)
                        {
                            string str3 = strArray[0];
                            string str4 = strArray[1];
                            int num = int.Parse(strArray[2]);
                            string str5 = null;
                            if (strArray.Length >= 4)
                                str5 = strArray[3];
                            entries.Add(new Entry
                            {
                                m_name = str3,
                                m_ip = str4,
                                m_port = num,
                                m_pass = str5
                            });
                        }
                        else
                            FastLinkPlugin.FastLinkLogger.LogWarning("Invalid config line: " + str2);
                    }

                    FastLinkPlugin.FastLinkLogger.LogInfo($"Loaded {entries.Count} server entries");
                }
            }
            catch (Exception ex)
            {
                FastLinkPlugin.FastLinkLogger.LogError($"Error loading config {ex}");
            }
        }

        public class Entry
        {
            public string m_name;
            public string m_ip;
            public int m_port;
            public string? m_pass;

            public override string ToString() => $"Server(name={m_name},ip={m_ip},port={m_port})";
        }
    }
}