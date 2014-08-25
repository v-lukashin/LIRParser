using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LIRParser
{
    public static class ParseManager
    {
        public static Dictionary<string, LiveInternet> cache = new Dictionary<string, LiveInternet>(200000);//в конце около 800000
        public static List<StateOptions> _queue;
        public static long countVisitsOnPages;

        private static bool _dCat, _dGeo;
        private static Regex _regCatalog, _regGeo;
        private static Repository<LiveInternet> _rep;
        private static Stopwatch sw;
        
        private const int _poolSize = 100;
        private const string _connectionString = @"mongodb://localhost:27017/liveInternetRating";
        private const string _patternCatalog = @"<tr class=""high cloud[0-9]""><td><a\shref=""(?<url>/rating/[-\w]+/[\w]{4,5}.html)"">(?<name>[-\w ]+)</a></td></tr>";
        private const string _patternGeo = @"<span class=""high cloud[0-9]""><a\shref=""(?<url>/rating/[\w]{2}(/[\d]{3})?/[\w]{4,5}.html)"">(?<name>[-\w ]+)</a></span>";

        static ParseManager()
        {
            ThreadPool.SetMaxThreads(_poolSize, _poolSize);
            _queue = new List<StateOptions>();
            _rep = new Repository<LiveInternet>(string.Format(_connectionString + "_{0:d2}{1:d2}", DateTime.Now.Month, DateTime.Now.Day));
            _regCatalog = new Regex(_patternCatalog);
            _regGeo = new Regex(_patternGeo);
            sw = new Stopwatch();

            Console.Write("Fill data...");
            var all = _rep.GetAll();
            foreach (var item in all)
            {
                cache.Add(item.Url, item);
            }
            Console.WriteLine("done.");
            Console.WriteLine("Read previos state?(y/n):");
            while (true)
            {
                var ans = Console.ReadKey().KeyChar;
                if (ans == 'y')
                {
                    ReadState();
                    break;
                }
                else if (ans == 'n')
                {
                    Console.WriteLine("From beginning..");
                    Preprocessing();
                    break;
                }
                else
                {
                    Console.WriteLine("Not \'y\' or \'n\'. Please enter again.");
                }
            }

            Query("Скачивать каталоги?(y/n)", out _dCat);
            Query("Скачивать гео?(y/n)", out _dGeo);
        }

        public static void Run()
        {
            sw.Start();
            StartHelperTasks();

            try
            {
                Processing();
            }
            finally
            {
                int a = 0, s;
                while (a < _poolSize - 2)
                {
                    ThreadPool.GetAvailableThreads(out a, out s);
                    Thread.Sleep(60000);
                }
                Saving();
                Console.WriteLine("Finish {0}\tTime worked {1:f4}min\nPress any key to exit", DateTime.Now, sw.Elapsed.TotalMinutes);
                Console.ReadKey();
            }
        }

        static void Processing()
        {
            //Сначала обрабатываем только каталоги
            if (_dCat)
            {
                //Parallel.ForEach(_queue.Where(x => x.IsGeo == false).ToArray(), new ParallelOptions { MaxDegreeOfParallelism = 10}, item =>
                foreach (var item in _queue.Where(x => x.IsGeo == false).ToArray())
                {
                    Downloader.WaitCallback(item);
                }
                //);
            }
            //Только гео
            if (_dGeo)
            {
                //Parallel.ForEach(_queue.Where(x => x.IsGeo == true).ToArray(), new ParallelOptions { MaxDegreeOfParallelism = 10}, item =>
                foreach (var item in _queue.Where(x => x.IsGeo == true).ToArray())
                {
                    Downloader.WaitCallback(item);
                }
                //);
            }
        }

        static void Preprocessing()
        {
            var link = @"http://www.liveinternet.ru/rating/month.html";
            var page = Downloader.DownloadPage(link);

            var indexCoutriesBegin = page.IndexOf(@"<div id=""countries_list""");
            var indexCoutriesEnd = page.IndexOf(@"</div>", indexCoutriesBegin);
            var indexRegionsBegin = page.IndexOf(@"<div id=""regions_list""");
            var indexRegionsEnd = page.IndexOf(@"</div>", indexRegionsBegin);
            //Каталоги
            var match = _regCatalog.Match(page, 0, indexCoutriesBegin);
            AddToQueueMatches(match, false);
            //Страны
            match = _regGeo.Match(page, indexCoutriesBegin, indexCoutriesEnd - indexCoutriesBegin);
            AddToQueueMatches(match, true);
            //Регионы России
            match = _regGeo.Match(page, indexRegionsBegin, indexRegionsEnd - indexRegionsBegin);
            AddToQueueMatches(match, true);
            //Удаляем Аргентину из списка(Редиректит на "Все")
            var ar = _queue.First(x => x.Name == "Аргентина");
            _queue.Remove(ar);

            SaveState();
        }
        #region Save
        static void Saving()
        {
            SaveState();

            Console.Write("Saving...");
            var val = cache.Values.ToArray();
            _rep.SaveAll(val);
            Console.WriteLine("done");
            Console.WriteLine("Saved {0} items. Visits {1}. Time {2:f4}min", val.Length, countVisitsOnPages, sw.Elapsed.TotalMinutes);
        }

        static void SaveState()
        {
            Console.Write("Saving state...");
            using (FileStream fs = new FileStream("State.txt", FileMode.Create))
            {
                string jsonStr = JsonConvert.SerializeObject(_queue.ToArray());
                byte[] res = System.Text.Encoding.UTF8.GetBytes(jsonStr);
                fs.Write(res, 0, res.Length);
            }
            Console.WriteLine("done");
        }

        static void ReadState()
        {
            Console.Write("Reading state...");
            using (FileStream fs = new FileStream("State.txt", FileMode.OpenOrCreate))
            {
                byte[] byteArr = new byte[fs.Length];
                fs.Read(byteArr, 0, byteArr.Length);
                List<StateOptions> res = JsonConvert.DeserializeObject<List<StateOptions>>(System.Text.Encoding.UTF8.GetString(byteArr));
                _queue = res;
            }
            Console.WriteLine("done");
        }
        #endregion

        #region Helpers
        static void StartHelperTasks()
        {
            Task consoleTask = new Task(ConsoleComand);
            consoleTask.Start();
            Task saver = new Task(() => { while (true) { Thread.Sleep(600000); Saving(); } });
            saver.Start();
        }

        static void ConsoleComand()
        {
            while (true)
            {
                string line = Console.ReadLine();
                var shift = "\t\t\t\t\t\t";
                Console.Write(shift);
                int a, s;
                switch (line)
                {
                    case "save": Saving(); break;
                    case "all":
                    default: Console.WriteLine("cnt = {0}", cache.Count);
                        Console.WriteLine(shift + "Visits on pages {0}", countVisitsOnPages);
                        Console.WriteLine(shift + "Queue lenght {0}", _queue.Count);
                        ThreadPool.GetAvailableThreads(out a, out s); Console.WriteLine(shift + "Available threads {0}/{1}", a, _poolSize);
                        Console.WriteLine(shift + "Time working {0:f4}min", sw.Elapsed.TotalMinutes);
                        break;
                }
            }
        }
        static void AddToQueueMatches(Match match, bool isGeo)
        {
            do
            {
                var so = new StateOptions { Url = match.Groups["url"].Value, Name = match.Groups["name"].Value, IsGeo = isGeo };
                _queue.Add(so);
            } while ((match = match.NextMatch()).Success);
        }

        static void Query(string queryStr, out bool boolVal)
        {
            Console.WriteLine(queryStr);
            boolVal = false;
            while (true)
            {
                var key = Console.ReadKey().KeyChar;
                if (key == 'y')
                {
                    boolVal = true;
                    break;
                }
                else if (key == 'n')
                {
                    break;
                }
                else
                {
                    Console.WriteLine("Только y или n. Еще раз.");
                }
            }
        }
        #endregion
    }
}
