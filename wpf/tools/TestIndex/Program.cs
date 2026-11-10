// testindex - deterministic Reqnroll/SpecFlow step index for LLM agents.
//
//   testindex [--root <repo>] build
//   testindex [--root <repo>] find "<query>" [-n 8] [--kind given|when|then]
//   testindex [--root <repo>] check <file.feature> [...] [-v]
//   testindex [--root <repo>] stats
//
// find/check rebuild the index automatically when any .cs/.feature file is newer than it.
// Regex bindings use .NET regex (same engine as Reqnroll). Cucumber expressions, custom
// parameter types via [StepArgumentTransformation], English + German Gherkin are supported.

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace TestIndex;

public class Binding
{
    public string Kind { get; set; }
    public string Expr { get; set; }
    public string Method { get; set; }
    public string Params { get; set; }
    public string Class { get; set; }
    public string File { get; set; }
    public int Line { get; set; }
    public int Used { get; set; }
    public string Mode { get; set; }
    [JsonIgnore] public Regex Rx { get; set; }
}

public class IndexData
{
    public string Root { get; set; }
    public int Features { get; set; }
    public int UnboundInExistingFeatures { get; set; }
    public Dictionary<string, string> Transforms { get; set; } = new();
    public List<Binding> Bindings { get; set; } = new();
}

public record Step(int Line, string Kind, string Text);

public static class Program
{
    static readonly HashSet<string> SkipDirs = new(StringComparer.OrdinalIgnoreCase)
        { "bin", "obj", "out", ".git", ".vs", ".idea", "node_modules", "packages", "TestResults", ".testindex" };

