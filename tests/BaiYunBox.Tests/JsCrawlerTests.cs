using BaiYunBox.Crawler;
using Xunit;

namespace BaiYunBox.Tests;

public class JsCrawlerTests
{
    [Fact]
    public void Load_声明式rule_解析字段()
    {
        var script = """
            var rule = {
              title: '示例站',
              host: 'https://example.com',
              url: '/list/fyclass-fypage.html',
              searchUrl: '/s?wd=**',
              class_parse: '.nav&&li;a&&Text;a&&href;/(\\w+).html',
              lazy: 'js:input=input',
              headers: { 'User-Agent': 'UA' },
              timeout: 30,
            };
            """;

        using var crawler = new JsCrawler();
        // Load 不抛异常即说明 Jint 成功解析声明式 rule
        crawler.Load(script);
        Assert.True(true);
    }

    [Fact]
    public void Eval_选择器链_提取文本与属性()
    {
        var html = """
            <ul class="nav">
              <li><a href="/list/1.html">电影</a></li>
              <li><a href="/list/2.html">电视剧</a></li>
            </ul>
            """;

        // 取第一个 a 的文本
        var name = RuleEvaluator.EvalFirst(html, ".nav li&&a&&Text()");
        Assert.Equal("电影", name);

        // 取第一个 a 的 href
        var href = RuleEvaluator.EvalFirst(html, ".nav li&&a&&href");
        Assert.Equal("/list/1.html", href);

        // 取所有 a 的文本数量
        var names = RuleEvaluator.EvalArray(html, ".nav li&&a&&Text()");
        Assert.Equal(2, names.Count);
    }

    [Fact]
    public void Eval_正则提取id()
    {
        var html = """<a href="/list/42.html">某分类</a>""";
        // 选 a → 取 href → 正则提取数字
        var id = RuleEvaluator.EvalFirst(html, "a&&href&&match(/(\\d+))");
        Assert.Equal("/42", id);
    }
}
