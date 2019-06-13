using EnvDTE;
using EnvDTE80;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;

namespace DebugAttacher
{
    class Program
    {
        static void WriteLine(string text)
        {
            var millis = Environment.TickCount;
            Console.WriteLine($"[{millis / 1000}.{millis % 1000}] {text}");
        }

        static string ProjectName;
        static string Port;

        /// <summary>
        /// args: ProjectName Port attachID detachID
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            WriteLine(nameof(DebugAttacher));
            if (args.Length == 0)
            {
                WriteLine("Usage: DebugAttacher <ProjectName> <port> server");
                return;
            }
            ProjectName = args[0];
            Port = args[1];
            WriteLine($"ProjectName: {ProjectName}");
            string param = args[2];
            WriteLine($"PARAM {param} {(args.Length == 4 ? args[3] : "")}");
            //System.Windows.Forms.MessageBox.Show("COMMAND", command);
            
            if (param == "server")
            {
                using (var clientstream = new NamedPipeClientStream($"DebugAttacherPipe_{ProjectName}"))
                {
                    try
                    {
                        clientstream.Connect(10);
                        if (clientstream.IsConnected)
                        {
                            clientstream.Write(BitConverter.GetBytes(0), 0, 4);
                        }
                        else
                        {
                            var startpsi = new ProcessStartInfo("DebugAttacher", $"{ProjectName} {Port} server2")
                            {
                                UseShellExecute = false
                            };
                            System.Diagnostics.Process.Start(startpsi);
                            WriteLine("Started server2");
                        }
                    }
                    catch
                    {
                        var startpsi = new ProcessStartInfo("DebugAttacher", $"{ProjectName} {Port} server2")
                        {
                            UseShellExecute = false
                        };
                        System.Diagnostics.Process.Start(startpsi);
                        WriteLine("Started server2 (catch)");
                    }
                    
                }
            }
            else if (param == "server2")
            {
                WriteLine("Starting PipeServer");
                using (var stream = new NamedPipeServerStream($"DebugAttacherPipe_{ProjectName}"))
                {
                    WriteLine("Created Server Pipe");
                    byte[] buf = new byte[4];
                    bool firstStart = true;
                    while (!stream.IsConnected)
                    {
                        WriteLine("Waiting for Connection...");
                        stream.WaitForConnection();
                        WriteLine("Connected...");
                        stream.Read(buf, 0, 4);
                        int pID = BitConverter.ToInt32(buf, 0);
                        if (pID != 0)
                        {
                            var startpsi = new ProcessStartInfo("DebugAttacher", $"{ProjectName} {Port} X{pID} {(firstStart ? "NOREFRESH" : "")}")
                            {
                                UseShellExecute = false
                            };
                            System.Diagnostics.Process.Start(startpsi);
                            firstStart = false;
                        }
                        stream.Disconnect();
                    }
                }
                WriteLine("After Loop. Ending.");
                Console.ReadLine();
            }
            else if (param.StartsWith("X"))
            {
                var dte = GetCurrent();
                int pID = int.Parse(param.Substring(1));
                WriteLine("Started with PID " + pID);
                using (var p = System.Diagnostics.Process.GetProcessById(pID))
                {
                    DetachParent(dte, pID);
                    Attach(dte, pID, args.Length == 4 && args[3] == "NOREFRESH");

                    //p.EnableRaisingEvents = true;
                    //p.Exited += (s, e) => Detach(dte, p.Id);
                    p.WaitForExit();
                    try
                    {
                        Detach(dte, p.Id);
                    }
                    catch(Exception e)
                    {
                        WriteLine("ERROR HAPPENED");
                        Console.WriteLine(e);
                    }
                }
            }
            else if (int.TryParse(param, out int pid))
            {
                WriteLine("Writing PID to pipe: " + pid);
                using (var stream = new NamedPipeClientStream($"DebugAttacherPipe_{ProjectName}"))
                {
                    stream.Connect();
                    var buf = BitConverter.GetBytes(pid);
                    stream.Write(buf, 0, 4);
                    stream.Flush();
                }
            }
            //{

