using RandomToolOrder;
using System.Drawing;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

bool DEBUG = false;

while (true)
{
	var tools = LoadTools().ToList();

	const int batchSize = 1000;
	int attempts = 0;
	bool valid = false;

	while (attempts < batchSize)
	{
		Shuffle(tools);
		if (IsValidOrder(tools))
		{
			valid = true;
			break;
		}
		attempts++;
	}

	if (!valid)
	{
		Console.WriteLine($"Could not create a valid splits file after {attempts} attempts. Please try again.");
		return Exit(1, DEBUG);
	}

	var content = BuildRTOContent(tools);
	PrintPrerequisites(tools);

	if (!DEBUG)
	{
		try
		{
			File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), $"rto-{tools.First().Name.Replace("'", string.Empty).Replace(' ', '-')}.lss"), content);
			Console.WriteLine($"\nWritten: {Path.Combine(Directory.GetCurrentDirectory(), $"rto-{tools.First().Name.Replace("'", string.Empty).Replace(' ', '-')}.lss")}");
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine(ex.Message);
			return Exit(1, DEBUG);
		}
	}
	else
	{
		Console.WriteLine($"\nTotal cost: {tools.Where(t => t.Cost.HasValue).Sum(t => t.Cost!.Value)} Rosaries");
	}

	var exit = Exit(0, DEBUG);

	if (!DEBUG)
	{
		return exit;
	}
}

static List<Tool> LoadTools()
{
	var asm = Assembly.GetExecutingAssembly();
	var resourceName = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("Tools.json", StringComparison.OrdinalIgnoreCase)) ?? "";
	var json = "";

	var s = asm.GetManifestResourceStream(resourceName);
	if (s != null)
	{
		using var sr = new StreamReader(s);
		json = sr.ReadToEnd();
	}

	return JsonSerializer.Deserialize<List<Tool>>(json, JsonOptions.Options) ?? [];
}

static bool IsValidOrder(IList<Tool> ordered)
{
	var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

	bool result = true;
	for (int i = 0; i < ordered.Count; i++)
	{
		var prereqs = ordered[i].Prerequisites;
		if (prereqs != null)
		{
			foreach (var clause in prereqs)
			{
				bool clauseSatisfied = false;
				foreach (var option in clause)
				{
					bool allAtomsValid = option.All(atom => AtomValid(atom, index, i));

					if (allAtomsValid)
					{
						clauseSatisfied = true;
						break;
					}
				}

				if (!clauseSatisfied)
				{
					result = false;
					break;
				}
			}

			if (!result)
				break;
		}

		var name = ordered[i].Name;
		index[name] = i;
	}

	return result;
}

static void Shuffle<T>(IList<T> list)
{
	var rng = new Random();
	for (int i = list.Count - 1; i > 0; i--)
	{
		int j = rng.Next(i + 1);
		(list[i], list[j]) = (list[j], list[i]);
	}
}

static string Color(string? color = null)
{
	if (Console.IsOutputRedirected)
		return string.Empty;

	return "\x1b[" + color?.ToLowerInvariant() switch
	{
		"red" => "91",
		"blue" => "94",
		"yellow" => "93",
		_ => "39",
	} + "m";
}

