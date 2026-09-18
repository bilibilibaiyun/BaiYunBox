namespace BaiYunBox.Crawler;

/// <summary>dr_py 爬虫返回的影片条目（vod_* 字段对齐）。</summary>
public class CrawlerVod
{
    public string VodId { get; set; } = "";
    public string VodName { get; set; } = "";
    public string VodPic { get; set; } = "";
    public string TypeName { get; set; } = "";
    public string VodRemarks { get; set; } = "";
    public string VodContent { get; set; } = "";
}

/// <summary>dr_py 首页分类。</summary>
public sealed class CrawlerClass
{
    public string TypeId { get; set; } = "";
    public string TypeName { get; set; } = "";
}

/// <summary>dr_py 详情。</summary>
public sealed class CrawlerDetail : CrawlerVod
{
    public string VodYear { get; set; } = "";
    public string VodArea { get; set; } = "";
    public string VodPlayFrom { get; set; } = "";
    public string VodPlayUrl { get; set; } = "";
}

/// <summary>dr_py 声明式 rule 的常用字段。</summary>
public sealed class CrawlerRule
{
    public string Title { get; set; } = "";
    public string Host { get; set; } = "";
    public string HomeUrl { get; set; } = "";
    public string SearchUrl { get; set; } = "";
    public string DetailUrl { get; set; } = "";
    public string PlayUrl { get; set; } = "";
    public string Url { get; set; } = "";
    public string ClassParse { get; set; } = "";
    public string Lazy { get; set; } = "";
    public Dictionary<string, string> Headers { get; set; } = new();
    public long Timeout { get; set; }

    public string ClassName { get; set; } = "";
    public string ClassUrl { get; set; } = "";

    public string VodSelector { get; set; } = "";
    public string NameSelector { get; set; } = "";
    public string PicSelector { get; set; } = "";
    public string TypeSelector { get; set; } = "";
    public string RemarksSelector { get; set; } = "";
    public string IdSelector { get; set; } = "";

    public string DetailNameSelector { get; set; } = "";
    public string DetailContentSelector { get; set; } = "";
    public string DetailYearSelector { get; set; } = "";
    public string DetailAreaSelector { get; set; } = "";

    public string PlaySelector { get; set; } = "";
    public string PlayNameSelector { get; set; } = "";
    public string PlayUrlSelector { get; set; } = "";

    /// <summary>内联规则（一级/二级/搜索/推荐），从 rule 对象动态读取。</summary>
    public Dictionary<string, string> Inline { get; set; } = new();
}
