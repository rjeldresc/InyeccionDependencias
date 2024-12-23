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

            HostBuilder hostBuilder = new();

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
        Task InvokeEndpointAsync(EndpointType endpointType);
    }

    enum EndpointType
    {
        People,
        Planets,
        Films
    }

    class Test : ITtest
    {
        private readonly HttpClient client;

        public Test(HttpClient client)
        {
            this.client = client;
        }

        public async Task InvokeEndpointAsync(EndpointType endpointType)
        {
            var endpoint = endpointType switch
            {
                EndpointType.People => "api/people/1/",
                EndpointType.Planets => "api/planets/1/",
                EndpointType.Films => "api/films/1/",
                _ => "unknown"
            };

            var result = await client.GetAsync(endpoint);
            var person = System.Text.Json.JsonSerializer.Deserialize<Person>(await result.Content.ReadAsStringAsync());
            var (name, hair_color, eye_color, height) = person;
            Console.WriteLine(name);
            Console.WriteLine(hair_color);
            Console.WriteLine(eye_color);
            Console.WriteLine(height);

            //Console.WriteLine(result.Content.ReadAsStringAsync());
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

        public string ConfigurarionKey { get; init; }

        public MyService(ITtest test, IConfiguration configuration, ILogger<MyService> logger)
        {
            this.test = test;
            this.configuration = configuration;
            this.logger = logger;
            ConfigurarionKey = "PROCESSOR_IDENTIFIER";
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            //throw new NotImplementedException();
            //var message = configuration.GetValue<string>("message");
            var message = configuration.GetValue<string>(ConfigurarionKey);
            logger.LogInformation($"Enviand {message}");
            await test.InvokeEndpointAsync(EndpointType.People);
            test.Run(message);
            //return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            //throw new NotImplementedException();
            return Task.CompletedTask;
        }
    }

    record Person(string name, string hair_color, string eye_color, string height);

    class Person2
    {
        public string name { get; set; }
        public string hair_color { get; set; }
        public string eye_color { get; set; }
        public string height { get; set; }
    }


}
