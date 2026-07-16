using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Function.Models
{
   public class BeckhoffModel
    {
        public string AmsNetId { get; init; } = string.Empty;

        public string Ip { get; init; } = string.Empty;

        public string HostName { get; init; } = string.Empty;

        public string OsVersion { get; init; } = string.Empty;

        public string Connected { get; set; } = string.Empty;

        public string TwinCATVersion { get; set; } = string.Empty;

        public string Fingerprint { get; set; } = string.Empty;
        
    }
}
