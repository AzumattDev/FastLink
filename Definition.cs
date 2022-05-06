using System.Collections.Generic;
using System.Linq;
using YamlDotNet.Serialization;

namespace FastLink;

public class Definition
{
	public string serverName = null!;

	public ushort port { get; set; }

	public string address { get; set; } = null!;
	
	public string password { get; set; } = "";

	public static IEnumerable<Definition> Parse(string yaml) => new DeserializerBuilder().IgnoreFields().Build().Deserialize<Dictionary<string, Definition>>(yaml).Select(kv =>
	{
		Definition def = kv.Value;
		def.serverName = kv.Key;
		return def;
	});

	public override string ToString() => $"Server(name={serverName},address={address},port={port})";
}
