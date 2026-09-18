using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jint;
using Jint.Native;

namespace BaiYunBox.Crawler;

/// <summary>
/// dr_py / FongMi js0 声明式爬虫引擎。
/// 用 Jint 加载脚本（剥离 async/await、处理 export default），宿主语言解释 rule。
/// </summary>
public sealed class JsCrawler : IDisposable
{
    private readonly Engine _js;
    private readonly HttpClient _http;
    private readonly Dictionary<string, string> _cookies = new();
    private readonly Dictionary<string, string> _headers = new();
    private MubanObject? _muban;
    private CrawlerRule? _rule;

    private const string DefaultUa = "okhttp/3.12.11";
    private const string DefaultVodSelector = ".item, .module-item, .stui-vodlist__box";

    private static readonly Regex AsyncFnRe = new(@"\basync\s+(function\b)");
    private static readonly Regex AwaitRe = new(@"\bawait\s+");
    private static readonly Regex EsModuleRe = new(@"(?m)^\s*(import|export)\s");

    static JsCrawler()
    {
        // 注册 GBK 编码（dr_py 站点大量 gbk 编码）
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public JsCrawler()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _js = new Engine(options => options.LimitRecursion(64));
        InstallHelpers();
        InstallRule();
        InstallReq();
        InstallMuban();
    }

    // ---------- 加载 ----------

    /// <summary>执行爬虫脚本（声明式 rule 或 js0 函数定义）。</summary>
    public void Load(string src)
    {
        var normalized = NormalizeModuleSource(src);
        try
        {
            _js.Execute(normalized);
        }
        catch (Exception ex)
        {
            if (EsModuleRe.IsMatch(src))
                throw new InvalidOperationException("该站点使用 drpy2 / ES Module 脚本，暂不支持（仅支持 FongMi js0 / dr_py 声明式 rule 形态）。", ex);
            throw new InvalidOperationException("爬虫脚本执行失败: " + ex.Message, ex);
        }

        // export default → 复制导出的方法到全局
        var exports = _js.GetValue("__crawler_exports");
        if (!exports.IsUndefined() && !exports.IsNull() && exports.IsObject())
        {
            foreach (var key in exports.AsObject().GetOwnPropertyKeys())
            {
                var name = key.AsString();
                _js.SetValue(name, exports.AsObject().Get(key));
            }
        }

        _rule = ReadRule();
    }

    /// <summary>下载并加载爬虫脚本。</summary>
    public async Task LoadFromUrlAsync(string url)
    {
        var resp = await _http.GetAsync(url);
        resp.EnsureSuccessStatusCode();
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        var src = Encoding.UTF8.GetString(bytes);
        Load(src);
    }

    private static string NormalizeModuleSource(string src)
    {
        // req 是同步的，把 async/await 归约为普通函数。
        src = AsyncFnRe.Replace(src, "$1");
        src = AwaitRe.Replace(src, "");
        var idx = src.LastIndexOf("export default", StringComparison.Ordinal);
        if (idx >= 0)
        {
            src = src[..idx] + "var __crawler_exports = " + src[(idx + "export default".Length)..];
        }
        return src;
    }

    private CrawlerRule ReadRule()
    {
        var rule = new CrawlerRule();
        var v = _js.GetValue("rule");
        if (v.IsUndefined() || v.IsNull() || !v.IsObject())
            return rule;

        var obj = v.AsObject();
        rule.Title = Str(obj, "title");
        rule.Host = Str(obj, "host");
        rule.HomeUrl = Str(obj, "homeUrl");
        rule.SearchUrl = Str(obj, "searchUrl");
        rule.DetailUrl = Str(obj, "detailUrl");
        rule.PlayUrl = Str(obj, "playUrl");
        rule.Url = Str(obj, "url");
        rule.ClassParse = Str(obj, "class_parse");
        rule.Lazy = Str(obj, "lazy");
        rule.Timeout = Num(obj, "timeout");
        rule.ClassName = Str(obj, "class_name");
        rule.ClassUrl = Str(obj, "class_url");
        rule.VodSelector = Str(obj, "vod_selector");
        rule.NameSelector = Str(obj, "name_selector");
        rule.PicSelector = Str(obj, "pic_selector");
        rule.TypeSelector = Str(obj, "type_selector");
        rule.RemarksSelector = Str(obj, "remarks_selector");
        rule.IdSelector = Str(obj, "id_selector");
        rule.DetailNameSelector = Str(obj, "detail_name_selector");
        rule.DetailContentSelector = Str(obj, "detail_content_selector");
        rule.DetailYearSelector = Str(obj, "detail_year_selector");
        rule.DetailAreaSelector = Str(obj, "detail_area_selector");
        rule.PlaySelector = Str(obj, "play_selector");
        rule.PlayNameSelector = Str(obj, "play_name_selector");
        rule.PlayUrlSelector = Str(obj, "play_url_selector");

        var headers = obj.Get("headers");
        if (headers.IsObject())
        {
            foreach (var key in headers.AsObject().GetOwnPropertyKeys())
            {
                var hv = headers.AsObject().Get(key);
                if (!hv.IsUndefined()) rule.Headers[key.AsString()] = hv.AsString();
            }
        }

        foreach (var kind in new[] { "推荐", "一级", "二级", "搜索" })
        {
            var iv = obj.Get(kind);
            if (!iv.IsUndefined() && !iv.IsNull()) rule.Inline[kind] = iv.AsString();
        }

        return rule;
    }

