using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LIRParser
{
    public class LiveInternet
    {
        private string _id;
        private HashSet<string> _geo;
        public string Id
        {
            get
            {
                _id = _id ?? Program.GenerateHash(new Regex(@"https?://").Replace(Url, string.Empty));
                return _id;
            }
            set
            {
                _id = value;
            }
        }
        public string Name { get; set; }
        public string Url { get; set; }
        public string Catalog { get; set; }
        public string Description { get; set; }
        public HashSet<string> Geo
        {
            get
            {
                _geo = _geo ?? new HashSet<string>();
                return _geo;
            }
            set { _geo = value; }
        }
        public int CountVisitors { get; set; }
    }
}
