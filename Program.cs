using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Management;
using System.Security.Principal;
using System.Reflection;
using System.Collections.Generic;
using System.Net;

namespace RDR2_DLSS_Replacer
{
    internal class Program
    {
        public const string PROCESS_NAME = "RDR2";

        public const string DLSS_TO_USE = "nvngx_dlss.dll";
        public const string DLSS_TO_DOWNLOAD_IF_NOT_FOUND = "https://github.com/EugenePoubel/RDR2_DLSS_Replacer/raw/master/DLSS/nvngx_dlss_3.5.10.dll";
        public const string DLSS_DOWNLOAD_PROGRESS_STRING = "Please wait... downloading: ";

        public const string DLSS_FILE_TO_REPLACE_IN_RDR2_LOCATION = "nvngx_dlss.dll";
        public const string DLSS_BACKUP_SUFFIX = "_backup";

        public const string SEPERATOR = "==========";

        public static string rdr2Folder = null;
        public static string currentFolder = null; //to store dlss backup

        public static string rdr2LocationFileForAutoStart = "rdr2_location_for_auto_start.txt"; //to auto launch RDR2 as told from this location
        public static string rdr2LocationForAutoStart = null; //start rdr2 automatically if this exist

        public static WebClient downloadDlssWebClient = null;

        public static void Main()
        {
            string appGuid = ((GuidAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(GuidAttribute), false).GetValue(0)).Value;
            string mutexId = string.Format("Global\\{{{0}}}", appGuid);
            using (Mutex mutex = new Mutex(false, mutexId))
            {
                if (!mutex.WaitOne(0, false))
                {
                    Console.WriteLine("ERROR: This program is already running. To ensure stability only one instance of this program can be opened.\n");
                    Console.WriteLine("Press any key to exit.");
                    Console.ReadKey();
                    Environment.Exit(1);
                }

                if (isAdministrator())
                {
                    currentFolder = Directory.GetCurrentDirectory();
                    checkDlssFileExistsOrDownload();
                }
                else
                {
                    Console.WriteLine("ERROR: Open this program with Administrator privileges. It is required as to replace files in RDR2 Folder.\n");
                    Console.WriteLine("Press any key to exit.");
                    Console.ReadKey();
                    Environment.Exit(1);
                }
            }
        }

        public static void checkDlssFileExistsOrDownload()
        {
            if (File.Exists(DLSS_TO_USE))
            {
                //dlss file exists
                startProcess();
            } else
            {
                //download dlss file.
                Console.WriteLine("{0}\nDownloading DLSS file to use\n{1}\n\nDLSS file to use: {2} does not found, so we're downloading from: {3}\n\nIf you want to use your own, you can replace it with your own version.\n", SEPERATOR, SEPERATOR, DLSS_TO_USE, DLSS_TO_DOWNLOAD_IF_NOT_FOUND);
                Console.Write("\r{0} {1}%", DLSS_DOWNLOAD_PROGRESS_STRING, 0);

                downloadDlssWebClient = new WebClient();
                downloadDlssWebClient.DownloadProgressChanged += client_DownloadProgressChanged;
                downloadDlssWebClient.DownloadFileCompleted += client_DownloadFileCompleted;

                string tempFile = DLSS_TO_USE + "_temp";  //download with _temp so if download is corrupted, the process can start again.
                downloadDlssWebClient.DownloadFileAsync(new Uri(DLSS_TO_DOWNLOAD_IF_NOT_FOUND), tempFile);
                Console.ReadLine();
            }
        }

        public static void client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e) // NEW
        {
            Console.Write("\r{0} {1}%", DLSS_DOWNLOAD_PROGRESS_STRING, e.ProgressPercentage);
        }

        private static void client_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e) // This is our new method!
        {
            Console.Write("\r{0} {1}", DLSS_DOWNLOAD_PROGRESS_STRING, "Finished");
            Console.WriteLine("\n");
            File.Move(DLSS_TO_USE + "_temp", DLSS_TO_USE);

            startProcess();
        }

        public static void startProcess()
        {
            //release webclient memory if not null
            if (downloadDlssWebClient != null)
            {
                downloadDlssWebClient.Dispose();
            }

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

                //auto start RDR2 if rdr2LocationFileForAutoStart exist and is valid
                autoStartRdr2IfEnabled();

                //Keep it running.
                Console.ReadLine();
            }
            finally
            {
                w.Stop();
                w2.Stop();
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

        public static void autoStartRdr2IfEnabled()
        {
            if (File.Exists(rdr2LocationFileForAutoStart))
            {
                //read file
                string rdr2LocationS = File.ReadAllText(rdr2LocationFileForAutoStart).Trim();
                if (!String.IsNullOrEmpty(rdr2LocationS))
                {
                    //remove comments (starting with hash) and empty lines from file
                    string[] rdr2LocationA = File.ReadLines(rdr2LocationFileForAutoStart).Where(line => !line.StartsWith("#") && !String.IsNullOrEmpty(line)).ToArray();
                    
                    //read only first
                    string rdr2Location = rdr2LocationA.FirstOrDefault();

                    if (rdr2Location != null)
                    {
                        rdr2Location = rdr2Location.Replace("\"", "");
                        if (!String.IsNullOrEmpty(rdr2Location))
                        {
                            //check if rdr2location exist.
                            if (File.Exists(rdr2Location))
                            {
                                rdr2LocationForAutoStart = rdr2Location;
                                Console.WriteLine("Auto start location found: {0}\n", rdr2LocationForAutoStart);

                                updateConsole("starting");
                                startRdr2();
                            }
                            else
                            {
                                Console.WriteLine("ERROR: `{0}` does not exist.\n", rdr2Location);
                                Console.WriteLine("- Either delete `{0}` so you can start RDR2 manually, or put correct location in `{1}`\n", rdr2LocationFileForAutoStart, rdr2LocationFileForAutoStart);
                                Console.WriteLine("Press any key to exit.");
                                Console.ReadKey();
                                Environment.Exit(1);
                            }
                        }
                    }
                }
            }

            if (rdr2LocationForAutoStart == null)
            {
                Console.WriteLine("Start your game now.\n");
            }
        }

        public static void startRdr2()
        {
            var processStartInfo = new ProcessStartInfo(rdr2LocationForAutoStart);
            processStartInfo.WorkingDirectory = Path.GetDirectoryName(rdr2LocationForAutoStart);
            var process = Process.Start(processStartInfo);
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

                Console.WriteLine("RDR2 Location: {0}\\{1}", rdr2Folder, PROCESS_NAME + ".exe");

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
                        Console.WriteLine("{0}\nIdle - Waiting for process to launch.\n{1}\n", SEPERATOR, SEPERATOR);
                        break;
                    }
                case "starting":
                    {
                        Console.Title = "Starting RDR2 - RDR2 DLSS Replacer";
                        Console.WriteLine("- Starting RDR2...\n");
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