static string BuildRTOContent(IList<Tool> ordered)
{
	var sb = new System.Text.StringBuilder();
	sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
	sb.AppendLine("<Run version=\"1.7.0\"> ");
	sb.AppendLine("\t<GameIcon />");
	sb.AppendLine("\t<GameName>Hollow Knight: Silksong Category Extensions</GameName>");
	sb.AppendLine("\t<CategoryName>Random Tool Order</CategoryName>");
	sb.AppendLine("\t<LayoutPath>");
	sb.AppendLine("\t</LayoutPath>");
	sb.AppendLine("\t<Metadata>");
	sb.AppendLine("\t\t<Run id=\"\" />");
	sb.AppendLine("\t\t<Platform usesEmulator=\"False\">\t\t</Platform>");
	sb.AppendLine("\t\t<Region>\t\t</Region>");
	sb.AppendLine("\t\t<Variables />");
	sb.AppendLine("\t\t<CustomVariables />");
	sb.AppendLine("\t</Metadata>");
	sb.AppendLine("\t<Offset>00:00:00</Offset>");
	sb.AppendLine("\t<AttemptCount>0</AttemptCount>");
	sb.AppendLine("\t\t<AttemptHistory />");
	sb.AppendLine("\t<AutoSplitterSettings />");
	sb.AppendLine("\t<Segments>");

	for (int i = 0; i < ordered.Count; i++)
	{
		var t = ordered[i];
		Console.WriteLine($"{i + 1:D2}. {Color(t.Color)}{t.Name}{Color()}");

		sb.AppendLine("\t\t<Segment>");
		sb.AppendLine($"\t\t\t<Name>{System.Security.SecurityElement.Escape(t.Name) ?? t.Name}</Name>");

		var imgFileName = t.Name.Replace("'", string.Empty).Replace(' ', '_') + ".png";
		var asm = Assembly.GetExecutingAssembly();

		try
		{
			using var rs = asm.GetManifestResourceStream($"{asm.GetName().Name}.img.{imgFileName}");
			using var ms = new MemoryStream();
			new BinaryFormatter().Serialize(ms, Image.FromStream(rs!)); // BinaryFormatter is obsolete but kept for LiveSplit compatibility
			sb.AppendLine($"\t\t\t<Icon><![CDATA[{Convert.ToBase64String(ms.ToArray())}]]></Icon>");
		}
		catch
		{
			sb.AppendLine("\t\t\t<Icon />");
		}

		sb.AppendLine("\t\t\t<SplitTimes>");
		sb.AppendLine("\t\t\t\t<SplitTime name=\"Personal Best\" />");
		sb.AppendLine("\t\t\t</SplitTimes>");
		sb.AppendLine("\t\t\t<BestSegmentTime />");
		sb.AppendLine("\t\t\t<SegmentHistory />");
		sb.AppendLine("\t\t</Segment>");
	}

	sb.AppendLine("\t</Segments>");

	sb.AppendLine("\t<AutoSplitterSettings>");
	sb.AppendLine("\t\t<Version>1.0</Version>");
	sb.AppendLine("\t\t<CustomSettings>");
	sb.AppendLine("\t\t\t<Setting id=\"script_name\" type=\"string\" value=\"silksong_autosplit_wasm\"/>");
	sb.AppendLine("\t\t\t<Setting id=\"splits\" type=\"list\">");
	sb.AppendLine("\t\t\t\t<Setting type=\"string\" value=\"StartNewGame\"></Setting>");

	for (var i = 0; i < ordered.Count; i++)
	{
		sb.AppendLine("\t\t\t\t<Setting type=\"string\" value=\"ManualSplit\"></Setting>");
	}

	sb.AppendLine("\t\t\t</Setting>");
	sb.AppendLine("\t\t</CustomSettings>");
	sb.AppendLine("\t</AutoSplitterSettings>");

	sb.AppendLine("</Run>");

	return sb.ToString();
}

static bool AtomValid(string atomName, Dictionary<string, int> index, int i)
{
	bool valid;
	var isNeg = atomName.StartsWith('!');
	var key = isNeg ? atomName[1..] : atomName;
	if (index.TryGetValue(key, out var pos))
	{
		valid = (pos < i) ^ isNeg;
	}
	else
	{
		valid = isNeg;
	}
	return valid;
}

