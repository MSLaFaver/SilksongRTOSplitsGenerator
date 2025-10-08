using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using RandomToolOrder;

var tools = LoadTools();
var ordered = tools.ToList();

const int batchSize = 1000;
int attempts = 0;
bool valid = false;

while (attempts < batchSize)
{
	Shuffle(ordered);
	if (IsValidOrder(ordered))
	{
		valid = true;
		break;
	}
	attempts++;
}

if (!valid)
{
	Console.WriteLine($"Could not create a valid splits file after {attempts} attempts. Please try again.");
	return Exit(1);
}

var fileName = $"rto-{ordered.First().Name.Replace("'", string.Empty).Replace(' ', '-')}.lss";
var outPath = Path.Combine(Directory.GetCurrentDirectory(), fileName);

try
{
	File.WriteAllText(outPath, BuildRTOContent(ordered));
	Console.WriteLine($"Written: {outPath}");
}
catch (Exception ex)
{
	Console.Error.WriteLine(ex.Message);
	return Exit(1);
}

return Exit(0);

static List<Tool> LoadTools()
{
	var asm = Assembly.GetExecutingAssembly();
	var resourceName = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("Tools.json", StringComparison.OrdinalIgnoreCase)) ?? "";
	var json = "";

	using var s = asm.GetManifestResourceStream(resourceName);
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
					if (index.TryGetValue(option, out var pi) && pi < i)
					{
						clauseSatisfied = true;
						break;
					}
				}
				if (!clauseSatisfied)
				{
					return false;
				}
			}
		}

		var name = ordered[i].Name;
		index[name] = i;
	}

	return true;
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

static string BuildRTOContent(IList<Tool> ordered)
{
	var sb = new System.Text.StringBuilder();
	sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
	sb.AppendLine("<Run version=\"1.7.0\"> ");
	sb.AppendLine("\t<GameIcon />");
	sb.AppendLine("\t<GameName>Hollow Knight: Silksong</GameName>");
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
		var index = (i + 1).ToString("D2");
		var color = "";
		switch (t.Color)
		{
			case "red":
				color = "\x1b[91m";
				break;
			case "blue":
				color = "\x1b[94m";
				break;
			case "yellow":
				color = "\x1b[93m";
				break;
		}
		color = Console.IsOutputRedirected ? "" : color;
		string NORMAL = Console.IsOutputRedirected ? "" : "\x1b[39m";
		Console.WriteLine($"{index}. {color}{t.Name}{NORMAL}");

		var escaped = System.Security.SecurityElement.Escape(t.Name) ?? t.Name;
		sb.AppendLine("\t\t<Segment>");
		sb.AppendLine($"\t\t\t<Name>{escaped}</Name>");
		sb.AppendLine("\t\t\t<Icon />");
		sb.AppendLine("\t\t\t<SplitTimes>");
		sb.AppendLine("\t\t\t\t<SplitTime name=\"Personal Best\" />");
		sb.AppendLine("\t\t\t</SplitTimes>");
		sb.AppendLine("\t\t\t<BestSegmentTime />");
		sb.AppendLine("\t\t\t<SegmentHistory />");
		sb.AppendLine("\t\t</Segment>");
	}

	sb.AppendLine("\t</Segments>");
	sb.AppendLine("</Run>");
	return sb.ToString();
}

static int Exit(int code)
{
	try
	{
		if (!Console.IsInputRedirected && !Console.IsOutputRedirected)
		{
			Console.WriteLine();
			Console.WriteLine("Press any key to exit...");
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
		public List<List<string>>? Prerequisites { get; set; }
	}

	public class PrerequisitesConverter : JsonConverter<List<List<string>>?>
	{
		public override List<List<string>>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var result = new List<List<string>>();

			reader.Read();

			if (reader.TokenType == JsonTokenType.String)
			{
				var clause = new List<string>();
				do
				{
					clause.Add(reader.GetString() ?? string.Empty);
					reader.Read();
				}
				while (reader.TokenType == JsonTokenType.String);

				result.Add(clause);
			}
			else
			{
				while (reader.TokenType != JsonTokenType.EndArray)
				{
					var clause = new List<string>();
					reader.Read();
					while (reader.TokenType == JsonTokenType.String)
					{
						clause.Add(reader.GetString() ?? string.Empty);
						reader.Read();
					}
					result.Add(clause);
					reader.Read();
				}
			}

			return result;
		}

		public override void Write(Utf8JsonWriter writer, List<List<string>>? value, JsonSerializerOptions options)
		{

		}
	}
}

