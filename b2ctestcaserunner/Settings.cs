using System;
using System.Collections.Generic;
using System.Text;

namespace b2ctestcaserunner
{
    class Settings
    {
        public TestConfiguration TestConfiguration { get; set; }

        public string[] Tests { get; set; }

        public bool? DebugMode { get; set; }
    }

    public class TestConfiguration
    {
        public string Environment { get; set; }

        public string OTP_Age { get; set; }

        public string TimeOut { get; set; }

        public int? DebugWait { get; set; }

        public int timeOut
        {
            get { return int.Parse(TimeOut); }
            set { TimeOut = value.ToString(); }
        }
    }
}
