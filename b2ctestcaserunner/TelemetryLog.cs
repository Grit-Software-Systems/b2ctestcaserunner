﻿using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Tools
{
    public class TelemetryLog
    {
        const string consoleFile = "console.log";
        bool logToFile = false;
        Random rand = new Random();

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
            if (!logToFile)
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
                if (eventId.Contains("assert"))
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
            if (!logToFile)
            {
                telemetryClient.Flush();
                System.Threading.Thread.Sleep(2000);
            }
        }
    }
}