    private static string Str(Jint.Native.Object.ObjectInstance obj, string name)
    {
        var v = obj.Get(name);
        return v.IsUndefined() || v.IsNull() ? "" : v.AsString();
    }

    private static long Num(Jint.Native.Object.ObjectInstance obj, string name)
    {
        var v = obj.Get(name);
        return v.IsNumber() ? (long)v.AsNumber() : 0;
    }

    private string? Inline(string kind) =>
        _rule != null && _rule.Inline.TryGetValue(kind, out var v) ? v : null;

    // ---------- 动作：Home / Classes / Category / Search / Detail / Play ----------

    public List<CrawlerVod> Home()
    {
        foreach (var name in new[] { "homeVideoContent", "homeVod", "home" })
            if (HasFunction(name)) return CallVods(name);

        var rule = RequireRule();
        var path = string.IsNullOrEmpty(rule.HomeUrl) ? "/" : rule.HomeUrl;
        if (!string.IsNullOrEmpty(rule.Url) || Inline("推荐") != null || Inline("一级") != null)
        {
            if (!string.IsNullOrEmpty(rule.Url)) path = FillUrl(rule.Url, "", 1);
            var html = FetchHtml(JoinUrl(rule.Host, path), rule);
            var kind = Inline("推荐") ?? "一级";
            return ExtractVods(kind, html, rule);
        }
        return FetchVods(JoinUrl(rule.Host, path), rule);
    }

    public List<CrawlerClass> Classes()
    {
        foreach (var name in new[] { "homeContent", "home" })
        {
            if (!HasFunction(name)) continue;
            var value = _js.Invoke(name, false);
            var json = ActionJson(value);
            var envelope = JsonSerializer.Deserialize<ClassEnvelope>(json);
            return envelope?.Class ?? new List<CrawlerClass>();
        }

        var rule = RequireRule();
        if (!string.IsNullOrEmpty(rule.ClassParse))
        {
            var path = string.IsNullOrEmpty(rule.HomeUrl) ? "/" : rule.HomeUrl;
            var html = FetchHtml(JoinUrl(rule.Host, path), rule);
            return ParseClasses(html, rule.ClassParse);
        }

        var names = rule.ClassName.Split('&');
        var ids = rule.ClassUrl.Split('&');
        var classes = new List<CrawlerClass>();
        for (var i = 0; i < names.Length; i++)
        {
            var name = names[i].Trim();
            if (name.Length == 0) continue;
            var id = i < ids.Length && !string.IsNullOrWhiteSpace(ids[i]) ? ids[i].Trim() : name;
            classes.Add(new CrawlerClass { TypeId = id, TypeName = name });
        }
        return classes;
    }

    public List<CrawlerVod> Category(string tid, int page)
    {
        foreach (var name in new[] { "categoryContent", "category", "categoryVod" })
            if (HasFunction(name)) return CallVods(name, tid, page.ToString(), false, null!);

        var rule = RequireRule();
        if (!string.IsNullOrEmpty(rule.Url))
        {
            var path = FillUrl(rule.Url, tid, page);
            var html = FetchHtml(JoinUrl(rule.Host, path), rule);
            return ExtractVods("一级", html, rule);
        }

        var paths = rule.ClassUrl.Split('&');
        var p = tid;
        if (int.TryParse(tid, out var n) && n >= 0 && n < paths.Length) p = paths[n];
        if (string.IsNullOrEmpty(p)) p = "/";
        if (page > 1) p = AddPage(p, page);
        return FetchVods(JoinUrl(rule.Host, p), rule);
    }

    public List<CrawlerVod> Search(string wd)
    {
        foreach (var name in new[] { "searchContent", "search", "searchVod" })
        {
            if (!HasFunction(name)) continue;
            if (name == "searchContent") return CallVods(name, wd, false);
            return CallVods(name, wd);
        }

        var rule = RequireRule();
        if (!string.IsNullOrEmpty(rule.SearchUrl) && (rule.SearchUrl.Contains("**") || Inline("搜索") != null))
        {
            var path = FillUrl(rule.SearchUrl, wd, 1);
            var html = FetchHtml(JoinUrl(rule.Host, path), rule);
            return ExtractVods("搜索", html, rule);
        }
        var p2 = rule.SearchUrl.Replace("{wd}", Uri.EscapeDataString(wd));
        if (!p2.Contains(Uri.EscapeDataString(wd))) p2 += Uri.EscapeDataString(wd);
        return FetchVods(JoinUrl(rule.Host, p2), rule);
    }

