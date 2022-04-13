using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;

namespace ParallelLoop
{
    class TestCases
    {
        public void Execute(
            string exePath,
            int threads,
            string suiteName,
            string appInsightsInstrumentationKey = "",
            bool singleThreaded = false)
        {
            Dictionary<string, string> env = new Dictionary<string, string>();
            env.Add("appInsightsInstrumentationKey", appInsightsInstrumentationKey);

            string jsonSuite = suiteName;
            if(!File.Exists(jsonSuite))
            {
                string root = Path.GetDirectoryName(exePath);
                jsonSuite = Directory.GetFiles(root, suiteName, SearchOption.AllDirectories).FirstOrDefault();
            }
            SuiteTests suite = JsonSerializer.Deserialize<SuiteTests>(File.ReadAllText(jsonSuite));

            if (singleThreaded)
            {
                foreach (string test in suite.Tests)
                {
                    StartProcess(exePath, $"{suiteName} singleTest:{test}", env);
                }
            }
            else
            {
                Parallel.ForEach(suite.Tests,
                    new ParallelOptions { MaxDegreeOfParallelism = threads },
                    test =>
                    {
                        StartProcess(exePath, $"{suiteName} singleTest:{test}", env);
                    });
            }
        }

        void StartProcess(string executable, string arguments = "", Dictionary<string, string> env = null, string workingDirectory = "")
        {
            ProcessStartInfo proc = new System.Diagnostics.ProcessStartInfo();
            proc.FileName = @"C:\windows\system32\cmd.exe";
            proc.UseShellExecute = false;
            if (!string.IsNullOrEmpty(workingDirectory))
            {
                proc.WorkingDirectory = workingDirectory;
            }
            else
            {
                string dir = Path.GetDirectoryName(executable);
                proc.WorkingDirectory = Path.GetDirectoryName(executable);
            }
            proc.Arguments = $"/c {executable} {arguments}";

            foreach (string key in env.Keys)
            {
                proc.Environment.Add(key, env[key]);
            }

            Process p = Process.Start(proc);
            p.WaitForExit();
        }

    }
}
