using System;
using System.Threading.Tasks;

namespace b2ctestcaserunner
{
    class Program
    {
        static void Main(string[] args)
        {
            string testsCSV = "signUp,OTP,signIn";//"signUp";//
            string[] tests = testsCSV.Split(',');

            //Parallel.ForEach(tests, t =>
            foreach (string t in tests)
            {
                TestCase testCase = new TestCase();

                testCase.ExecuteTest(t);
            }
            //);
        }
    }
}