    public string Play(string flag, string id)
    {
        foreach (var name in new[] { "playerContent", "playContent", "play", "playVod" })
        {
            if (!HasFunction(name)) continue;
            var args = new List<JsValue> { flag, id };
            if (name == "playerContent") args.Add(JsValue.FromObject(_js, new string[0]));
            var value = _js.Invoke(name, args.ToArray());
            var json = ActionJson(value);
            var envelope = JsonSerializer.Deserialize<PlayEnvelope>(json);
            return envelope?.Url ?? "";
        }

        var rule = RequireRule();
        if (!string.IsNullOrWhiteSpace(rule.Lazy))
            return ResolveLazy(flag, id);

        if (!string.IsNullOrEmpty(rule.PlayUrl))
        {
            var path = FillUrl(rule.PlayUrl, id, 1).Replace("fyid", id).Replace("{id}", Uri.EscapeDataString(id));
            return JoinUrl(rule.Host, path);
        }
        return id;
    }

    public CrawlerDetail Detail(string id)
    {
        foreach (var name in new[] { "detailContent", "detail", "detailVod" })
        {
            if (!HasFunction(name)) continue;
            var value = name == "detailContent"
                ? _js.Invoke(name, JsValue.FromObject(_js, new[] { id }))
                : _js.Invoke(name, id);
            return ExportDetail(value);
        }

        var rule = RequireRule();
        if (!string.IsNullOrEmpty(rule.DetailUrl) || Inline("二级") != null)
        {
            var path = string.IsNullOrEmpty(rule.DetailUrl) ? id : rule.DetailUrl;
            path = FillUrl(path, DetailCategoryId(rule), 1).Replace("fyid", id).Replace("{id}", Uri.EscapeDataString(id));
            var target = JoinUrl(rule.Host, path);
            var inline = Inline("二级")?.Trim() ?? "";
            if (inline.StartsWith("js:"))
                return ExtractJsDetail(target, inline[3..].Trim(), rule, id);
            var html = FetchHtml(target, rule);
            return ExtractDetail(html, rule, id);
        }

        var p2 = rule.DetailUrl.Replace("{id}", Uri.EscapeDataString(id));
        if (!p2.Contains(Uri.EscapeDataString(id))) p2 += Uri.EscapeDataString(id);
        var html2 = FetchHtml(JoinUrl(rule.Host, p2), rule);
        return ExtractDetail(html2, rule, id);
    }

    // ---------- 内联提取 ----------

    private List<CrawlerVod> ExtractVods(string kind, string html, CrawlerRule rule)
    {
        var inline = Inline(kind)?.Trim() ?? "";
        if (inline.StartsWith("json:"))
            return ExtractJsonVods(html, inline);

        var effective = CloneRule(rule);
        ApplyMubanList(effective, kind);
        return ExtractHtmlVods(html, effective);
    }

    private CrawlerDetail ExtractDetail(string html, CrawlerRule rule, string id)
    {
        var inline = Inline("二级")?.Trim() ?? "";
        if (inline.StartsWith("json:"))
            return ExtractJsonDetail(html, inline[5..], id);

        var effective = CloneRule(rule);
        ApplyMubanDetail(effective);

        var detail = new CrawlerDetail { VodId = id };
        detail.VodName = FirstNonEmpty(RuleEvaluator.FirstValue(html, effective.DetailNameSelector), RuleEvaluator.FirstValue(html, effective.NameSelector));
        detail.VodPic = RuleEvaluator.FirstValue(html, effective.PicSelector);
        detail.VodRemarks = RuleEvaluator.FirstValue(html, effective.RemarksSelector);
        detail.TypeName = RuleEvaluator.FirstValue(html, effective.TypeSelector);
        detail.VodContent = RuleEvaluator.FirstValue(html, effective.DetailContentSelector);
        detail.VodYear = RuleEvaluator.FirstValue(html, effective.DetailYearSelector);
        detail.VodArea = RuleEvaluator.FirstValue(html, effective.DetailAreaSelector);
        (detail.VodPlayFrom, detail.VodPlayUrl) = ParsePlay(html, effective);
        if (string.IsNullOrEmpty(detail.VodName)) detail.VodName = id;
        return detail;
    }

    private CrawlerDetail ExtractJsDetail(string target, string script, CrawlerRule rule, string id)
    {
        var html = FetchHtml(target, rule);
        _js.SetValue("input", html);
        var vod = new Jint.Native.JsObject(_js);
        vod.Set("vod_id", id);
        _js.SetValue("VOD", vod);
        SetFetchParams(rule);
        var host = rule.Host;
        _js.SetValue("urljoin2", new Func<JsValue, JsValue, string>((a, b) =>
        {
            if (b.IsUndefined()) return a.AsString();
            return JoinUrl(host, a.AsString());
        }));
        _js.Execute(script);
        return ExportDetail(_js.GetValue("VOD"));
    }

