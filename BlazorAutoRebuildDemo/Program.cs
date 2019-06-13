using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlazorAutoRebuildDemo
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();

            Process currentProcess = null;
            if (File.Exists(@"..\DebugAttacher.exe") && !Debugger.IsAttached)
            {
                currentProcess = Process.GetCurrentProcess();
                Process.Start("..\\DebugAttacher.exe", $"{typeof(Program).Assembly.FullName.Split(",")[0]} DUMMY {currentProcess.Id}");
            }
            host.Run(); 
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
