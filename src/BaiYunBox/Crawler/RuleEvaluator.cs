using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace BaiYunBox.Crawler;

/// <summary>
/// dr_py 选择器引擎：执行 pdfh/pdfa/pd 的 && 链规则。
/// 用 AngleSharp 做 CSS 选择器，补充 jQuery 的 :lt/:eq 位置伪类与 Text/Html/attr/match/split/replace/substring 操作。
/// </summary>
public static class RuleEvaluator
{
    private static readonly Regex PositionalRe = new(
        @"^(.*):((?:lt|eq))\((-?\d+)\)$", RegexOptions.IgnoreCase);

    /// <summary>执行一条规则，返回全部命中值。</summary>
    public static List<string> Eval(string html, string rule)
    {
        var doc = Parse(html);
        var selection = new List<IElement> { doc.DocumentElement };
        var values = new List<string>();
        var stringMode = false;

        foreach (var raw in rule.Split("&&"))
        {
            var seg = raw.Trim();
            if (seg.Length == 0) continue;

            if (!stringMode)
            {
                switch (seg)
                {
                    case "Text":
                    case "Text()":
                        values = selection.Select(e => e.TextContent.Trim()).ToList();
                        stringMode = true;
                        continue;
                    case "Html":
                    case "Html()":
                        values = selection.Select(e => e.OuterHtml).ToList();
                        stringMode = true;
                        continue;
                    case "href":
                    case "src":
                        values = selection.Select(e => e.GetAttribute(seg) ?? "").ToList();
                        stringMode = true;
                        continue;
                    case "Array()":
                        values = selection.Select(e => e.TextContent.Trim()).ToList();
                        stringMode = true;
                        continue;
                }

                if (seg.StartsWith("attr(", StringComparison.Ordinal))
                {
                    var name = TrimParens(seg, "attr(");
                    values = selection.Select(e => e.GetAttribute(name) ?? "").ToList();
                    stringMode = true;
                    continue;
                }
                if (seg.StartsWith("match(", StringComparison.Ordinal))
                {
                    var pattern = TrimParens(seg, "match(");
                    values = selection.Select(e => Regex.Match(e.TextContent, pattern).Value).ToList();
                    stringMode = true;
                    continue;
                }
                if (seg.StartsWith("split(", StringComparison.Ordinal))
                {
                    var sep = TrimParens(seg, "split(");
                    values = selection.Select(e => e.TextContent.Split(sep)[0]).ToList();
                    stringMode = true;
                    continue;
                }

                // 否则当作 CSS 选择器
                selection = FindSelection(selection, seg);
                continue;
            }

            // stringMode：对 values 应用操作
            switch (seg)
            {
                case "trim":
                    for (var i = 0; i < values.Count; i++) values[i] = values[i].Trim();
                    break;
                case "ltrim":
                    for (var i = 0; i < values.Count; i++) values[i] = values[i].TrimStart();
                    break;
                case "rtrim":
                    for (var i = 0; i < values.Count; i++) values[i] = values[i].TrimEnd();
                    break;
                default:
                    if (seg.StartsWith("match(", StringComparison.Ordinal))
                    {
                        var pattern = TrimParens(seg, "match(");
                        for (var i = 0; i < values.Count; i++) values[i] = Regex.Match(values[i], pattern).Value;
                    }
                    else if (seg.StartsWith("split(", StringComparison.Ordinal))
                    {
                        var sep = TrimParens(seg, "split(");
                        for (var i = 0; i < values.Count; i++) values[i] = values[i].Split(sep)[0];
                    }
                    else if (seg.StartsWith("replace(", StringComparison.Ordinal))
                    {
                        var args = ParseCallArgs(seg);
                        if (args.Count >= 2)
                            for (var i = 0; i < values.Count; i++) values[i] = values[i].Replace(args[0], args[1]);
                    }
                    else if (seg.StartsWith("substring(", StringComparison.Ordinal))
                    {
                        var args = ParseCallArgs(seg);
                        var start = int.TryParse(ArgAt(args, 0), out var s) ? s : 0;
                        var end = args.Count > 1 && int.TryParse(args[1], out var e) ? e : -1;
                        for (var i = 0; i < values.Count; i++) values[i] = Substring(values[i], start, end);
                    }
                    break;
            }
        }

        if (stringMode) return values;
        return selection.Select(e => e.TextContent.Trim()).ToList();
    }

