using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LIRParser
{
    public class Downloader
    {
        private const string _patternItem = @"<a name=""(?<name>.+)""\s+target=""_blank""\s+href=""(?<url>.+)"">(?<desc>.+)</a>\s*(<span.*>[\d]{0,3}\%</span>)?</td>\s*<td.*>(?<visitors>[\w,]+)</td>";
        private const string _patternNext = @"<a href=""(?<url>\w{4,5}.html(?<page>\?page=\d+))"" class=""high"">следующая</a>";
        private Regex _regItem, _regNext;
        private Dictionary<string, LiveInternet> _cache;
        private StateOptions _opt;

        public Downloader(StateOptions opt)
        {
            _opt = opt;
            _cache = ParseManager.cache;
            _regItem = new Regex(_patternItem);
            _regNext = new Regex(_patternNext);
        }

        public static void WaitCallback(object options)
        {
            var opt = (StateOptions)options;
            new Downloader(opt).StartProcessing();
        }

        public void StartProcessing()
        {
            Console.WriteLine("Start page: {0}", _opt.Url);
            while (true)
            {
                try
                {
                    string page = DownloadPage(_opt.Url);
                    if (page == null) throw new Exception(string.Format("Page not downloaded: {0}", _opt.Url));

                    var matches = _regItem.Matches(page);
                    foreach (Match match in matches)
                    {
                        var name = match.Groups["name"].Value;
                        var url = match.Groups["url"].Value;
                        var desc = match.Groups["desc"].Value;
                        var cntVis = -1;
                        try
                        {
                            var tmp = match.Groups["visitors"].Value;
                            cntVis = int.Parse(tmp.Replace(",", string.Empty));
                        }
                        catch { }

                        LiveInternet item;
                        if (_cache.ContainsKey(url))
                        {
                            item = _cache[url];
                        }
                        else
                        {
                            item = new LiveInternet { Name = name, Url = url, Description = desc, CountVisitors = cntVis };
                            _cache.Add(url, item);
                        }
                        if (_opt.IsGeo)
                        {
                            if (!item.Geo.Contains(_opt.Name)) item.Geo.Add(_opt.Name);
                        }
                        else
                        {
                            item.Catalog = _opt.Name;
                        }
                    }
                    Console.Write('.');
                    ParseManager.countVisitsOnPages++;

                    var matchNext = _regNext.Match(page);
                    if (matchNext.Success)
                    {
                        var pageNum = matchNext.Groups["page"].Value;
                        _opt.Url = new Regex(@"\?page=\d+").Replace(_opt.Url, string.Empty) + pageNum;
                    }
                    else break;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error in downloader: {0}\n{1}", e.Message, e.StackTrace);
                }
            }
            Console.WriteLine("Finish page: {0}", _opt.Url);
            ParseManager._queue.Remove(_opt);
        }

        public static string DownloadPage(string link)
        {
            WebClient cli = new WebClient();
            cli.BaseAddress = "http://www.liveinternet.ru/rating/";
            cli.Proxy = null;
            cli.Encoding = Encoding.UTF8;

            string page = null;
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    page = cli.DownloadString(link);
                    break;
                }
                catch (WebException wexc)
                {
                    if (wexc.Status == WebExceptionStatus.ProtocolError)
                    {
                        Console.WriteLine("ProtocolError on link {0}. Sleep 5 min.", link);
                        Thread.Sleep(300000);
                        Console.WriteLine("Repeat:{0}.Link: {1}", i, link);
                    }
                    else
                    {
                        Console.WriteLine("TimeoutError({0}). Repeat", i);
                    }
                    continue;
                }
            }
            return page;
        }
    }
}
