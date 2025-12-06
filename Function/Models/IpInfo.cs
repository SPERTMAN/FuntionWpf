using System;
using System.Collections.Generic;
using System.ComponentModel;
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

        
    }

    public class IpInfoConfig:IpInfo
    {
        [Browsable(false)]
        public int Id { get; set; }
        public string remark { get; set; }

    }
    public class Statu: IpInfo
    {
        public  Brush Status { get; set; }
       
    }

    public class RomoteInfo 
    {
        public int Id { get; set; }
        public string Ip { get; set; }
        public string remark { get; set; }

        public string UserName { get; set; }
        public string PassWord { get; set; }
    }
    public class RomoteFile
    {
        public int Id { get; set; }
        public string Ip { get; set; }
        public string remark { get; set; }

        public string UserName { get; set; }
        public string PassWord { get; set; }
    }
}
