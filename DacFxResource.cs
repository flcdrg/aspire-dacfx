
using System.Diagnostics;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.Dac;

namespace AspireDacFx;

public sealed class DacFxResource(string name) : Resource(name), IResourceWithWaitSupport;

public static class DacFxExtensions
{
    public static IResourceBuilder<DacFxResource> AddDacFxImport(this IDistributedApplicationBuilder builder, string name)
    {
        var resource = new DacFxResource(name);

        builder.Eventing.Subscribe<BeforeResourceStartedEvent>((@event, cancellationToken) =>
        {
            var loggerService = @event.Services.GetRequiredService<ResourceLoggerService>();
            var logger = loggerService.GetLogger(resource);
            var services =
                new DacServices(
                    "Server=127.0.0.1,52483;User ID=sa;Password=xxxxxx;TrustServerCertificate=true;Initial Catalog=demo");

            services.Message += (s, e) => Debug.WriteLine(e.Message);
            services.Message += (sender, args) => logger.Log(LogLevel.Information, "{message}", args.Message.ToString());
                
            var package = BacPackage.Load("C:\\temp\\crashing.bacpac");

            services.ImportBacpac(package, "crash", new DacImportOptions());

            return Task.CompletedTask;
        });

        return builder.AddResource(resource)
            .ExcludeFromManifest()
            .WithInitialState(new CustomResourceSnapshot()
            {
                ResourceType = "AspireDacFx/DacFxImport",
                CreationTimeStamp = DateTime.UtcNow,
                State = KnownResourceStates.Starting,
                Properties =
                [
                    new(CustomResourceKnownProperties.Source, "DacFxImport")
                ]
            });
    }
}

//public sealed class
//    DacFxLifecycleHook( // Aspire service for publishing resource state updates (e.g., Running, Starting).
//        ResourceNotificationService notification,
//        // Aspire service for publishing and subscribing to application-wide events.
//        IDistributedApplicationEventing eventing,
//        // Aspire service for getting a logger scoped to a specific resource.
//        ResourceLoggerService loggerSvc,
//        // General service provider for dependency injection if needed.
//        IServiceProvider services) : IDistributedApplicationEventingSubscriber // Implement the Aspire hook interface.
//{
//    public Task SubscribeAsync(IDistributedApplicationEventing eventing, DistributedApplicationExecutionContext executionContext,
//        CancellationToken cancellationToken)
//    {
//        eventing.Subscribe<BeforeStartEvent>(OnBeforeStart);
//        return Task.CompletedTask;
//    }

//    private Task OnBeforeStart(BeforeStartEvent @event, CancellationToken cancellationToken)
//    {
//        return Task.CompletedTask;
//    }
//}