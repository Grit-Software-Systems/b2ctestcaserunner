using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Imaging;

namespace Tools
{
    public class TelemetryLog
    {
        public const string metricPass = "Pass";
        public const string metricFail = "Fail";
        const string consoleFile = "console.log";
        static Dictionary<string, int> metrics = new Dictionary<string, int>();


        bool logToFile = false;
        Random rand = new Random();
        string prevError = "";      // do not repeat an error log

        public TelemetryClient telemetryClient { get; set; }


        public TelemetryLog(string instrumentationKey)
        {
            logToFile = string.IsNullOrEmpty(instrumentationKey);

            if (!logToFile)
            {
                this.telemetryClient = InitializeTelemetryClient(instrumentationKey);
            }
        }


        private TelemetryClient InitializeTelemetryClient(string instrumentationKey)
        {           
            var telemetryClient = new TelemetryClient(new TelemetryConfiguration(instrumentationKey)
            {
                DisableTelemetry = false
            });
            telemetryClient.InstrumentationKey = instrumentationKey;

            return telemetryClient;
        }


        public void TrackMetric(string metricName, int metricValue)
        {
            if (logToFile)
            {
                if (metrics.ContainsKey(metricName))
                    metrics[metricName] = metrics[metricName] + metricValue;
                else
                    metrics.Add(metricName, metricValue);
            }
            else
            {
                MetricTelemetry metricTelemetry = new MetricTelemetry(metricName, metricValue);
                telemetryClient.TrackMetric(metricTelemetry);
            }
        }


        public void TrackEvent(string eventId, Dictionary<string, string> eventProperties)
        {
            if (logToFile)
            {
                ConsoleLogger( $"\n{eventId}:{JsonConvert.SerializeObject(eventProperties)}");
            }
            else
            {
                telemetryClient.TrackEvent(eventId, eventProperties);

                string id = eventId.ToLower();
                if (id.Contains("exception") || id.Contains("fail"))
                    Flush();
            }
        }


        public void TrackEvent(string eventId, string propertyName, string propertyValue)
        {
            if (logToFile)
            {
                if (propertyName == "Error")
                {
                    TrackMetric(metricFail, 1);
                    ConsoleLogger($"\n{eventId}: {propertyName} {propertyValue}");

                    string fileName = TakeScreenshot();
                    ConsoleLogger($"\n{eventId}: screenshot name {fileName}");
                }
                else if (eventId.Contains("assert"))
                {
                    ConsoleLogger( $"\nStatus: {eventId.Replace("assert ", "")}");
                }
                else if (eventId.Contains("information"))
                {
                    string value = propertyName == "browser" ? $"Browser: {propertyValue}" : propertyValue;
                    ConsoleLogger( $"\n{value}");
                }
                else if (eventId.Contains("exception"))
                {
                    ConsoleLogger( $"\n{propertyValue}");
                }
                else
                {
                    ConsoleLogger( $"\n{eventId}: {propertyName} {propertyValue}");
                }
            }
            else
            {
                Dictionary<string, string> eventProperties = new Dictionary<string, string>();
                eventProperties.Add(propertyName, propertyValue);
                TrackEvent(eventId, eventProperties);
            }
        }

        public void TrackException(Exception exception, Dictionary<string, string> eventProperties = null, Dictionary<string, double> metrics = null)
        {
            if (logToFile)
            {
                if (prevError == exception.ToString())
                    return;
                prevError = exception.ToString();

                string details = "";
                if (eventProperties != null) details = JsonConvert.SerializeObject(eventProperties);
                if (metrics != null) details = details + "\n" + JsonConvert.SerializeObject(metrics);
                ConsoleLogger( $"\nException thrown\n{exception.ToString()}\n{details}");
            }
            else
            {
                telemetryClient.TrackException(exception, eventProperties, metrics);
            }
        }


        public void TrackTrace(string message)
        {
            if (logToFile)
            {
                ConsoleLogger( $"\nTrace: {message}");
            }
            else
            {
                telemetryClient.TrackTrace(message, SeverityLevel.Information);
            }
        }


        public void LogException(string message)
        {
            if (logToFile)
            {
                ConsoleLogger( $"\nexception: {message}");
            }
            else
            {
                Dictionary<string, string> eventProperties = new Dictionary<string, string>();
                eventProperties.Add("message", message);
                TrackEvent("exception", eventProperties);
            }
        }


        public void ConsoleLogger(string msg)
        {
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    File.AppendAllText(consoleFile, msg);
                    break;
                }
                catch { }
                System.Threading.Thread.Sleep(rand.Next() % 23);    // wait some random ms before retrying
            }
        }


        public void Flush()
        {
            if (logToFile)
            {
                string metricResults = "\nTest Results:";
                foreach(string key in metrics.Keys.OrderByDescending(k=>k))
                {
                    metricResults = $"{metricResults}\t{key} {metrics[key]}";
                }
                ConsoleLogger(metricResults);
                ConsoleLogger("\n-----------------------");
            }
            else
            {
                telemetryClient.Flush();
                System.Threading.Thread.Sleep(2*1000);
            }
        }


        string TakeScreenshot()
        {
            string fileName = $"screenshot.{DateTime.Now.ToString("yyyy.MMM.dd.hhmmss.fff")}.jpg"; ;

            Screen screen = Screen.PrimaryScreen;
            int width = screen.Bounds.Width;
            int height = screen.Bounds.Height;

            using var bitmap = new Bitmap(width, height);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(0, 0, 0, 0,
                bitmap.Size, CopyPixelOperation.SourceCopy);
            }
            bitmap.Save(fileName, ImageFormat.Jpeg);

            return fileName;
        }

    }
}
