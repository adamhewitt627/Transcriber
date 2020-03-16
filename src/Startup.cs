using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;

[assembly: FunctionsStartup(typeof(Transcriber.Startup))]

namespace Transcriber
{
#pragma warning disable CA1812
    internal class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder) => builder?.Services
            .AddSingleton(isp => new HttpClient())

            .AddTransient(isp =>
            {
                (string? region, string? key) = (
                    Environment.GetEnvironmentVariable("CognitiveRegion"),
                    Environment.GetEnvironmentVariable("CognitiveApiKey")
                );

                if (region is null || key is null)
                    throw new InvalidOperationException("You must provide `CognitiveRegion` and `CognitiveApiKey` as variables.");

                var config = SpeechConfig.FromSubscription(key, region);
                return config;
            });
    }
}
