using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

var tools = LoadTools() ?? new List<Tool>();
if (tools.Count == 0)
{
	Console.Error.WriteLine("No tools available.");
	return Exit(1);
}

var rng = new Random();
var ordered = tools.ToList();

const int batchSize = 1000;
int attempts = 0;
bool valid = false;
while (true)
{
	int target = attempts + batchSize;
	while (attempts < target)
	{
		attempts++;
		Shuffle(ordered, rng);
		if (IsValidOrder(ordered))
		{
			valid = true;
			break;
		}
	}

	if (valid) break;

	Console.WriteLine($"Warning: no valid ordering found after {attempts} attempts.");
	Console.Write($"Continue randomizing for another {batchSize} attempts? (Y/Enter = continue, N = quit): ");
	var input = Console.ReadLine();
	if (input == null)
	{
		Console.WriteLine("No input received. Quitting.");
		return Exit(1);
	}
	input = input.Trim();
	if (input.Equals("N", StringComparison.OrdinalIgnoreCase))
	{
		Console.WriteLine("Quitting without finding a valid ordering.");
		return Exit(1);
	}
	Console.WriteLine("Continuing...\n");
}

for (int i = 0; i < ordered.Count; i++)
{
	var t = ordered[i];
	var index = (i + 1).ToString("D2");
	Console.WriteLine($"{index}. {t.Name} ({t.Color})");
}

try
{
	var first = ordered.First();
	var safe = MakeSafeFileName(first.Name);
	var fileName = $"rto-{safe}.lss";
	var content = BuildRtoContent(ordered);
	var outDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
	var outPath = Path.Combine(outDir, fileName);
	File.WriteAllText(outPath, content);
	Console.WriteLine($"Written: {outPath}");
}
catch (Exception ex)
{
	Console.Error.WriteLine(ex.Message);
	return Exit(1);
}

return Exit(0);

static List<Tool>? LoadTools()
{
	var asm = Assembly.GetExecutingAssembly();
	var resourceName = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("Tools.json", StringComparison.OrdinalIgnoreCase));
	string? json = null;

	if (resourceName != null)
	{
		using var s = asm.GetManifestResourceStream(resourceName);
		if (s != null)
		{
			using var sr = new StreamReader(s);
			json = sr.ReadToEnd();
		}
	}

	if (json == null && File.Exists("Tools.json"))
		json = File.ReadAllText("Tools.json");

	if (string.IsNullOrWhiteSpace(json)) return null;

	var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
	opts.Converters.Add(new PrerequisitesConverter());
	try
	{
		return JsonSerializer.Deserialize<List<Tool>>(json, opts);
	}
	catch
	{
		return null;
	}
}

static string MakeSafeFileName(string name)
{
	var safe = name.Replace("'", string.Empty).Replace(' ', '-');
	foreach (var c in Path.GetInvalidFileNameChars()) safe = safe.Replace(c.ToString(), string.Empty);
	return string.IsNullOrWhiteSpace(safe) ? "tool" : safe;
}

static bool IsValidOrder(IList<Tool> ordered)
{
	var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
	for (int i = 0; i < ordered.Count; i++) index[ordered[i].Name] = i;
	for (int i = 0; i < ordered.Count; i++)
	{
			var prereqs = ordered[i].Prerequisites;
			if (prereqs == null) continue;
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
				if (!clauseSatisfied) return false;
			}
	}
	return true;
}

static void Shuffle<T>(IList<T> list, Random rng)
{
	for (int i = list.Count - 1; i > 0; i--)
	{
		int j = rng.Next(i + 1);
		(list[i], list[j]) = (list[j], list[i]);
	}
}

 

static string BuildRtoContent(IList<Tool> ordered)
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
	foreach (var t in ordered)
	{
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
		if (reader.TokenType == JsonTokenType.Null) return null;

		var result = new List<List<string>>();

		if (reader.TokenType != JsonTokenType.StartArray)
			throw new JsonException("Expected start of array for prerequisites");

		reader.Read();

		if (reader.TokenType == JsonTokenType.String)
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

			if (reader.TokenType != JsonTokenType.EndArray)
				throw new JsonException("Expected end of array after prerequisites strings");

			return result;
		}
        
		while (reader.TokenType != JsonTokenType.EndArray)
		{
			if (reader.TokenType != JsonTokenType.StartArray)
				throw new JsonException("Expected start of inner array in prerequisites");

			var clause = new List<string>();
			reader.Read();
			while (reader.TokenType == JsonTokenType.String)
			{
				clause.Add(reader.GetString() ?? string.Empty);
				reader.Read();
			}

			if (reader.TokenType != JsonTokenType.EndArray)
				throw new JsonException("Expected end of inner array in prerequisites");

			result.Add(clause);
			reader.Read();
		}

		return result;
	}

	public override void Write(Utf8JsonWriter writer, List<List<string>>? value, JsonSerializerOptions options)
	{
		if (value == null)
		{
			writer.WriteNullValue();
			return;
		}

		writer.WriteStartArray();
		foreach (var clause in value)
		{
			writer.WriteStartArray();
			foreach (var s in clause)
			{
				writer.WriteStringValue(s);
			}
			writer.WriteEndArray();
		}
		writer.WriteEndArray();
	}
}

