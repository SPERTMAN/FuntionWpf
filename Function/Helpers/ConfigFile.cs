using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Function.Helpers
{
    public static class ConfigFile
    {
        private static IConfigurationRoot configuration;
        private static string path = AppContext.BaseDirectory + "jsconfig1.json";

        public static void ReadConfigFile()
        {
            var config = new ConfigurationBuilder()
            .AddJsonFile(AppContext.BaseDirectory + "jsconfig1.json", optional: true, reloadOnChange: true)
            .Build();
            Console.WriteLine($"{config["SecretKey"]}");
        }
    }
}