static void PrintPrerequisites(IList<Tool> ordered)
{
	var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
	for (int i = 0; i < ordered.Count; i++)
	{
		index[ordered[i].Name] = i;
	}

	Console.WriteLine("\nCheck prerequisites:");

	for (int i = 0; i < ordered.Count; i++)
	{
		var t = ordered[i];
		if (t.Prerequisites == null || t.Prerequisites.Count == 0)
		{
			continue;
		}

		Console.WriteLine($"\n{i + 1:D2}. {Color(t.Color)}{t.Name}{Color()}");

		foreach (var clause in t.Prerequisites)
		{
			var optionStrings = clause.Select(option =>
			{
				var atomStrings = option.Select(a =>
				{
					var displayName = a;
					var lookup = a.StartsWith('!') ? a[1..] : a;
					if (index.TryGetValue(lookup, out var refPos))
					{
						var refTool = ordered[refPos];
						displayName = $"{Color(refTool.Color)}{lookup}{Color()}";
					}

					return a.StartsWith('!') ? $"NOT {displayName}" : displayName;
				});

				var joined = string.Join(" AND ", atomStrings);
				return option.Count > 1 ? $"({joined})" : joined;
			});
			var opts = string.Join(" OR ", optionStrings);

			var satisfyingOption = clause.FirstOrDefault(option => option.All(atom => AtomValid(atom, index, i)));

			int repPos = -1;
			string repName = "";
			if (satisfyingOption != null)
			{
				var repAtom = satisfyingOption.FirstOrDefault(a =>
				{
					var isNeg = a.StartsWith('!');
					var lookup = isNeg ? a[1..] : a;
					if (!isNeg && index.TryGetValue(lookup, out var p) && p < i)
					{
						repPos = p;
						repName = lookup;
						return true;
					}
					return false;
				});
			}

			if (repPos >= 0 && satisfyingOption != null)
			{
				var parts = new List<string>();
				var repTool = ordered[repPos];
				var repDisplay = $"{(repPos + 1).ToString("D2")}. {Color(repTool.Color)}{repTool.Name}{Color()}";
				parts.Add(repDisplay);

				foreach (var a in satisfyingOption)
				{
					var lookup = a.StartsWith('!') ? a[1..] : a;
					if (string.Equals(lookup, repName, StringComparison.OrdinalIgnoreCase))
						continue;

					var pos2 = index[lookup];
					var refTool = ordered[pos2];
					parts.Add($"{(pos2 + 1).ToString("D2")}. {Color(refTool.Color)}{refTool.Name}{Color()}");
				}
				Console.WriteLine($"  [{opts}] -> {string.Join(" / ", parts)}");
			}
		}
	}
}

static int Exit(int code, bool debug)
{
	try
	{
		if (!Console.IsInputRedirected && !Console.IsOutputRedirected)
		{
			var message = debug ? "Press any key to continue . . .\n\n" : "Press any key to exit . . .";

			Console.WriteLine();
			Console.WriteLine(message);
			Console.ReadKey(true);
		}
	}
	catch
	{

	}

	return code;
}

namespace RandomToolOrder
{
	public static class JsonOptions
	{
		public static readonly JsonSerializerOptions Options;

		static JsonOptions()
		{
			Options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
			Options.Converters.Add(new PrerequisitesConverter());
		}
	}

	public class Tool
	{
		public string Name { get; set; } = string.Empty;
		public string Color { get; set; } = string.Empty;
		public List<List<List<string>>>? Prerequisites { get; set; }
		public int? Cost { get; set; }
	}

	public class PrerequisitesConverter : JsonConverter<List<List<List<string>>>?>
	{
		public override List<List<List<string>>>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var result = new List<List<List<string>>>();

			if (reader.TokenType != JsonTokenType.StartArray)
			{
				if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
				{
					return result;
				}
			}

			while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
			{
				if (reader.TokenType == JsonTokenType.String)
				{
					result.Add([[reader.GetString() ?? string.Empty]]);
				}
				else if (reader.TokenType == JsonTokenType.StartArray)
				{
					var clause = new List<List<string>>();
					while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
					{
						if (reader.TokenType == JsonTokenType.String)
						{
							clause.Add([reader.GetString() ?? string.Empty]);
						}
						else if (reader.TokenType == JsonTokenType.StartArray)
						{
							var option = new List<string>();
							while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
							{
								if (reader.TokenType == JsonTokenType.String)
								{
									option.Add(reader.GetString() ?? string.Empty);
								}
							}
							clause.Add(option);
						}
						else
						{
						}
					}
					result.Add(clause);
				}
				else
				{
				}
			}

			return result;
		}

		public override void Write(Utf8JsonWriter writer, List<List<List<string>>>? value, JsonSerializerOptions options)
		{

		}
	}
}