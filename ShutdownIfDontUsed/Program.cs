using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShutdownIfDontUsed;
using ShutdownIfDontUsed.Services;
using ShutdownIfDontUsed.Services.Interfaces;

// Initialise the hosting environment.
IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        // Configure the Windows Service Name.
        options.ServiceName = "ShutdownIfDontUsed";
    })
    .ConfigureServices(services =>
    {
        // Register the primary worker service.
        services.AddHostedService<Worker>();

        // Register other services here.
        services.AddSingleton<IProcessServices, ProcessServices>();
    })
    .Build();


// Run the application.
await host.RunAsync();