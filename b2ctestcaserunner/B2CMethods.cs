using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace b2ctestcaserunner
{
    public class B2CMethods
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public static async Task<string> GetEmailOTP(string emailAddress,string key, string apiEndpoint, string maxAge)
        {
            _httpClient.DefaultRequestHeaders.Add("x-functions-key", key);

            var json = JsonSerializer.Serialize(new { email = emailAddress, maxage = maxAge });

            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(apiEndpoint, httpContent);

            return await response.Content.ReadAsStringAsync();
        }

        public static string NewRandomUser(string postfix)
        {
            return "testDriver" + DateTimeOffset.Now.ToUnixTimeSeconds() + postfix;
        }
    }
}
