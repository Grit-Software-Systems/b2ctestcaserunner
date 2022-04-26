using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Tools;

namespace b2ctestcaserunner
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                string containerName = "";
                string consoleLogFile = "";
                List<string> argList = ParseArgs(args, ref containerName, ref consoleLogFile);
                if (!string.IsNullOrEmpty(consoleLogFile))
                    TelemetryLog.consoleFile = consoleLogFile;

                foreach (string arg in argList)
                {
                    TestCase testCase = new TestCase(args[0], containerName);

                    testCase.DoTests();
                }
            }
            else
            {
                string exeBasePath = AppDomain.CurrentDomain.BaseDirectory;
                string helpFile = Path.Combine(exeBasePath, "HelpFile.txt");

                if (File.Exists(helpFile))
                {
                    string text = File.ReadAllText(helpFile);
                    Console.WriteLine(text);
                }
            }

        }


        static List<string> ParseArgs(string[] args, ref string containerName, ref string logFile)
        {
            containerName = args.Where(a => a.ToLower().Contains("container:")).FirstOrDefault();
            logFile = args.Where(a => a.ToLower().Contains("logfile:")).FirstOrDefault();

            List<string> argList = args.ToList();

            if (string.IsNullOrEmpty(containerName))
            {
                containerName = "";
            }
            else
            {
                argList.Remove(containerName);
                containerName = containerName.Substring(containerName.IndexOf(":") + 1);
            }

            if (string.IsNullOrEmpty(logFile))
            {
                logFile = "";
            }
            else
            {
                argList.Remove(logFile);
                logFile = logFile.Substring(logFile.IndexOf(":") + 1);
            }
            return argList;
        }
    }
}
