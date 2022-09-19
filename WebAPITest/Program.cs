using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;
using System.IO;
using Serilog.Formatting.Display;

namespace WebAPITest
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
                    .WriteTo.ApplicationInsights(
                        services.GetRequiredService<TelemetryConfiguration>(),
                        new CustomEventAndTraceTelemetryConverter()))
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }

    public class CustomEventAndTraceTelemetryConverter : TelemetryConverterBase
    {
        public override IEnumerable<ITelemetry> Convert(
            LogEvent logEvent,
            IFormatProvider formatProvider)
        {
            CustomEventAndTraceTelemetryConverter converter = this;

            if (logEvent == null)
                throw new ArgumentNullException(nameof(logEvent));
            if (logEvent.Exception == null)
            {
                if (logEvent.MessageTemplate.Text.StartsWith("event:"))
                {
                    EventTelemetry telemetryProperties = new EventTelemetry(logEvent.MessageTemplate.Text)
                    {
                        Timestamp = logEvent.Timestamp
                    };
                    converter.ForwardPropertiesToTelemetryProperties(logEvent,
                        (ISupportProperties)telemetryProperties, formatProvider);
                    yield return (ITelemetry)telemetryProperties;
                }
                else
                {
                    StringWriter stringWriter = new StringWriter();
                    CustomEventAndTraceTelemetryConverter.MessageTemplateTextFormatter.Format(logEvent, (TextWriter)stringWriter);
                    TraceTelemetry telemetryProperties = new TraceTelemetry(stringWriter.ToString())
                    {
                        Timestamp = logEvent.Timestamp,
                        SeverityLevel = converter.ToSeverityLevel(logEvent.Level)
                    };
                    converter.ForwardPropertiesToTelemetryProperties(logEvent,
                        (ISupportProperties)telemetryProperties, formatProvider);
                    yield return (ITelemetry)telemetryProperties;
                }
            }
            else
                yield return (ITelemetry)converter.ToExceptionTelemetry(logEvent, formatProvider);
        }

        private static readonly MessageTemplateTextFormatter MessageTemplateTextFormatter = new MessageTemplateTextFormatter("{Message:lj}", (IFormatProvider)null);

        public override void ForwardPropertiesToTelemetryProperties(
            LogEvent logEvent,
            ISupportProperties telemetryProperties,
            IFormatProvider formatProvider)
        {
            this.ForwardPropertiesToTelemetryProperties(logEvent, telemetryProperties, formatProvider, false, true, false);
        }
    }
}
