using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VideoProcessor.Services;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var host = new HostBuilder().ConfigureFunctionsWorkerDefaults()
                                    .ConfigureServices(services =>
                                    {

                                        services.AddTransient<IFakeLoadService, FakeLoadService>();

                                        services.AddAzureClients(clientBuilder =>
                                        {
                                            clientBuilder.AddBlobServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
                                            clientBuilder.AddTableServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
                                        });
                                    })
                                    .Build();

        await host.RunAsync();
    }
}