    private List<CrawlerVod> ExtractJsonVods(string raw, string rule)
    {
        var parts = rule.Trim()[5..].Split(';');
        if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[0]))
            throw new InvalidOperationException("json 列表规则格式错误");

        using var doc = JsonDocument.Parse(raw);
        var value = NavigateJson(doc.RootElement, parts[0]);
        if (value == null || value.Value.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("json 列表路径不是数组: " + parts[0]);

        var vods = new List<CrawlerVod>();
        foreach (var item in value.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var vod = new CrawlerVod();
            foreach (var field in parts.Skip(1))
            {
                var name = field.Trim();
                if (name.Length == 0) continue;
                SetVodField(vod, name, EvalJsonExpr(item, name));
            }
            if (!string.IsNullOrEmpty(vod.VodId) || !string.IsNullOrEmpty(vod.VodName))
                vods.Add(vod);
        }
        return vods;
    }

    private CrawlerDetail ExtractJsonDetail(string raw, string rule, string id)
    {
        var parts = rule.Trim().Split(';');
        if (parts.Length < 2) throw new InvalidOperationException("json 详情规则格式错误");

        using var doc = JsonDocument.Parse(raw);
        var value = NavigateJson(doc.RootElement, parts[0]);
        if (value == null) throw new InvalidOperationException("json 详情路径不存在: " + parts[0]);

        var obj = value.Value.ValueKind == JsonValueKind.Object ? value.Value
            : (value.Value.ValueKind == JsonValueKind.Array ? value.Value.EnumerateArray().FirstOrDefault() : default);
        if (obj.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("json 详情路径不是对象");

        var detail = new CrawlerDetail { VodId = id };
        foreach (var field in parts.Skip(1))
        {
            var f = field.Trim();
            if (f.Length == 0) continue;
            SetDetailField(detail, f, EvalJsonExpr(obj, f));
        }
        return detail;
    }

    private List<CrawlerVod> ExtractHtmlVods(string html, CrawlerRule rule)
    {
        var selector = string.IsNullOrEmpty(rule.VodSelector) ? DefaultVodSelector : rule.VodSelector;
        var doc = RuleEvaluator.Parse(html);
        var vods = new List<CrawlerVod>();
        foreach (var item in doc.QuerySelectorAll(selector))
        {
            var itemHtml = item.OuterHtml;
            var id = RuleEvaluator.FirstValue(itemHtml, rule.IdSelector);
            var name = RuleEvaluator.FirstValue(itemHtml, rule.NameSelector);
            var pic = RuleEvaluator.FirstValue(itemHtml, rule.PicSelector);
            var remarks = RuleEvaluator.FirstValue(itemHtml, rule.RemarksSelector);
            var typeName = RuleEvaluator.FirstValue(itemHtml, rule.TypeSelector);
            if (string.IsNullOrEmpty(id)) id = item.QuerySelector("a")?.GetAttribute("href") ?? "";
            if (string.IsNullOrEmpty(name)) name = item.TextContent.Trim();
            if (!string.IsNullOrEmpty(id) || !string.IsNullOrEmpty(name))
                vods.Add(new CrawlerVod { VodId = id, VodName = name, VodPic = pic, TypeName = typeName, VodRemarks = remarks });
        }
        return vods;
    }

    private List<CrawlerVod> FetchVods(string target, CrawlerRule rule)
    {
        var html = FetchHtml(target, rule);
        return ExtractHtmlVods(html, rule);
    }

    private (string, string) ParsePlay(string html, CrawlerRule rule)
    {
        if (string.IsNullOrEmpty(rule.PlaySelector)) return ("", "");
        var doc = RuleEvaluator.Parse(html);
        var items = doc.QuerySelectorAll(rule.PlaySelector);
        var parts = new List<string>();
        foreach (var item in items)
        {
            var name = item.TextContent.Trim();
            if (!string.IsNullOrEmpty(rule.PlayNameSelector))
                name = RuleEvaluator.FirstValue(item.OuterHtml, rule.PlayNameSelector);
            var url = "";
            if (!string.IsNullOrEmpty(rule.PlayUrlSelector))
                url = RuleEvaluator.FirstValue(item.OuterHtml, rule.PlayUrlSelector);
            if (string.IsNullOrEmpty(url)) url = item.GetAttribute("href") ?? "";
            parts.Add(name + "$" + url);
        }
        return parts.Count == 0 ? ("", "") : ("线路", string.Join("#", parts));
    }

    // ---------- class_parse ----------

    private static List<CrawlerClass> ParseClasses(string html, string classParse)
    {
        var parts = classParse.Split(';');
        if (parts.Length != 4) throw new InvalidOperationException("class_parse 需要四段规则");
        var selectorRule = parts[0].Trim();
        var nameRule = parts[1].Trim();
        var idRule = parts[2].Trim();
        var idPattern = parts[3].Trim();
        if (selectorRule.Length == 0) throw new InvalidOperationException("class_parse 选择器不能为空");

        var doc = RuleEvaluator.Parse(html);
        var selection = SelectClassEntries(doc, selectorRule);
        Regex? re = null;
        if (idPattern.Length > 0) re = new Regex(idPattern);

        var classes = new List<CrawlerClass>();
        foreach (var item in selection)
        {
            var itemHtml = item.OuterHtml;
            var name = RuleEvaluator.FirstValue(itemHtml, NormalizeClassRule(nameRule));
            var id = RuleEvaluator.FirstValue(itemHtml, NormalizeClassRule(idRule));
            if (re != null)
            {
                var m = re.Match(id);
                id = m.Success && m.Groups.Count > 1 ? m.Groups[1].Value : (m.Success ? m.Value : "");
            }
            classes.Add(new CrawlerClass { TypeId = id, TypeName = name });
        }
        return classes;
    }

    private static List<AngleSharp.Dom.IElement> SelectClassEntries(AngleSharp.Html.Dom.IHtmlDocument doc, string rule)
    {
        var parts = rule.Split("&&");
        var selection = new List<AngleSharp.Dom.IElement> { doc.DocumentElement };
        foreach (var raw in parts)
        {
            var seg = raw.Trim();
            if (seg.Length == 0 || seg is "Text" or "Text()" or "Html" or "Html()" or "href" or "src" || seg.StartsWith("attr("))
                break;
            selection = RuleEvaluator.FindSelectionPublic(selection, seg);
        }
        return selection;
    }

    private static string NormalizeClassRule(string rule)
    {
        var parts = rule.Split("&&");
        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = parts[i].Trim() switch
            {
                "Text" => "Text()",
                "Html" => "Html()",
                _ => parts[i],
            };
        }
        return string.Join("&&", parts);
    }

    // ---------- lazy ----------

    private string ResolveLazy(string flag, string id)
    {
        var rule = RequireRule();
        var script = rule.Lazy.Trim();
        if (script.Length == 0) return id;
        if (script.StartsWith("js:")) script = script[3..].Trim();
        SetFetchParams(rule);

        var input = new Jint.Native.JsObject(_js);
        input.Set("flag", flag);
        input.Set("url", id);
        input.Set("split", JsValue.FromObject(_js, new Func<string, string[]>(sep => id.Split(sep))));
        _js.SetValue("input", input);

        _js.Execute(script);
        var value = _js.GetValue("input");
        if (value.IsObject())
        {
            var url = value.AsObject().Get("url");
            if (!url.IsUndefined() && !url.IsNull() && url.AsString().Length > 0)
                return url.AsString();
        }
        var s = value.IsString() ? value.AsString() : "";
        if (s.Length > 0) return s;
        throw new InvalidOperationException("lazy 规则未返回播放地址");
    }

    // ---------- 宿主函数 ----------

    private void InstallHelpers()
    {
        _js.SetValue("log", new Action<object[]>(args => { }));
        _js.SetValue("print", new Action<object[]>(args => { }));
        _js.SetValue("urljoin2", new Func<JsValue, JsValue, string>((a, b) =>
        {
            if (a.IsUndefined()) return "";
            if (b.IsUndefined() || b.IsNull()) return a.AsString();
            return JoinUrl(a.AsString(), b.AsString());
        }));
        _js.SetValue("urlDeal", new Func<JsValue, JsValue, string>((a, b) =>
            a.IsUndefined() ? "" : (b.IsUndefined() || b.IsNull() ? a.AsString() : JoinUrl(a.AsString(), b.AsString()))));
        _js.SetValue("base64Encode", new Func<string, string>(s => Convert.ToBase64String(Encoding.UTF8.GetBytes(s))));
        _js.SetValue("base64Decode", new Func<string, string>(s =>
        {
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(s)); } catch { return ""; }
        }));
        _js.SetValue("md5", new Func<string, string>(Md5Hash));
        _js.SetValue("fetch", new Func<JsValue, JsValue, object?>((url, opts) => FetchContent(url, opts)));
        _js.SetValue("request", new Func<JsValue, JsValue, object?>((url, opts) => FetchContent(url, opts)));
        _js.SetValue("cookie", new Func<JsValue, JsValue, object?>((k, v) =>
        {
            var key = k.AsString();
            if (v.IsUndefined()) return _cookies.TryGetValue(key, out var cv) ? cv : "";
            _cookies[key] = v.AsString();
            return null;
        }));
        _js.SetValue("header", new Func<JsValue, JsValue, object?>((k, v) =>
        {
            var key = k.AsString();
            if (v.IsUndefined()) return _headers.TryGetValue(key, out var hv) ? hv : "";
            _headers[key] = v.AsString();
            return null;
        }));
    }

    private void InstallRule()
    {
        _js.SetValue("pdfh", new Func<JsValue, JsValue, string>((html, rule) => RuleEvaluator.EvalFirst(html.AsString(), rule.AsString())));
        _js.SetValue("pdfa", new Func<JsValue, JsValue, string[]>((html, rule) => RuleEvaluator.EvalArray(html.AsString(), rule.AsString()).ToArray()));
        _js.SetValue("pd", new Func<JsValue, JsValue, JsValue, string>((html, rule, sep) =>
            RuleEvaluator.EvalJoin(html.AsString(), rule.AsString(), sep.IsUndefined() ? "" : sep.AsString())));
    }

    private void InstallReq()
    {
        _js.SetValue("req", new Func<JsValue, JsValue, object?>(DoRequest));
    }

    private void InstallMuban()
    {
        _muban = new MubanObject(_js);
        _js.SetValue("muban", _muban);
    }

    private object? DoRequest(JsValue urlArg, JsValue optsArg)
    {
        var url = urlArg.AsString();
        var method = "GET";
        var data = "";
        long timeoutMs = 0;
        Dictionary<string, string>? reqHeaders = null;

        if (!optsArg.IsUndefined() && !optsArg.IsNull() && optsArg.IsObject())
        {
            var obj = optsArg.AsObject();
            var m = obj.Get("method");
            if (!m.IsUndefined() && !m.IsNull()) method = m.AsString();
            var d = obj.Get("data");
            if (!d.IsUndefined() && !d.IsNull()) data = d.AsString();
            var t = obj.Get("timeout");
            if (t.IsNumber()) timeoutMs = (long)t.AsNumber();
            var h = obj.Get("headers");
            if (h.IsObject())
            {
                reqHeaders = new Dictionary<string, string>();
                foreach (var key in h.AsObject().GetOwnPropertyKeys())
                {
                    var hv = h.AsObject().Get(key);
                    if (!hv.IsUndefined()) reqHeaders[key.AsString()] = hv.AsString();
                }
            }
        }

        using var req = new HttpRequestMessage(new HttpMethod(method.ToUpperInvariant()), url);
        if (!string.IsNullOrEmpty(data))
            req.Content = new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded");

        foreach (var kv in _headers) req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
        if (reqHeaders != null)
            foreach (var kv in reqHeaders) req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
        if (!req.Headers.Contains("User-Agent"))
            req.Headers.TryAddWithoutValidation("User-Agent", DefaultUa);
        if (_cookies.Count > 0 && !req.Headers.Contains("Cookie"))
            req.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", _cookies.Select(kv => kv.Key + "=" + kv.Value)));

        using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeoutMs > 0 ? timeoutMs : 30000) };
        using var resp = client.Send(req);
        var bytes = resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
        var contentType = resp.Content.Headers.ContentType?.ToString() ?? "";

        var headers = new Dictionary<string, string>();
        foreach (var h in resp.Headers) headers[h.Key] = string.Join(", ", h.Value);

        return new Dictionary<string, object?>
        {
            ["content"] = DecodeBody(bytes, contentType),
            ["statusCode"] = (int)resp.StatusCode,
            ["finalUrl"] = resp.RequestMessage?.RequestUri?.ToString() ?? url,
            ["headers"] = headers,
        };
    }

    private object? FetchContent(JsValue url, JsValue opts)
    {
        var r = DoRequest(url, opts);
        return r is Dictionary<string, object?> d && d.TryGetValue("content", out var c) ? c : "";
    }

    private static string DecodeBody(byte[] bytes, string contentType)
    {
        var lower = contentType.ToLowerInvariant();
        var gbk = lower.Contains("gbk") || lower.Contains("gb2312");
        var utf8Declared = lower.Contains("utf-8") || lower.Contains("utf8");

        if (!gbk && utf8Declared)
            return Encoding.UTF8.GetString(bytes);

        // 尝试 UTF-8 严格解码
        try
        {
            var utf8 = new UTF8Encoding(false, true);
            return utf8.GetString(bytes);
        }
        catch
        {
            // 回退 GBK
            return Encoding.GetEncoding("GBK").GetString(bytes);
        }
    }

    private static string Md5Hash(string s)
    {
        var bytes = System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // ---------- 辅助 ----------

    private void SetFetchParams(CrawlerRule rule)
    {
        // fetch_params 供 js 片段读取，这里简化为空对象
        _js.SetValue("fetch_params", new Jint.Native.JsObject(_js));
    }

    private bool HasFunction(string name)
    {
        var v = _js.GetValue(name);
        return v.IsObject() && v.AsObject() is Jint.Native.Function.Function;
    }

    private List<CrawlerVod> CallVods(string name, params object[] args)
    {
        var jsArgs = args.Select(a => JsValue.FromObject(_js, a)).ToArray();
        var value = _js.Invoke(name, jsArgs);
        var json = ActionJson(value);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
            return JsonSerializer.Deserialize<List<CrawlerVod>>(json) ?? new();
        var envelope = JsonSerializer.Deserialize<ListEnvelope>(json);
        return envelope?.List ?? new();
    }

    private static string ActionJson(JsValue value)
    {
        if (value.IsUndefined() || value.IsNull())
            throw new InvalidOperationException("爬虫动作返回空值");
        if (value.IsString()) return value.AsString();
        return JsonSerializer.Serialize(value.ToObject());
    }

    private static CrawlerDetail ExportDetail(JsValue value)
    {
        var json = ActionJson(value);
        if (json.TrimStart().StartsWith("["))
        {
            var list = JsonSerializer.Deserialize<List<CrawlerDetail>>(json);
            if (list != null && list.Count > 0) return list[0];
        }
        var envelope = JsonSerializer.Deserialize<ListEnvelope>(json);
        if (envelope?.List != null && envelope.List.Count > 0)
        {
            var first = JsonSerializer.Deserialize<List<CrawlerDetail>>(JsonSerializer.Serialize(envelope.List));
            if (first != null && first.Count > 0) return first[0];
        }
        return JsonSerializer.Deserialize<CrawlerDetail>(json) ?? new CrawlerDetail();
    }

    private string FetchHtml(string target, CrawlerRule? rule)
    {
        var opts = new Jint.Native.JsObject(_js);
        if (rule != null)
        {
            if (rule.Headers.Count > 0)
            {
                var headers = new Jint.Native.JsObject(_js);
                foreach (var kv in rule.Headers) headers.Set(kv.Key, kv.Value);
                opts.Set("headers", headers);
            }
            if (rule.Timeout > 0) opts.Set("timeout", rule.Timeout);
        }
        var result = DoRequest(JsValue.FromObject(_js, target), opts);
        return result is Dictionary<string, object?> d && d.TryGetValue("content", out var c) ? (c as string ?? "") : "";
    }

    private CrawlerRule RequireRule() => _rule ?? throw new InvalidOperationException("爬虫未定义 rule 对象");

    private static string FillUrl(string tmpl, string tid, int pg)
    {
        return tmpl.Replace("fyclass", tid).Replace("fypage", pg.ToString()).Replace("**", Uri.EscapeDataString(tid));
    }

    private static string JoinUrl(string host, string path)
    {
        if (string.IsNullOrEmpty(host)) return path;
        if (Uri.TryCreate(new Uri(host), path, out var resolved)) return resolved.ToString();
        return path;
    }

    private static string AddPage(string path, int pg)
    {
        var sep = path.Contains('?') ? '&' : '?';
        return $"{path}{sep}page={pg}";
    }

    private static string DetailCategoryId(CrawlerRule rule)
    {
        foreach (var id in rule.ClassUrl.Split('&'))
        {
            var t = id.Trim();
            if (t.Length > 0) return t;
        }
        return "";
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var v in values)
            if (!string.IsNullOrWhiteSpace(v)) return v;
        return "";
    }

    private static CrawlerRule CloneRule(CrawlerRule src)
    {
        var r = new CrawlerRule
        {
            VodSelector = src.VodSelector, NameSelector = src.NameSelector, PicSelector = src.PicSelector,
            TypeSelector = src.TypeSelector, RemarksSelector = src.RemarksSelector, IdSelector = src.IdSelector,
            DetailNameSelector = src.DetailNameSelector, DetailContentSelector = src.DetailContentSelector,
            DetailYearSelector = src.DetailYearSelector, DetailAreaSelector = src.DetailAreaSelector,
            PlaySelector = src.PlaySelector, PlayNameSelector = src.PlayNameSelector, PlayUrlSelector = src.PlayUrlSelector,
        };
        return r;
    }

    private void ApplyMubanList(CrawlerRule rule, string kind)
    {
        if (_muban == null) return;
        foreach (var kv in _muban.Flatten())
        {
            var key = kv.Key;
            if (key.Contains("." + kind + "."))
                ApplyMubanField(rule, key[(key.LastIndexOf('.') + 1)..], kv.Value);
            else if (key.EndsWith("." + kind) && kv.Value is Dictionary<string, object?> sub)
                foreach (var f in sub) ApplyMubanField(rule, f.Key, f.Value);
        }
    }

    private void ApplyMubanDetail(CrawlerRule rule)
    {
        if (_muban == null) return;
        foreach (var kv in _muban.Flatten())
        {
            var key = kv.Key;
            if (!key.Contains(".二级.")) continue;
            var field = key[(key.LastIndexOf('.') + 1)..].ToLowerInvariant().Trim();
            var value = kv.Value?.ToString() ?? "";
            switch (field)
            {
                case "title": case "name": rule.DetailNameSelector = value; break;
                case "desc": case "description": case "content": rule.DetailContentSelector = value; break;
                case "pic": case "cover": case "image": rule.PicSelector = value; break;
                case "year": rule.DetailYearSelector = value; break;
                case "area": rule.DetailAreaSelector = value; break;
                case "type": case "type_name": rule.TypeSelector = value; break;
                case "remarks": case "remark": rule.RemarksSelector = value; break;
                case "play": case "play_selector": rule.PlaySelector = value; break;
                case "play_name": case "play_name_selector": rule.PlayNameSelector = value; break;
                case "play_url": case "play_url_selector": rule.PlayUrlSelector = value; break;
            }
        }
    }

    private static void ApplyMubanField(CrawlerRule rule, string field, object? raw)
    {
        var value = raw?.ToString() ?? "";
        switch (field.ToLowerInvariant().Trim())
        {
            case "vod": case "vod_selector": case "selector": case "list": rule.VodSelector = value; break;
            case "name": case "title": case "name_selector": rule.NameSelector = value; break;
            case "pic": case "cover": case "pic_selector": rule.PicSelector = value; break;
            case "id": case "id_selector": rule.IdSelector = value; break;
            case "type": case "type_selector": rule.TypeSelector = value; break;
            case "remarks": case "remark": case "remarks_selector": rule.RemarksSelector = value; break;
        }
    }

    // json: 规则辅助
    private static JsonElement? NavigateJson(JsonElement value, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return value;
        foreach (var seg in path.Split('.'))
        {
            var s = seg.Trim();
            if (s.Length == 0) continue;
            if (value.ValueKind != JsonValueKind.Object) return null;
            if (!value.TryGetProperty(s, out value)) return null;
        }
        return value;
    }

    private static string EvalJsonExpr(JsonElement obj, string expr)
    {
        var sb = new StringBuilder();
        foreach (var part in expr.Split('+'))
        {
            var val = "";
            foreach (var candidate in part.Split("||"))
            {
                var node = NavigateJson(obj, candidate.Trim());
                if (node != null)
                {
                    val = JsonScalar(node.Value);
                    if (val.Length > 0) break;
                }
            }
            sb.Append(val);
        }
        return sb.ToString();
    }

    private static string JsonScalar(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.String => v.GetString() ?? "",
        JsonValueKind.Number => v.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => "",
        _ => v.GetRawText(),
    };

    private static void SetVodField(CrawlerVod vod, string field, string value)
    {
        switch (CanonicalField(field))
        {
            case "title": case "name": case "vod_name": case "titletxt": case "titlealias": vod.VodName = value; break;
            case "cover": case "pic": case "image": case "vod_pic": vod.VodPic = value; break;
            case "id": case "url": case "vod_id": case "cat_id": case "en_id": vod.VodId = value; break;
            case "description": case "desc": case "content": case "vod_content": vod.VodContent = value; break;
            case "cat_name": case "type_name": case "type": vod.TypeName = value; break;
            case "remarks": case "remark": case "vod_remarks": vod.VodRemarks = value; break;
        }
    }

    private static void SetDetailField(CrawlerDetail detail, string field, string value)
    {
        switch (CanonicalField(field))
        {
            case "title": case "name": case "vod_name": detail.VodName = value; break;
            case "id": case "url": case "vod_id": detail.VodId = value; break;
            case "cover": case "pic": case "image": case "vod_pic": detail.VodPic = value; break;
            case "description": case "desc": case "content": case "vod_content": detail.VodContent = value; break;
            case "year": case "vod_year": detail.VodYear = value; break;
            case "area": case "vod_area": detail.VodArea = value; break;
            case "cat_name": case "type_name": case "type": detail.TypeName = value; break;
            case "remarks": case "remark": case "vod_remarks": detail.VodRemarks = value; break;
            case "play_from": case "vod_play_from": detail.VodPlayFrom = value; break;
            case "play_url": case "vod_play_url": detail.VodPlayUrl = value; break;
        }
    }

    private static string CanonicalField(string expr)
    {
        foreach (var part in expr.Split('+'))
        {
            foreach (var candidate in part.Split("||"))
            {
                var name = candidate.ToLowerInvariant().Trim();
                if (name.Length > 0) return name;
            }
        }
        return expr.ToLowerInvariant().Trim();
    }

    public void Dispose()
    {
        _http.Dispose();
    }

    // 信封模型
    private sealed class ClassEnvelope { public List<CrawlerClass>? Class { get; set; } }
    private sealed class ListEnvelope { public List<CrawlerVod>? List { get; set; } }
    private sealed class PlayEnvelope { public string? Url { get; set; } }
}
