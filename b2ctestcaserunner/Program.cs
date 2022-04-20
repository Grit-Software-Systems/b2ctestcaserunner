using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Tools;

namespace b2ctestcaserunner
{
    class Program
    {
        static string containerName = "";
        static string logFile = "";
        static string testName = "";

        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                List<string> argList = ParseArgs(args);
                if (!string.IsNullOrEmpty(logFile))
                    TelemetryLog.consoleFile = logFile;

                foreach (string arg in argList)
                {
                    TestCase testCase = new TestCase(args[0], containerName);

                    testCase.DoTests(testName);
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


        static List<string> ParseArgs(string[] args)
        {
            List<string> argList = args.ToList();

            SetField(argList, "container:", ref containerName);
            SetField(argList, "logfile:", ref logFile);
            SetField(argList, "singleTest:", ref testName);
            return argList;
        }


        static void SetField(List<string> args, string prefix, ref string field)
        {
            string value = args.Where(a => a.ToLower().Contains(prefix.ToLower())).FirstOrDefault();
            if(!string.IsNullOrEmpty(value))
            {
                args.Remove(value);
                field = value.Substring(prefix.Length);
            }
        }
    }
}
