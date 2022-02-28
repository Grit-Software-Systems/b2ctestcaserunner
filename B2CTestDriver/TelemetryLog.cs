using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using System;
using System.Collections.Generic;

namespace Tools
{
    public class TelemetryLog
    {
        public TelemetryClient telemetryClient { get; set; }


        public TelemetryLog(string instrumentationKey)
        {
            this.telemetryClient = InitializeTelemetryClient(instrumentationKey);
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
            MetricTelemetry metricTelemetry = new MetricTelemetry(metricName, metricValue);
            telemetryClient.TrackMetric(metricTelemetry);
        }


        public void TrackEvent(string eventId, Dictionary<string, string> eventProperties)
        {
            telemetryClient.TrackEvent(eventId, eventProperties);

            string id = eventId.ToLower();
            if (id.Contains("exception") || id.Contains("fail"))
                Flush();
        }

        public void TrackEvent(string eventId, string propertyName, string propertyValue)
        {
            Dictionary<string, string> eventProperties = new Dictionary<string, string>();
            eventProperties.Add(propertyName, propertyValue);
            TrackEvent(eventId, eventProperties);
        }

        public void TrackException(Exception exception, Dictionary<string, string> eventProperties = null, Dictionary<string, double> metrics = null)
        {
            telemetryClient.TrackException(exception, eventProperties, metrics);
        }


        public void TrackTrace(string message)
        {
            telemetryClient.TrackTrace(message, SeverityLevel.Information);
        }


        public void LogException(string message)
        {
            Dictionary<string,string> eventProperties = new Dictionary<string, string>();
            eventProperties.Add("message", message);
            TrackEvent("exception", eventProperties);
        }

        public void Flush()
        {
            telemetryClient.Flush();
            System.Threading.Thread.Sleep(2000);
        }
    }
}
