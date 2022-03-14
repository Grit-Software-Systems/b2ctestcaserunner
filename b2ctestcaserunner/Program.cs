using System;
using System.Threading.Tasks;

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

        }
    }
}