    /// <summary>返回规则命中值数组（pdfa）。</summary>
    public static List<string> EvalArray(string html, string rule) => Eval(html, rule);

    /// <summary>返回第一个命中值（pdfh）。</summary>
    public static string EvalFirst(string html, string rule)
    {
        var values = Eval(html, rule);
        return values.Count > 0 ? values[0] : "";
    }

    /// <summary>返回用分隔符拼接的值（pd）。</summary>
    public static string EvalJoin(string html, string rule, string separator = "")
    {
        var values = Eval(html, rule);
        return string.Join(separator, values);
    }

    /// <summary>从多个规则里取第一个非空命中值。</summary>
    public static string FirstValue(string html, params string[] rules)
    {
        foreach (var rule in rules)
        {
            if (string.IsNullOrEmpty(rule)) continue;
            var values = Eval(html, rule);
            if (values.Count > 0 && !string.IsNullOrWhiteSpace(values[0]))
                return values[0].Trim();
        }
        return "";
    }

    /// <summary>解析 HTML 为文档。</summary>
    public static IHtmlDocument Parse(string html)
    {
        return new HtmlParser().ParseDocument(html);
    }

    /// <summary>CSS 选择器 + :lt/:eq 位置伪类（供 class_parse 等复用）。</summary>
    public static List<IElement> FindSelectionPublic(List<IElement> selection, string selector)
        => FindSelection(selection, selector);

    /// <summary>CSS 选择器 + :lt/:eq 位置伪类。</summary>
    private static List<IElement> FindSelection(List<IElement> selection, string selector)
    {
        var m = PositionalRe.Match(selector);
        if (m.Success)
        {
            var baseSel = m.Groups[1].Value.Trim();
            var op = m.Groups[2].Value.ToLowerInvariant();
            var index = int.Parse(m.Groups[3].Value);

            var baseResult = new List<IElement>();
            foreach (var parent in selection)
            {
                foreach (var el in parent.QuerySelectorAll(baseSel))
                    baseResult.Add(el);
            }

            if (op == "lt")
            {
                if (index < 0) index = 0;
                if (index > baseResult.Count) index = baseResult.Count;
                return baseResult.Take(index).ToList();
            }
            // eq
            if (index < 0) index = baseResult.Count + index;
            if (index < 0 || index >= baseResult.Count) return new List<IElement>();
            return new List<IElement> { baseResult[index] };
        }

        var result = new List<IElement>();
        foreach (var parent in selection)
        {
            foreach (var el in parent.QuerySelectorAll(selector))
                result.Add(el);
        }
        return result;
    }

    private static string TrimParens(string seg, string prefix)
    {
        var inner = seg[prefix.Length..];
        // 只去掉最后一个 ')'（正则内部可能含 ')'，不能用 TrimEnd 全删）
        var lastParen = inner.LastIndexOf(')');
        if (lastParen >= 0) inner = inner[..lastParen];
        return inner.Trim('"', '\'', ' ');
    }

    private static List<string> ParseCallArgs(string seg)
    {
        var open = seg.IndexOf('(');
        var inner = seg[(open + 1)..].TrimEnd(')');
        return inner.Split(',').Select(a => a.Trim().Trim('"', '\'')).ToList();
    }

    private static string ArgAt(List<string> args, int i) => i < args.Count ? args[i] : "0";

    private static string Substring(string s, int start, int end)
    {
        var runes = s.ToCharArray();
        if (end < 0) end = runes.Length;
        if (start < 0) start = 0;
        if (start > runes.Length) start = runes.Length;
        if (end < start) end = start;
        if (end > runes.Length) end = runes.Length;
        return new string(runes, start, end - start);
    }
}
