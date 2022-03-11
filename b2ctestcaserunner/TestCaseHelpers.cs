using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace b2ctestcaserunner
{
    partial class TestCase
    {
        private static HttpClient httpClient = new HttpClient();

        /// <summary>
        /// read data from path, either local file or web file
        /// </summary>
        /// <param name="fileName">local file path or URL</param>
        /// <returns>contents of file as a string if success, zero length string otherwise</returns>
        string ReadFile(string fileName)
        {
            string text = "";

            try
            {
                if (fileName.ToLower().IndexOf("http")==0)
                {
                    text = httpClient.GetStringAsync(fileName).Result;
                }
                else
                {
                    string filePath = fileName;
                    if (!File.Exists(fileName))
                    {
                        fileName = fileName.ToLower().Replace(".json", "");
                        if (File.Exists(fileName + ".json")) filePath = fileName + ".json";
                        else if (File.Exists($"{exeBasePath}\\Tests\\{fileName}.json")) 
                            filePath = $"{exeBasePath}\\Tests\\{fileName}.json";
                    }

                    text = File.ReadAllText(filePath);
                }
            }
            catch 
            {
                telemetryLog.TrackEvent("File Failure", "Error", $"Unable to load file {fileName}");
            }
            return text;
        }


        /// <summary>
        /// return the value of the environment variable 
        /// or the default value if the environment variable does not exist
        /// </summary>
        /// <param name="key">environment variable name</param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        string EnvVar(string key, string defaultValue = "")
        {
            string value = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }


        /// <summary>
        /// file is an array of arrays of Page json (?)
        /// </summary>
        /// <param name="json"></param>
        /// <returns>list of Pages</returns>
        List<Page> ParsePageJson(string json)
        {
            List<Page> pages = new List<Page>();

            try
            {
                OrderedDictionary ordered = JsonSerializer.Deserialize<OrderedDictionary>(json);

                foreach (string key in ordered.Keys)
                {
                    string jsonArray = JsonSerializer.Serialize(ordered[key]);
                    pages.AddRange(JsonSerializer.Deserialize<Page[]>(jsonArray));
                }
            }
            catch { }

            return pages;
        }
    }
}
