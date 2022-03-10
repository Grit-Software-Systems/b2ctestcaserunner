using System;
using System.Collections.Generic;
using System.Text;

namespace b2ctestcaserunner
{
    class AppSettings
    {
        public TestConfiguration TestConfiguration { get; set; }

        public string[] Tests { get; set; }
    }

    public class TestConfiguration
    {
        public string Environment { get; set; }

        public string OTP_Age { get; set; }

        public string TimeOut { get; set; }

        public int timeOut
        {
            get { return int.Parse(TimeOut); }
        }
    }
}
