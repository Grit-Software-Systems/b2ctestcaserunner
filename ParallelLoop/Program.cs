using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ParallelLoop
{
    class Program
    {
        static int threadCount = int.Parse(Config("threads", "1"));
        static int iterations = int.Parse(Config("iterations", "1"));

        static string suiteName = "";
        static string appInsightsInstrumentationkey = "";
        static string testRunnerPath = "";


        static void Main(string[] args)
        {
            ParseArgs(args);

            TestCases testCases = new TestCases();

            string[] suites = suiteName.Split(',');

            for (int iter = 0; iter < iterations; iter++)
            {
                Parallel.ForEach(suites,
                    new ParallelOptions { MaxDegreeOfParallelism = threadCount },
                    suite =>   // comment out if using Single Threaded foreach
                //foreach (string suite in suites)
                {
                    testCases.Execute(
                        testRunnerPath,
                        threadCount,
                        suite,
                        appInsightsInstrumentationkey,
                        false);
                }
                );      // comment out if not using Parallel.ForEach
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

            string value = "";
            ProcessArg(args, "threadsPrefix", ref value);
            if (!string.IsNullOrEmpty(value)) 
                threadCount = int.Parse(value);    

            ProcessArg(args, "iterationsPrefix", ref value);
            if (!string.IsNullOrEmpty(value))
                iterations = int.Parse(value);
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
