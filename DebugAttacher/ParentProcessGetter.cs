using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DebugAttacher
{
    /// <summary>
    /// A utility class to determine a process parent.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ParentProcessGetter
    {
        // These members must match PROCESS_BASIC_INFORMATION
        internal IntPtr Reserved1;
        internal IntPtr PebBaseAddress;
        internal IntPtr Reserved2_0;
        internal IntPtr Reserved2_1;
        internal IntPtr UniqueProcessId;
        internal IntPtr InheritedFromUniqueProcessId;

        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref ParentProcessGetter processInformation, int processInformationLength, out int returnLength);

        /// <summary>
        /// Gets the parent process of the current process.
        /// </summary>
        /// <returns>An instance of the Process class.</returns>
        public static int GetParentProcessID() => GetParentProcessID(Process.GetCurrentProcess().Handle);

        /// <summary>
        /// Gets the parent process of specified process.
        /// </summary>
        /// <param name="id">The process id.</param>
        /// <returns>An instance of the Process class.</returns>
        public static int GetParentProcessID(int id)
        {
            using (Process process = Process.GetProcessById(id))
            {
                return GetParentProcessID(process.Handle);
            }
        }

        /// <summary>
        /// Gets the parent process of a specified process.
        /// </summary>
        /// <param name="handle">The process handle.</param>
        /// <returns>An instance of the Process class.</returns>
        public static int GetParentProcessID(IntPtr handle)
        {
            ParentProcessGetter pbi = new ParentProcessGetter();
            int status = NtQueryInformationProcess(handle, 0, ref pbi, Marshal.SizeOf(pbi), out _);
            if (status != 0)
            {
                throw new Win32Exception(status);
            }

            try
            {
                using (var process = Process.GetProcessById(pbi.InheritedFromUniqueProcessId.ToInt32()))
                {
                    return process.Id;
                }
            }
            catch (ArgumentException)
            {
                // not found
                return -1;
            }
        }
    }
}
