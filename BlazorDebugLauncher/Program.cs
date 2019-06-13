using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Process = System.Diagnostics.Process;

namespace BlazorDebugLauncher
{
    class Program
    {
        static void WriteLine(string text)
        {
            var millis = Environment.TickCount;
            Console.WriteLine($"[{millis / 1000}.{millis % 1000}] {text}");
        }

        static async Task Main(string[] args)
        {
            WriteLine("BlazorDebugLauncher");
            var launchPort = args[0];
            var launchDnsName = args[1];
            WriteLine("starting DebugAttacher and dotnet watch run");
            Process.Start("cmd", $"/c \"..\\DebugAttacher {Path.GetFileName(Environment.CurrentDirectory)} {launchPort} server & dotnet watch run\"");
            WriteLine("starting edge debug server");
            using (var edgeRemoteControlStartProcess = Process.Start("MicrosoftEdge.exe", "--devtools-server-port 9222"))
            {
                edgeRemoteControlStartProcess.WaitForExit();
            }
            WriteLine("Checking for edge windows");
            using (var client = new HttpClient())
            {
                var json = await client.GetStringAsync("http://localhost:9222/json/list");
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.EnumerateArray().Any(e => e.GetProperty("url").GetString().Contains($":{launchPort}/")))
                {
                    WriteLine("no edge tab found. opening");
                    using var httpClient = new HttpClient { BaseAddress = new Uri($"http://{launchDnsName}:{launchPort}") };
                    var timeout = DateTime.Now.AddSeconds(30);
                    bool serverIsThere = false;
                    while (!serverIsThere && timeout > DateTime.Now)
                    {
                        try
                        {
                            await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/"));
                            serverIsThere = true;
                        }
                        catch (HttpRequestException)
                        {
                            await Task.Delay(1000);
                            continue;
                        }
                    }
                    if (serverIsThere)
                    {

                        var psi = new ProcessStartInfo($"microsoft-edge:http://{launchDnsName}:{launchPort}")
                        {
                            UseShellExecute = true
                        };
                        Process.Start(psi);
                    }
                    else
                    {
                        WriteLine("Server not found after timeout");
                    }
                }
            }
            WriteLine("done.");


            //WriteLine();
            //WriteLine();
            //WriteLine();
            //WriteLine();

            //foreach (var proc in Process.GetProcesses().OrderBy(p => p.Id))
            //{
            //    Write($"{proc.ProcessName} -> {proc.Id} => ");
            //    try
            //    {
            //        var arguments = proc.StartInfo.Arguments;
            //        WriteLine(arguments);
            //    }
            //    catch
            //    {
            //        WriteLine("no access");
            //    }
            //}

            //WriteLine();
            //WriteLine();
            //WriteLine();
            //WriteLine();
            //newProcess.WaitForExit();
            //WriteLine("ended.");
            //ReadLine();
        }
    }
}
