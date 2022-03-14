using System;
using System.Threading.Tasks;
using System.IO;

namespace b2ctestcaserunner
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 1)
            {
                TestCase testCase = new TestCase(args[0]);

                testCase.DoTests();
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
    }
}
