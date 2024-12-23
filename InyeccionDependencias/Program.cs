using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Net.Http;
using Polly;
using Polly.Extensions.Http;

namespace InyeccionDependencias
{
    class Program
    {
        static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
                .WaitAndRetryAsync(6, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
        }

        static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.File("test.log")
                .CreateLogger();

            var hostBuilder = new HostBuilder();

            hostBuilder.ConfigureServices((context, services) => {
                services.AddSingleton<ITtest, Test>();
                services.AddSingleton<IHostedService, MyService>();
                services.AddHttpClient<ITtest, Test>(client => {
                    client.BaseAddress = new Uri("https://petstore.swagger.io");
                }).AddPolicyHandler(GetRetryPolicy());
            });

            hostBuilder.ConfigureAppConfiguration((context, configuration) =>
            {
                configuration.AddCommandLine(args);
                configuration.AddEnvironmentVariables();
            });

            hostBuilder.ConfigureLogging((context, logging) =>
            {
                logging.AddConsole();
                logging.AddSerilog();
            });

            await hostBuilder.RunConsoleAsync();

        }
    }

    interface ITtest
    {
        void Run(string message);
        Task InvokeEndPointAsync(string endpoint);
    }

    class Test : ITtest
    {
        private readonly HttpClient client;

        public Test(HttpClient client)
        {
            this.client = client;
        }

        public async Task InvokeEndPointAsync(string endpoint)
        {
            var result = await client.GetAsync(endpoint);
            Console.WriteLine(result.Content.ReadAsStringAsync());
        }

        public void Run(string message)
        {
            Console.WriteLine(message);
        }
    }

    class MyService : IHostedService
    {
        private readonly ITtest test;
        private readonly IConfiguration configuration;
        private readonly ILogger<MyService> logger;

        public MyService(ITtest test, IConfiguration configuration, ILogger<MyService> logger)
        {
            this.test = test;
            this.configuration = configuration;
            this.logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            //throw new NotImplementedException();
            //var message = configuration.GetValue<string>("message");
            var message = configuration.GetValue<string>("PROCESSOR_IDENTIFIER");
            logger.LogInformation($"Enviand {message}");
            await test.InvokeEndPointAsync("/v2/pet/1");
            test.Run(message);
            //return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            //throw new NotImplementedException();
            return Task.CompletedTask;
        }
    }
}
