using Newtonsoft.Json;

namespace B2CTestDriver.models
{
    public partial class AppSettings
    {
        [JsonProperty("TestConfiguration")]
        public TestConfiguration TestConfiguration { get; set; }

        [JsonProperty("Tests")]
        public string[] Tests { get; set; }
        [JsonProperty("DebugMode")]
        public bool? DebugMode { get; set; }

    }

    public partial class Page
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("inputType")]
        public string InputType { get; set; }

        [JsonProperty("value", NullValueHandling = NullValueHandling.Ignore)]
        public string Value { get; set; }
    }

    public partial class TestConfiguration
    {
        [JsonProperty("Environment")]
        public string Environment { get; set; }

        [JsonProperty("OTP_Age")]
        public string OTP_Age { get; set; }

        [JsonProperty("TimeOut")]
        public long TimeOut { get; set; }
        [JsonProperty("DebugWait")]
        public int? DebugWait { get; set; }
    }
}
