using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ParallelLoop
{
    class Program
    {
        static string suiteName = "";
        static string appInsightsInstrumentationkey = "";
        static string testRunnerPath = "";


        static void Main(string[] args)
        {
            ParseArgs(args);

            int threadCount = int.Parse(Config("threads", "1"));
            int iterations = int.Parse(Config("iterations", "1"));

            TestCases testCases = new TestCases();

            for (int iter = 0; iter < iterations; iter++)
            {
                testCases.Execute(
                    testRunnerPath,
                    threadCount,
                    suiteName,
                    appInsightsInstrumentationkey,
                    true);
            }
        }


        static void ParseArgs(string[] args)
        {
            ProcessArg(args, "suiteNamePrefix", ref suiteName);
            ProcessArg(args, "executablePathPrefix", ref testRunnerPath);
            ProcessArg(args, "appInsightsInstrumentationKeyPrefix", ref appInsightsInstrumentationkey);

            if(!testRunnerPath.Contains(".exe"))
            {
                testRunnerPath = Directory.GetFiles(testRunnerPath, "b2ctestcaserunner.exe").FirstOrDefault();
            }
        }


        static void ProcessArg(string[] args, string prefixName, ref string field)
        {
            string prefix = Config(prefixName);
            string value = args.Where(a => a.ToLower().Contains(prefix.ToLower())).FirstOrDefault();
            if(!string.IsNullOrEmpty(value))
            {
                field = value.Substring(prefix.Length);
            }
        }

        static string Config(string key, string defaultValue = "")
        {
            string value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }
    }
}
