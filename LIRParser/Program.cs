using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LIRParser
{
    class Program
    {
        static void Main(string[] args)
        {
            //TestPattern();
            //DeleteAr();
            ParseManager.Run();
        }
        static void DeleteAr(){
            var connStr = @"mongodb://localhost:27017/liveInternetRating_0825";
            var rep = new Repository<LiveInternet>(connStr);
            var all = rep.GetAll();
            Console.WriteLine("Del");
            int cnt = 0;
            foreach (var item in all)
            {
                if (++cnt % 10000 == 0) Console.Write('.');

                if (item.Geo.Contains("Аргентина"))
                {
                    item.Geo.Remove("Аргентина");
                    rep.Save(item);
                }
            }
            Console.WriteLine("End");

        }
        static void TestPattern()
        {
            string _patternItem = @"<a name=""(?<name>.+)""\s+target=""_blank""\s+href=""(?<url>.+)"">(?<desc>.+)</a>\s*(<span.*>[\d]{0,3}\%</span>)?</td>\s*<td.*>(?<visitors>[\w,]+)</td>";
            var page = Downloader.DownloadPage("http://www.liveinternet.ru/rating/au/month.html");
            var mc = new Regex(_patternItem).Matches(page);
            Console.WriteLine();
        }
        public static string GenerateHash(string str)
        {
            byte[] b = Encoding.UTF8.GetBytes(str);
            b = MD5.Create().ComputeHash(b);
            return BitConverter.ToString(b).Replace("-", string.Empty).ToUpper();
        }
    }
}
