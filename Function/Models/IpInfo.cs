using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Function.Models
{
    public class IpInfo
    {
        public string Ip { get; set; }

        public string SubNet { get; set; }
        public string GetWay { get; set; }

        public Brush Status { get; set; }
    }
}
