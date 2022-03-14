using System;
using System.Threading.Tasks;

namespace b2ctestcaserunner
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                foreach (string arg in args)
                {
                    TestCase testCase = new TestCase();

                    testCase.ExecuteTest(arg);
                }
            }
            else
            {
                string testsCSV = "signUp,OTP,signIn";//"signUp";//
                string[] tests = testsCSV.Split(',');

                foreach (string t in tests)
                {
                    TestCase testCase = new TestCase();

                    testCase.ExecuteTest(t);
                }
            }
        }
    }
}
