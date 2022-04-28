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
				string parentCorrelationId = "";
                List<string> argList = ParseArgs(args, ref containerName, ref consoleLogFile, ref parentCorrelationId);
                if (!string.IsNullOrEmpty(consoleLogFile))
                    TelemetryLog.consoleFile = consoleLogFile;

                if (string.IsNullOrEmpty(parentCorrelationId))
                  parentCorrelationId = Guid.NewGuid().ToString(); 

                foreach (string arg in argList)
                {
                    Guid id = Guid.NewGuid();
                    TestCase testCase = new TestCase(args[0], containerName,id.ToString(),parentCorrelationId);
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



        static List<string> ParseArgs(string[] args, ref string containerName, ref string logFile, ref string parentCorrelationId)
        {
            containerName = args.Where(a => a.ToLower().Contains("container:")).FirstOrDefault();
            logFile = args.Where(a => a.ToLower().Contains("logfile:")).FirstOrDefault();
            parentCorrelationId = args.Where(a => a.ToLower().Contains("parentcorrelationid:")).FirstOrDefault();

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

            if (string.IsNullOrEmpty(parentCorrelationId))
            {
                parentCorrelationId = "";
            }
            else
            {
                argList.Remove(parentCorrelationId);
                parentCorrelationId = parentCorrelationId.Substring(parentCorrelationId.IndexOf(":") + 1);
            }
            return argList;
        }
    }
}
