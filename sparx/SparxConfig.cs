using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sparx
{
    public class SparxConfig
    {
        public string name { get; set; }
        public string author { get; set; }
        public string version { get; set; }

        public List<string> sparks { get; set; }
    }

    public class Spark : SparxConfig
    {
        public string libraryDll { get; set; }
        public string mainNamespace { get; set; }
    }
}
