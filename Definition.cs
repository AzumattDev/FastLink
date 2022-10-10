using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using YamlDotNet.Serialization;

namespace FastLink;

public class Definition
{
    public string serverName = null!;

    public ushort port { get; set; }

    public string address { get; set; } = null!;

    public bool ispvp { get; set; } = false;

    public bool iscrossplay { get; set; } = false;

    public string password { get; set; } = "";

    public static IEnumerable<Definition> Parse(string yaml) => new DeserializerBuilder().IgnoreFields().Build()
        .Deserialize<Dictionary<string, Definition>>(yaml).Select(kv =>
        {
            Definition def = kv.Value;
            def.serverName = kv.Key;
            return def;
        });
    public override string ToString()
    {
        StringBuilder text = new(256);
        text.Append(Environment.NewLine);
        text.Append($"Address: {address}");
        text.Append(Environment.NewLine);
        text.Append($"Port: {port}");
        text.Append(Environment.NewLine);
        text.Append($"PvP: {ispvp}");
        text.Append(Environment.NewLine);
        text.Append($"Crossplay Enabled: {iscrossplay}");
        if (FastLinkPlugin.ShowPasswordInTooltip.Value)
        {
            text.Append(Environment.NewLine);
            text.Append($"Password: {password}");
        }
        return text.ToString();
    }
}