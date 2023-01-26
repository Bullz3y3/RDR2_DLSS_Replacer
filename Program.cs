using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Management;
using System.Security.Principal;

namespace RDR2_DLSS_Replacer
{
    internal class Program
    {
        public const string PROCESS_NAME = "RDR2";

        public const string DLSS_TO_USE = "dlss_to_use.dll";
        public const string DLSS_FILE_TO_REPLACE_IN_RDR2_LOCATION = "nvngx_dlss.dll";
        public const string DLSS_BACKUP_SUFFIX = "_backup";

        public const string SEPERATOR = "==========";

        public static string rdr2Folder = null;
        public static string currentFolder = null; //to store dlss backup

        public static void Main()
        {
            if (isAdministrator())
            {
                currentFolder = System.IO.Directory.GetCurrentDirectory();

                ManagementEventWatcher w = null;
                ManagementEventWatcher w2 = null;

                string processNameComplete = PROCESS_NAME + ".exe";
                Console.WriteLine("{0}\nRDR2 DLSS Replacer running.\n{1}\n\nDLSS to use: {2} (version: {3})\nListening for process: {4}\n", SEPERATOR, SEPERATOR, DLSS_TO_USE, getDlssVersion(DLSS_TO_USE), processNameComplete);

                updateConsole("idle");

                try
                {
                    //detect start of apps
                    w = new ManagementEventWatcher("Select * From Win32_ProcessStartTrace WHERE ProcessName='" + processNameComplete + "'");
                    w.EventArrived += ProcessStartEventArrived;
                    w.Start();

                    //detect exit of apps
                    w2 = new ManagementEventWatcher("Select * From Win32_ProcessStopTrace WHERE ProcessName='" + processNameComplete + "'");
                    w2.EventArrived += ProcessStopEventArrived;
                    w2.Start();

                    //Keep it running.
                    Console.ReadLine();
                }
                finally
                {
                    w.Stop();
                    w2.Stop();
                }
            } else
            {
                Console.WriteLine("Open this program with Administrator privileges. It is required as to replace files in RDR2 Folder.\n");
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
            }
        }

        public static void ProcessStartEventArrived(object sender, EventArrivedEventArgs ev)
        {
            //Execute when RDR2 is launched.
            foreach (PropertyData pd in ev.NewEvent.Properties)
            {

                if (pd.Name == "ProcessName")
                {
                    updateConsole("started");
                    CopyDlssFile(PROCESS_NAME, "started");
                }
            }
        }

        public static void ProcessStopEventArrived(object sender, EventArrivedEventArgs e)
        {
            //Execute when RDR2 is stopped.
            foreach (PropertyData pd in e.NewEvent.Properties)
            {
                if (pd.Name == "ProcessName")
                {
                    updateConsole("stopped");

                    CopyDlssFile(PROCESS_NAME, "stopped");

                    Thread.Sleep(2000);
                    updateConsole("idle");
                }
            }
        }

        public static void CopyDlssFile(string processName, string type)
        {
            try
            {
                //get RDR2.exe location
                if (rdr2Folder == null)
                {
                    Process[] process = Process.GetProcessesByName(processName);
                    if (process == null || process.Length <= 0)
                    {
                        throw new Exception("Unable to get process by name: " + processName);
                    }

                    var firstProcess = process.First();

                    string processPath = firstProcess.GetMainModuleFileName();
                    rdr2Folder = Path.GetDirectoryName(processPath);
                }

                string source = DLSS_TO_USE;
                string destination = rdr2Folder + "/" + DLSS_FILE_TO_REPLACE_IN_RDR2_LOCATION;
                string destinationForBackup = currentFolder + "/" + DLSS_FILE_TO_REPLACE_IN_RDR2_LOCATION  + DLSS_BACKUP_SUFFIX;

                Console.WriteLine("RDR2 Location: {0}", rdr2Folder);

                if (type == "started")
                {
                    //create backup.
                    File.Copy(destination, destinationForBackup, true);
                    Console.WriteLine("✓ Successfully backed up DLSS: {0}", getDlssVersion(destinationForBackup));

                    //replace dlss.
                    File.Copy(source, destination, true);
                    Console.WriteLine("✓ Successfully copied DLSS: {0}\n", getDlssVersion(destination));
                }
                else if (type == "stopped")
                {
                    //restore from backup
                    File.Copy(destinationForBackup, destination, true);
                    Console.WriteLine("✓ Successfully restored backup DLSS: {0}\n", getDlssVersion(destination));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: Unable to copy file.\n\nException: {0}", ex.ToString());
            }
        }

        public static void updateConsole(string type)
        {
            switch (type)
            {
                case "idle":
                    {
                        Console.Title = "Idle - RDR2 DLSS Replacer";
                        Console.WriteLine("{0}\nIdle - Waiting for process to launch\n{1}\n", SEPERATOR, SEPERATOR);
                        break;
                    }
                case "started":
                    {
                        Console.Title = PROCESS_NAME + " Started - RDR2 DLSS Replacer";
                        Console.WriteLine("{0}\n{1} Started\n{2}\n", SEPERATOR, PROCESS_NAME, SEPERATOR);
                        Console.WriteLine("Trying to use new DLSS.\n");
                        break;
                    }
                case "stopped":
                    {
                        Console.Title = PROCESS_NAME + " Stopped - RDR2 DLSS Replacer";
                        Console.WriteLine("{0}\n{1} Process Stopped\n{2}\n", SEPERATOR, PROCESS_NAME, SEPERATOR);
                        Console.WriteLine("Trying to restore original RDR2's DLSS.\n");
                        break;
                    }
            }
        }

        public static string getDlssVersion(string file)
        {

            FileVersionInfo dlssVersionInfo = FileVersionInfo.GetVersionInfo(file);
            return dlssVersionInfo.FileVersion.ToString().Replace(",", ".");
        }

        public static bool isAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
    }

    internal static class Extensions
    {
        [DllImport("Kernel32.dll")]
        private static extern bool QueryFullProcessImageName([In] IntPtr hProcess, [In] uint dwFlags, [Out] StringBuilder lpExeName, [In, Out] ref uint lpdwSize);

        public static string GetMainModuleFileName(this Process process, int buffer = 1024)
        {
            var fileNameBuilder = new StringBuilder(buffer);
            uint bufferLength = (uint)fileNameBuilder.Capacity + 1;
            return QueryFullProcessImageName(process.Handle, 0, fileNameBuilder, ref bufferLength) ?
                fileNameBuilder.ToString() :
                null;
        }
    }
}