            //}
            //if (command == "dummy2" && processID > 0)
            //{
            //    var psi = new ProcessStartInfo("cmd", "/k start cmd /k start " + Directory.GetCurrentDirectory() + $"\\DebugAttacher.exe real {processID}");
            //    psi.UseShellExecute = false;
            //    System.Diagnostics.Process.Start(psi);
            //}
            //if (command == "dummy1" && processID > 0)
            //{
            //    var psi = new ProcessStartInfo("cmd", "/k start cmd /k start " + Directory.GetCurrentDirectory() + $"\\DebugAttacher.exe " + $"dummy2 {processID}");
            //    psi.UseShellExecute = false;
            //    System.Diagnostics.Process.Start(psi);
            //}
            //if (command == "dummy" && processID > 0)
            //{
            //    var psi = new ProcessStartInfo("cmd", "/k start cmd /k start " + Directory.GetCurrentDirectory() + $"\\DebugAttacher.exe " + $"dummy1 {processID}");
            //    psi.UseShellExecute = false;
            //    System.Diagnostics.Process.Start(psi);
            //}
            //else if (command == "real" && processID > 0)
            //{
            //    WriteLine("HIT THE KEY");
            //    ReadLine();

            //}
        }

        public static void Attach(DTE2 dte, int processID, bool noRefresh = false)
        {
            WriteLine($"ATTACHING {processID}");
            var processes = dte.Debugger.LocalProcesses;
            var proc = processes.Cast<EnvDTE.Process>().FirstOrDefault(p => p.ProcessID == processID);
            if (proc != null)
            {
                proc.Attach();
                
                if (!noRefresh)
                {
                    var startpsi = new ProcessStartInfo("..\\MicrosoftEdgeRemoteControl.exe", Port)
                    {
                        UseShellExecute = false
                    };
                    System.Diagnostics.Process.Start(startpsi);
                }
            }
        }

        public static void DetachParent(DTE2 dte, int processID) => Detach(dte, ParentProcessGetter.GetParentProcessID(processID));

        public static void Detach(DTE2 dte, int processID)
        {
            int step = 0;
            try
            {
                WriteLine($"DETACHING {processID}");
                step++;
                EnvDTE.Processes processes = null;
                try
                {
                    processes = dte.Debugger.DebuggedProcesses;
                }
                catch
                {
                    System.Threading.Thread.Sleep(200);
                    WriteLine("Retrying...");
                    // TODO das hier sauber klären:
  //                  System.Runtime.InteropServices.COMException(0x8001010A): The message filter indicated that the application is busy. (Exception from HRESULT: 0x8001010A(RPC_E_SERVERCALL_RETRYLATER))
  //at EnvDTE80.DTE2.get_Debugger()
  // at DebugAttacher.Program.Detach(DTE2 dte, Int32 processID)
  // at DebugAttacher.Program.Main(String[] args)
                    processes = dte.Debugger.DebuggedProcesses;
                }
                if (processes is null)
                {
                    WriteLine("couldn't get processes");
                }
                else
                {
                    step++;
                    foreach (EnvDTE.Process proc in processes)
                    {
                        proc.Detach();
                    }
                    //var proc = processes.Cast<EnvDTE.Process>().FirstOrDefault(p => p.ProcessID == processID);
                    //step++;
                    //if (proc != null)
                    //{
                    //    step++;
                    //    proc.Detach();
                    //    step++;
                    //}
                }
                WriteLine(step.ToString());
            }
            catch (Exception e)
            {
                WriteLine(step.ToString());
                throw;
            }
        }

        internal static DTE2 GetCurrent()
        {
            foreach (var proc in System.Diagnostics.Process.GetProcessesByName("devenv"))
            {
                //var dte = (DTE2)Marshal.GetActiveObject($"VisualStudio.DTE.16.0:{proc.Id}"); // For VisualStudio 2013

                WriteLine($"Process devenv {proc.Id}");// sln {dte.Solution.FileName}");
            }

            foreach (var x in GetRunningDTEsByPID())
            {
                foreach (Project project in x.Value.Dte.Solution.Projects)
                {
                    if (project.Name == ProjectName)
                    return (DTE2)x.Value.Dte;
                }
                //WriteLine($"devenv PID {x.Key} : sln {x.Value.SolutionFullName}, debugging: {x.Value.Debugging} PID {x.Value.DebuggingProcessID}");
            }

            throw new InvalidOperationException("No VS found");

            //PrintRot();
            
            //return (DTE2)Marshal.GetActiveObject("VisualStudio.DTE.16.0"); // For VisualStudio 2013
        }