    // @"verbatim" or "regular" C# string literal
    const string Str = @"(?:@""((?:[^""]|"""")*)""|""((?:[^""\\]|\\.)*)"")";
    static readonly Regex AttrRe = new(@"[\[,]\s*(?:[\w.]+\.)?(Given|When|Then|StepDefinition)(?:Attribute)?\s*\(\s*" + Str);
    static readonly Regex TransRe = new(@"[\[,]\s*(?:[\w.]+\.)?StepArgumentTransformation(?:Attribute)?\s*\(\s*" + Str);
    static readonly Regex MethodRe = new(@"\b(?:public|internal|private|protected)\b[^;{}=]*?\b(\w+)\s*\(([^)]*)\)");
    static readonly Regex TransMethodRe = new(@"\b(?:public|internal|private|protected)\s+(?:static\s+)?(?:async\s+)?([\w<>\[\],.?]+?)\s+\w+\s*\(");
    static readonly Regex ClassRe = new(@"\bclass\s+(\w+)");
    static readonly Regex RegexHint = new(@"\(\.\*\)|\(\.\+\)|\\[dDwWsS]|\[[^\]]*\]|\.\*|\.\+|\(\?|\|");

    static readonly Dictionary<string, string> Param = new(StringComparer.OrdinalIgnoreCase)
    {
        ["int"] = @"(-?\d+)", ["long"] = @"(-?\d+)", ["short"] = @"(-?\d+)", ["byte"] = @"(\d+)",
        ["float"] = @"(-?\d*[.,]?\d+)", ["double"] = @"(-?\d*[.,]?\d+)", ["decimal"] = @"(-?\d*[.,]?\d+)",
        ["word"] = @"(\S+)", ["string"] = @"(?:""([^""]*)""|'([^']*)')", [""] = @"(.*)",
    };

    static readonly (string Kw, string Kind)[] Keywords = new (string, string)[]
    {
        ("Gegeben seien", "given"), ("Gegeben sei", "given"), ("Angenommen", "given"), ("Given", "given"),
        ("When", "when"), ("Wenn", "when"), ("Then", "then"), ("Dann", "then"),
        ("And", null), ("But", null), ("Und", null), ("Aber", null), ("*", null),
    }.OrderByDescending(k => k.Item1.Length).ToArray();

    static readonly Regex ScenarioRe = new(@"^(Scenario Outline|Scenario Template|Scenario|Example|Background|Rule|Szenariogrundriss|Szenarien|Szenario|Beispiel|Grundlage|Hintergrund|Voraussetzungen|Vorbedingungen|Regel)\s*:", RegexOptions.IgnoreCase);
    static readonly Regex ExamplesRe = new(@"^(Examples|Scenarios|Beispiele)\s*:", RegexOptions.IgnoreCase);

    static readonly JsonSerializerOptions Json = new() { WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static int Main(string[] argv)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var args = argv.ToList();
        string root = ".";
        int ri = args.IndexOf("--root");
        if (ri >= 0 && ri + 1 < args.Count) { root = args[ri + 1]; args.RemoveRange(ri, 2); }
        root = Path.GetFullPath(root);
        if (args.Count == 0) { Console.Error.WriteLine("usage: testindex [--root dir] build|find|check|stats ..."); return 2; }
        string idx = Path.Combine(root, ".testindex", "steps.json");
        string cmd = args[0];
        var rest = args.Skip(1).ToList();

        switch (cmd)
        {
            case "build":
            {
                var d = Build(root, idx);
                Console.WriteLine($"{d.Bindings.Count} bindings, {d.Transforms.Count} transformations, {d.Features} feature files -> {idx}");
                return 0;
            }
            case "stats":
            {
                var d = Load(root, idx);
                var bs = d.Bindings;
                Console.WriteLine("bindings: " + bs.Count + "  " + string.Join("  ", bs.GroupBy(b => b.Kind).Select(g => $"{g.Key}={g.Count()}")));
                Console.WriteLine("modes: " + string.Join("  ", bs.GroupBy(b => b.Mode).Select(g => $"{g.Key}={g.Count()}")));
                Console.WriteLine("transformations: " + string.Join(", ", d.Transforms.Keys));
                Console.WriteLine("unused bindings: " + bs.Count(b => b.Used == 0));
                Console.WriteLine("unbound steps in existing features: " + d.UnboundInExistingFeatures);
                foreach (var b in bs.Where(b => b.Mode == "invalid").Take(20)) Console.WriteLine("INVALID " + Fmt(b));
                return 0;
            }
            case "find":
            {
                if (rest.Count == 0) { Console.Error.WriteLine("find needs a query"); return 2; }
                int n = IntOpt(rest, "-n", 8);
                string kind = StrOpt(rest, "--kind");
                string query = string.Join(" ", rest);
                foreach (var b in Search(Load(root, idx).Bindings, query, n, kind)) Console.WriteLine(Fmt(b));
                return 0;
            }
            case "check":
            {
                bool verbose = rest.Remove("-v");
                if (rest.Count == 0) { Console.Error.WriteLine("check needs feature files"); return 2; }
                var bs = Load(root, idx).Bindings;
                int bad = 0, ok = 0;
                foreach (var f in rest)
                {
                    var path = Path.IsPathRooted(f) ? f : Path.Combine(Environment.CurrentDirectory, f);
                    if (!System.IO.File.Exists(path)) { Console.WriteLine($"MISSING   {f}"); bad++; continue; }
                    foreach (var s in ParseFeature(path))
                    {
                        var hits = Match(bs, s.Kind, s.Text);
                        if (hits.Count == 1)
                        {
                            ok++;
                            if (verbose) Console.WriteLine($"OK        {f}:{s.Line} {s.Text} -> {hits[0].Class}.{hits[0].Method}");
                        }
                        else if (hits.Count > 1)
                        {
                            bad++;
                            Console.WriteLine($"AMBIGUOUS {f}:{s.Line} {s.Text}");
                            foreach (var h in hits) Console.WriteLine("          " + Fmt(h));
                        }
                        else
                        {
                            bad++;
                            Console.WriteLine($"UNBOUND   {f}:{s.Line} [{s.Kind}] {s.Text}");
                            foreach (var h in Search(bs, s.Text, 3, s.Kind)) Console.WriteLine("  similar " + Fmt(h));
                        }
                    }
                }
                Console.WriteLine($"{ok} bound, {bad} problem step(s)");
                return bad > 0 ? 1 : 0;
            }
            default:
                Console.Error.WriteLine("unknown command: " + cmd);
                return 2;
        }
    }

    static int IntOpt(List<string> a, string name, int def)
    {
        int i = a.IndexOf(name);
        if (i < 0 || i + 1 >= a.Count || !int.TryParse(a[i + 1], out int v)) return def;
        a.RemoveRange(i, 2);
        return v;
    }

    static string StrOpt(List<string> a, string name)
    {
        int i = a.IndexOf(name);
        if (i < 0 || i + 1 >= a.Count) return null;
        var v = a[i + 1].ToLowerInvariant();
        a.RemoveRange(i, 2);
        return v;
    }

    // ---------- C# scanning ----------
    static string Unescape(Group verbatim, Group regular) =>
        verbatim.Success
            ? verbatim.Value.Replace("\"\"", "\"")
            : Regex.Replace(regular.Value, @"\\(.)", m => m.Groups[1].Value switch { "n" => "\n", "t" => "\t", var s => s });

    static int LineOf(string text, int pos)
    {
        int n = 1;
        for (int i = 0; i < pos; i++) if (text[i] == '\n') n++;
        return n;
    }

    static bool CommentedOut(string text, int pos)
    {
        int ls = pos == 0 ? 0 : text.LastIndexOf('\n', pos - 1) + 1;
        return text.Substring(ls, pos - ls).Contains("//");
    }

    static void ScanCs(string path, string rel, List<Binding> bindings, Dictionary<string, string> transforms)
    {
        string text = System.IO.File.ReadAllText(path);
        bool hasSteps = text.Contains("Given") || text.Contains("When") || text.Contains("Then") || text.Contains("StepDefinition");
        bool hasTrans = text.Contains("StepArgumentTransformation");
        if (!hasSteps && !hasTrans) return;
        var classes = ClassRe.Matches(text).Cast<Match>().ToList();

        if (hasSteps)
            foreach (Match m in AttrRe.Matches(text))
            {
                if (CommentedOut(text, m.Index)) continue;
                var mm = MethodRe.Match(text, m.Index + m.Length);
                var cls = classes.LastOrDefault(c => c.Index < m.Index);
                bindings.Add(new Binding
                {
                    Kind = m.Groups[1].Value == "StepDefinition" ? "any" : m.Groups[1].Value.ToLowerInvariant(),
                    Expr = Unescape(m.Groups[2], m.Groups[3]),
                    Method = mm.Success ? mm.Groups[1].Value : "?",
                    Params = mm.Success ? Regex.Replace(mm.Groups[2].Value, @"\s+", " ").Trim() : "",
                    Class = cls?.Groups[1].Value ?? "?",
                    File = rel,
                    Line = LineOf(text, m.Index),
                });
            }

        if (hasTrans)
            foreach (Match m in TransRe.Matches(text))
            {
                if (CommentedOut(text, m.Index)) continue;
                var mm = TransMethodRe.Match(text, m.Index + m.Length);
                if (!mm.Success) continue;
                var type = mm.Groups[1].Value.Split('.').Last().TrimEnd('?');
                var rx = Unescape(m.Groups[1], m.Groups[2]).TrimStart('^').TrimEnd('$'); // no kind group in TransRe
                transforms[type] = transforms.TryGetValue(type, out var prev) ? $"{prev}|{rx}" : rx;
            }
    }

    // ---------- expression -> regex ----------
    static string CukePiece(string s, Dictionary<string, string> transforms)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < s.Length;)
        {
            char c = s[i];
            int close;
            if (c == '\\' && i + 1 < s.Length) { sb.Append(Regex.Escape(s[i + 1].ToString())); i += 2; }
            else if (c == '{' && (close = s.IndexOf('}', i)) > i)
            {
                var name = s.Substring(i + 1, close - i - 1);
                if (Param.TryGetValue(name, out var p)) sb.Append(p);
                else if (transforms.TryGetValue(name, out var t)) sb.Append("(" + t + ")");
                else sb.Append("(.*)");
                i = close + 1;
            }
            else if (c == '(' && (close = s.IndexOf(')', i)) > i)
            {
                sb.Append("(?:" + CukePiece(s.Substring(i + 1, close - i - 1), transforms) + ")?");
                i = close + 1;
            }
            else { sb.Append(Regex.Escape(c.ToString())); i++; }
        }
        return sb.ToString();
    }

    static (string Rx, string Mode) ToRegex(string expr, Dictionary<string, string> transforms)
    {
        if (expr.StartsWith("^") || expr.EndsWith("$") || (!Regex.IsMatch(expr, @"\{\w*\}") && RegexHint.IsMatch(expr)))
            return ((expr.StartsWith("^") ? "" : "^") + expr + (expr.EndsWith("$") ? "" : "$"), "regex");
        var sb = new StringBuilder("^");
        foreach (var tok in Regex.Split(expr, @"(\s+)"))
        {
            if (tok.Length == 0) continue;
            if (string.IsNullOrWhiteSpace(tok)) { sb.Append(@"\s+"); continue; }
            var alts = tok.Contains('/') && !tok.Contains('{') ? tok.Split('/') : new[] { tok };
            var conv = alts.Select(a => CukePiece(a, transforms)).ToArray();
            sb.Append(conv.Length == 1 ? conv[0] : "(?:" + string.Join("|", conv) + ")");
        }
        return (sb.Append('$').ToString(), "cucumber");
    }

    static void Compile(List<Binding> bindings, Dictionary<string, string> transforms)
    {
        foreach (var b in bindings)
        {
            try
            {
                var (rx, mode) = ToRegex(b.Expr, transforms);
                b.Rx = new Regex(rx, RegexOptions.CultureInvariant);
                b.Mode = mode;
            }
            catch (ArgumentException) { b.Rx = null; b.Mode = "invalid"; }
        }
    }

    static List<Binding> Match(List<Binding> bs, string kind, string text) =>
        bs.Where(b => b.Rx != null && (b.Kind == kind || b.Kind == "any") && b.Rx.IsMatch(text)).ToList();

    // ---------- Gherkin ----------
    public static List<Step> ParseFeature(string path)
    {
        var lines = System.IO.File.ReadAllLines(path);
        var steps = new List<Step>();
        var block = new List<Step>();
        string lastKind = "given", inDoc = null;

        void Flush(List<string> header, List<string> row)
        {
            foreach (var s in block)
            {
                var t = s.Text;
                if (header != null && row != null)
                    for (int h = 0; h < Math.Min(header.Count, row.Count); h++) t = t.Replace("<" + header[h] + ">", row[h]);
                steps.Add(s with { Text = t });
            }
            block.Clear();
        }

        static List<string> Cells(string l) => l.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToList();

        for (int i = 0; i < lines.Length; i++)
        {
            var s = lines[i].Trim();
            if (inDoc != null) { if (s.StartsWith(inDoc)) inDoc = null; continue; }
            if (s.StartsWith("\"\"\"") || s.StartsWith("```")) { inDoc = s.Substring(0, 3); continue; }
            if (ScenarioRe.IsMatch(s)) { Flush(null, null); lastKind = "given"; continue; }
            if (ExamplesRe.IsMatch(s))
            {
                var rows = new List<List<string>>();
                int j = i + 1;
                for (; j < lines.Length; j++)
                {
                    var t = lines[j].Trim();
                    if (t.StartsWith("|")) rows.Add(Cells(t));
                    else if (t.Length > 0 && !t.StartsWith("#")) break;
                }
                Flush(rows.Count > 0 ? rows[0] : null, rows.Count > 1 ? rows[1] : null);
                i = j - 1;
                continue;
            }
            if (s.Length == 0 || s.StartsWith("#") || s.StartsWith("|") || s.StartsWith("@")) continue;
            foreach (var (kw, kind) in Keywords)
            {
                if (s.StartsWith(kw + " "))
                {
                    var k = kind ?? lastKind;
                    lastKind = k;
                    block.Add(new Step(i + 1, k, s.Substring(kw.Length).Trim()));
                    break;
                }
            }
        }
        Flush(null, null);
        return steps;
    }

    // ---------- index ----------
    static IEnumerable<string> Walk(string root, params string[] exts)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            IEnumerable<string> files, dirs;
            try { files = Directory.EnumerateFiles(dir); dirs = Directory.EnumerateDirectories(dir); }
            catch (UnauthorizedAccessException) { continue; }
            foreach (var f in files)
                if (exts.Any(e => f.EndsWith(e, StringComparison.OrdinalIgnoreCase))) yield return f;
            foreach (var d in dirs)
                if (!SkipDirs.Contains(Path.GetFileName(d))) stack.Push(d);
        }
    }

    static IndexData Build(string root, string idx)
    {
        var data = new IndexData { Root = root };
        foreach (var p in Walk(root, ".cs"))
            ScanCs(p, Path.GetRelativePath(root, p).Replace('\\', '/'), data.Bindings, data.Transforms);
        Compile(data.Bindings, data.Transforms);
        foreach (var p in Walk(root, ".feature"))
        {
            data.Features++;
            foreach (var s in ParseFeature(p))
            {
                var hit = data.Bindings.FirstOrDefault(b => b.Rx != null && (b.Kind == s.Kind || b.Kind == "any") && b.Rx.IsMatch(s.Text));
                if (hit != null) hit.Used++; else data.UnboundInExistingFeatures++;
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(idx));
        System.IO.File.WriteAllText(idx, JsonSerializer.Serialize(data, Json));
        return data;
    }

    static bool Stale(string root, string idx)
    {
        if (!System.IO.File.Exists(idx)) return true;
        var t = System.IO.File.GetLastWriteTimeUtc(idx);
        return Walk(root, ".cs", ".feature").Any(p => System.IO.File.GetLastWriteTimeUtc(p) > t);
    }

    static IndexData Load(string root, string idx)
    {
        if (Stale(root, idx)) return Build(root, idx);
        var d = JsonSerializer.Deserialize<IndexData>(System.IO.File.ReadAllText(idx), Json);
        Compile(d.Bindings, d.Transforms);
        return d;
    }

    // ---------- BM25 search ----------
    static List<string> Tokens(string s)
    {
        s = Regex.Replace(s, "([a-zäöü])([A-ZÄÖÜ])", "$1 $2").ToLowerInvariant();
        return Regex.Matches(s, "[a-zäöüß0-9]+").Select(m => m.Value).ToList();
    }

    static List<string> DocTokens(Binding b)
    {
        var text = Regex.Replace(b.Expr, @"\{\w*\}|\([^)]*\)|\\.|[\^$]", " ");
        var t = Tokens(text);
        return t.Concat(t).Concat(Tokens(b.Method)).Concat(Tokens(b.Class)).ToList();
    }

    static List<Binding> Search(List<Binding> bindings, string query, int n, string kind)
    {
        var docs = bindings.Where(b => kind == null || b.Kind == kind || b.Kind == "any")
            .Select(b => (B: b, C: DocTokens(b).GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count()))).ToList();
        if (docs.Count == 0) return new();
        double N = docs.Count, avg = docs.Average(d => d.C.Values.Sum());
        var df = new Dictionary<string, int>();
        foreach (var d in docs) foreach (var t in d.C.Keys) df[t] = df.GetValueOrDefault(t) + 1;
        var q = Tokens(query);
        var scored = new List<(double S, Binding B)>();
        foreach (var (b, c) in docs)
        {
            double dl = c.Values.Sum(), score = 0;
            foreach (var t in q)
                foreach (var x in c.Keys)
                {
                    bool exact = x == t;
                    bool partial = t.Length >= 4 && (x.StartsWith(t) || (x.Length >= 4 && t.StartsWith(x)));
                    if (!exact && !partial) continue;
                    double idf = Math.Log(1 + (N - df[x] + 0.5) / (df[x] + 0.5));
                    double tf = c[x];
                    score += idf * tf * 2.2 / (tf + 1.2 * (0.25 + 0.75 * dl / avg)) * (exact ? 1.0 : 0.6);
                }
            if (score > 0) scored.Add((score + 0.05 * Math.Log(1 + b.Used), b));
        }
        return scored.OrderByDescending(x => x.S).Take(n).Select(x => x.B).ToList();
    }

    static string Fmt(Binding b) =>
        $"{char.ToUpperInvariant(b.Kind[0])} \"{b.Expr}\" -> {b.Class}.{b.Method}({b.Params}) {b.File}:{b.Line} used={b.Used}";
}
