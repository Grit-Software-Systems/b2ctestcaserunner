using System;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace b2ctestcaserunner
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                string containerName = "";
                List<string> argList = ParseArgs(args, ref containerName);

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


        static List<string> ParseArgs(string[] args, ref string containerName)
        {
            containerName = args.Where(a => a.Contains("container:")).FirstOrDefault();

            List<string> argList = args.ToList();

            if (string.IsNullOrEmpty(containerName))
            {
                containerName = "";
            }
            else
            {
                argList.Remove(containerName);
                containerName = containerName.Replace("container:", "");
            }

            return argList;
        }
    }
}
