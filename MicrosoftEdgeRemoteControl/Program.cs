using System;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Text.Json;
using System.Linq;
using System.Threading;
using System.Text;

namespace MicrosoftEdgeRemoteControl
{
    class Program
    {
        static void WriteLine(string text)
        {
            var millis = Environment.TickCount;
            Console.WriteLine($"[{millis / 1000}.{millis % 1000}] {text}");
        }

        /// <summary>
        /// Siehe https://docs.microsoft.com/en-us/microsoft-edge/devtools-protocol/
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        static async Task Main(string[] args)
        {
            string port = args[0];
            WriteLine(nameof(MicrosoftEdgeRemoteControl));
            using var client = new HttpClient();
            var json = await client.GetStringAsync("http://localhost:9222/json/list");
            using var doc = JsonDocument.Parse(json);
            foreach (var element in doc.RootElement.EnumerateArray().Where(e => e.GetProperty("url").GetString().Contains($":{port}/")))
            {
                var webSocketDebuggerUrl = element.GetProperty("webSocketDebuggerUrl").GetString();
                using var wsClient = new ClientWebSocket();
                await wsClient.ConnectAsync(new Uri(webSocketDebuggerUrl), CancellationToken.None);
                {
                    //byte[] buf = new byte[10_000];
                    //await wsClient.ReceiveAsync(buf, CancellationToken.None);
                    //var resString = Encoding.Default.GetString(buf);
                }
                WriteLine("refreshing now...");
                await wsClient.SendAsync(Encoding.Default.GetBytes($@"{{""id"":1,""method"":""Page.navigate"",""params"":{{""url"":""{element.GetProperty("url").GetString()}""}}}}"), WebSocketMessageType.Text, true, CancellationToken.None);
                {
                    byte[] buf = new byte[10_000];
                    await wsClient.ReceiveAsync(buf, CancellationToken.None);
                    var resString = Encoding.Default.GetString(buf).Trim();
                    WriteLine(resString);
                    if (!resString.StartsWith(@"{""id"":1,""result"":{""frameId"":""0""}}"))
                    {
                        WriteLine("press key");
                        Console.ReadLine();
                    }
                }
            }
            WriteLine(nameof(MicrosoftEdgeRemoteControl) + " Done");
        }
    }
}
