// From https://gist.github.com/egil/f3a9d42f58862913d95dbc0b6bba494e

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.SqlServer.Dac;

namespace AspireDacFx;

public static class SqlServerDatabaseResourceExtensions
{
    public static IResourceBuilder<SqlServerDatabaseResource> WithBacPacImportCommand(
        this IResourceBuilder<SqlServerDatabaseResource> target,
        string bacPacFilename)
    {
        target.ApplicationBuilder.Services.TryAddSingleton<SqlServerDatabaseImportService>();
        target.WithCommand(
            name: "ImportBacPac",
            displayName: "Import BacPac",
            executeCommand: async context =>
            {
                try
                {
                    // Display Culture info
                    Debug.WriteLine($"Current Culture: {System.Globalization.CultureInfo.CurrentCulture.Name}");
                    Debug.WriteLine($"Current UICulture: {System.Globalization.CultureInfo.CurrentUICulture.Name}");
                    var services =
                        new DacServices(await target.Resource.ConnectionStringExpression.GetValueAsync(context.CancellationToken));

                    services.Message += (s, e) => Debug.WriteLine(e.Message.ToString());
                    services.ProgressChanged += (s, e) => Debug.WriteLine($"{e.OperationId} {e.Status}: {e.Message}");

                    var package = BacPackage.Load(@"D:\downloads\WideWorldImporters-Standard.bacpac");

                    services.ImportBacpac(package, "crashing", new DacImportOptions()
                    {
                    
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                }

                //var service = context.ServiceProvider.GetRequiredService<SqlServerDatabaseImportService>();
                //await service.ImportBacPacFile(bacPacFilename, target.Resource, context.CancellationToken);
                return new ExecuteCommandResult { Success = true };
            }, new CommandOptions()
            {
                IconName = "ArrowImport",
                ConfirmationMessage = "This will drop the existing database. Are you sure you want to proceed?",
                Description = $"Drops the existing database and imports the '{bacPacFilename}' to replace it.",
                UpdateState = (context => context.ResourceSnapshot.HealthStatus == HealthStatus.Healthy && context.ResourceSnapshot?.State != SqlServerDatabaseImportService.ImportingState ? ResourceCommandState.Enabled
                    : ResourceCommandState.Disabled)
            }
            );

        return target;
    }
}