        [DllImport("ole32.dll")]
        private static extern void CreateBindCtx(int reserved, out IBindCtx ppbc);

        [DllImport("ole32.dll")]
        private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable prot);

        private static class HResult
        {
            ///<summary>Operation successful</summary>
            public const int S_OK = 0x00000000;
            ///<summary>Operation not successful</summary>
            public const int S_FALSE = 0x00000001;
            /////<summary>Not implemented</summary>
            //public const int E_NOTIMPL = 0x80004001);

            /////<summary>No such interface supported</summary>
            //public const int E_NOINTERFACE = 0x80004002;

            /////<summary>Pointer that is not valid</summary>
            //public const int E_POINTER = 0x80004003;

            /////<summary>Operation aborted</summary>
            //public const int E_ABORT = 0x80004004;

            /////<summary>Unspecified failure</summary>
            //public const int E_FAIL = 0x80004005;

            /////<summary>Unexpected failure</summary>
            //public const int E_UNEXPECTED = 0x8000FFFF;

            /////<summary>General access denied error</summary>
            //public const int E_ACCESSDENIED = 0x80070005;

            /////<summary>Handle that is not valid</summary>
            //public const int E_HANDLE = 0x80070006;

            /////<summary>Failed to allocate necessary memory</summary>
            //public const int E_OUTOFMEMORY = 0x8007000E;

            /////<summary>One or more arguments are not valid</summary>
            //public const int E_INVALIDARG = 0x80070057;
        }

        public static Dictionary<int, (DTE Dte, string SolutionFullName, bool Debugging, int DebuggingProcessID)> GetRunningDTEsByPID()
        {
            var result = new Dictionary<int, (DTE Dte, string SolutionFullName, bool Debugging, int DebuggingProcessID)>();
            if (GetRunningObjectTable(0, out var rot) == HResult.S_OK)
            {
                rot.EnumRunning(out var enumMoniker);

                IntPtr fetched = IntPtr.Zero;
                IMoniker[] moniker = new IMoniker[1];
                while (enumMoniker.Next(1, moniker, fetched) == HResult.S_OK)
                {
                    CreateBindCtx(0, out var bindCtx);
                    var mon = moniker[0];
                    mon.GetDisplayName(bindCtx, null, out var displayName);
                    if (displayName.Contains("VisualStudio.DTE.16.0"))
                    {
                        rot.GetObject(mon, out var dteObj);
                        var dte = (DTE)dteObj;
                        result[int.Parse(displayName.Split(':').Last())] = (dte, dte.Solution?.FullName ?? "", dte.Debugger.CurrentProcess != null, dte.Debugger.CurrentProcess?.ProcessID ?? -1);
                    }
                }
            }
            return result;
        }

        public static void PrintRot()
        {
            WriteLine("Running Object Table start");
            if (GetRunningObjectTable(0, out var rot) == HResult.S_OK)
            {
                rot.EnumRunning(out var enumMoniker);

                IntPtr fetched = IntPtr.Zero;
                IMoniker[] moniker = new IMoniker[1];
                while (enumMoniker.Next(1, moniker, fetched) == HResult.S_OK)
                {
                    CreateBindCtx(0, out var bindCtx);
                    var mon = moniker[0];
                    mon.GetDisplayName(bindCtx, null, out var displayName);
                    if (displayName.Contains("VisualStudio.DTE.16.0"))
                    {
                        rot.GetObject(mon, out var dteObj);
                        var dte = (DTE)dteObj;
                        WriteLine(dte.Solution.FileName);

                    }
                    WriteLine($"Display Name: {displayName}");
                }
            }
            WriteLine("Running Object Table end");
        }
    }
}

