using BaiYunBox.Models;
using BaiYunBox.Services;
using Xunit;

namespace BaiYunBox.Tests;

public class PlayLineParsingTests
{
    [Fact]
    public void ParsePlayLines_单线路_解析剧集()
    {
        var lines = VodService.ParsePlayLines("线路1", "第1集$http://a/1.m3u8#第2集$http://a/2.m3u8");

        Assert.Single(lines);
        Assert.Equal("线路1", lines[0].Name);
        Assert.Equal(2, lines[0].Episodes.Count);
        Assert.Equal("第1集", lines[0].Episodes[0].Title);
        Assert.Equal("http://a/1.m3u8", lines[0].Episodes[0].Url);
    }

    [Fact]
    public void ParsePlayLines_多线路_按线路分组()
    {
        var lines = VodService.ParsePlayLines(
            "线路1$$$线路2",
            "第1集$http://a/1.m3u8#第2集$http://a/2.m3u8$$$第1集$http://b/1.m3u8");

        Assert.Equal(2, lines.Count);
        Assert.Equal("线路1", lines[0].Name);
        Assert.Equal("线路2", lines[1].Name);
        Assert.Equal(2, lines[0].Episodes.Count);
        Assert.Single(lines[1].Episodes);
        Assert.Equal("http://b/1.m3u8", lines[1].Episodes[0].Url);
    }

    [Fact]
    public void ParsePlayLines_空url_返回空()
    {
        var lines = VodService.ParsePlayLines("", "");
        Assert.Empty(lines);
    }
}

public class LiveParsingTests
{
    [Fact]
    public void ParseM3u_解析分组与频道()
    {
        var content = """
            #EXTM3U
            #EXTINF:-1 tvg-name="央视1" group-title="央视",央视1
            http://a/cctv1.m3u8
            #EXTINF:-1 tvg-name="湖南卫视" group-title="卫视",湖南卫视
            http://a/hunan.m3u8
            """;
        var groups = LiveService.Parse(content);

        Assert.Equal(2, groups.Count);
        var cctv = groups.First(g => g.Name == "央视");
        Assert.Equal("央视1", cctv.Channels[0].Name);
        Assert.Equal("http://a/cctv1.m3u8", cctv.Channels[0].Url);
    }

    [Fact]
    public void ParseTxt_解析频道()
    {
        var content = """
            央视,#genre#
            央视1,http://a/cctv1.m3u8
            央视2,http://a/cctv2.m3u8
            """;
        var groups = LiveService.Parse(content);

        Assert.Single(groups);
        Assert.Equal("央视", groups[0].Name);
        Assert.Equal(2, groups[0].Channels.Count);
    }
}

public class PodcastFeedParsingTests
{
    [Fact]
    public void ParseFeed_RSS2_解析节目与剧集()
    {
        var xml = """
            <rss version="2.0"><channel>
              <title>测试节目</title>
              <item>
                <title>第一集</title>
                <enclosure url="http://a/ep1.mp3" type="audio/mpeg"/>
                <itunes:duration xmlns:itunes="http://www.itunes.com/dtds/podcast-1.0.dtd">30:00</itunes:duration>
              </item>
            </channel></rss>
            """;
        var show = PodcastService.ParseFeed(xml, "http://feed");

        Assert.Equal("测试节目", show.Title);
        Assert.Single(show.Episodes);
        Assert.Equal("第一集", show.Episodes[0].Title);
        Assert.Equal("http://a/ep1.mp3", show.Episodes[0].AudioUrl);
        Assert.Equal(1800, show.Episodes[0].DurationSeconds);
    }

    [Fact]
    public void ParseFeed_Atom_解析节目与剧集()
    {
        var xml = """
            <feed xmlns="http://www.w3.org/2005/Atom">
              <title>原子节目</title>
              <entry>
                <title>某一集</title>
                <link rel="enclosure" href="http://a/ep.m4a"/>
              </entry>
            </feed>
            """;
        var show = PodcastService.ParseFeed(xml, "http://feed");

        Assert.Equal("原子节目", show.Title);
        Assert.Single(show.Episodes);
        Assert.Equal("http://a/ep.m4a", show.Episodes[0].AudioUrl);
    }
}
