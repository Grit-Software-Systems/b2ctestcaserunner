using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;

namespace LoadTest.models
{
    public partial class AppSettings
    {
        [JsonProperty("WebPages")]
        public WebPages WebPages { get; set; }

        [JsonProperty("TestConfiguration")]
        public TestConfiguration TestConfiguration { get; set; }

        [JsonProperty("Pages")]
        public Page[][] Pages { get; set; }
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

        [JsonProperty("TimeOut")]
        public long TimeOut { get; set; }
    }

    public partial class WebPages
    {
        [JsonProperty("SignInPage")]
        public string SignInPage { get; set; }

        [JsonProperty("SuccessPage")]
        public string SuccessPage { get; set; }
    }
}
