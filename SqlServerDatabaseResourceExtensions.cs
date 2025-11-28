// From https://gist.github.com/egil/f3a9d42f58862913d95dbc0b6bba494e

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.SqlServer.Dac;

namespace AspireDacFx;

public static class SqlServerDatabaseResourceExtensions
{
    //public static IResourceBuilder<SqlServerDatabaseResource> WithDatabaseImportCommand(
    //    this IResourceBuilder<SqlServerDatabaseResource> target,
    //    IResourceBuilder<IResourceWithConnectionString> importSource)
    //{
    //    target.ApplicationBuilder.Services.TryAddSingleton<SqlServerDatabaseImportService>();
    //    target.WithCommand(
    //        name: "ImportDatabase",
    //        displayName: "Import database",
    //        executeCommand: async context =>
    //        {
    //            var service = context.ServiceProvider.GetRequiredService<SqlServerDatabaseImportService>();
    //            var importSourceConnectionString = await importSource.Resource.GetConnectionStringAsync(context.CancellationToken);

    //            if (importSourceConnectionString is null)
    //            {
    //                return new ExecuteCommandResult { Success = false, ErrorMessage = "Import source connection string is null." };
    //            }

    //            await service.ImportDatabase(importSourceConnectionString, target.Resource, context.CancellationToken);
    //            return new ExecuteCommandResult { Success = true };
    //        },
    //        updateState: (context) => context.ResourceSnapshot.HealthStatus == HealthStatus.Healthy && context.ResourceSnapshot?.State != SqlServerDatabaseImportService.ImportingState
    //            ? ResourceCommandState.Enabled
    //            : ResourceCommandState.Disabled,
    //        displayDescription: "Create a BacPac from the import source, drops the existing database and imports the BacPac to replace it.",
    //        confirmationMessage: "This will drop the existing database. Are you sure you want to proceed?",
    //        iconName: "ArrowImport");

    //    return target;
    //}

    //public static IResourceBuilder<SqlServerDatabaseResource> WithDatabaseImportCommand(
    //   this IResourceBuilder<SqlServerDatabaseResource> target,
    //   string importSourceConnectionString)
    //{
    //    target.ApplicationBuilder.Services.TryAddSingleton<SqlServerDatabaseImportService>();
    //    target.WithCommand(
    //        name: "ImportDatabase",
    //        displayName: "Import database",
    //        executeCommand: async context =>
    //        {
    //            var service = context.ServiceProvider.GetRequiredService<SqlServerDatabaseImportService>();
    //            await service.ImportDatabase(importSourceConnectionString, target.Resource, context.CancellationToken);
    //            return new ExecuteCommandResult { Success = true };
    //        },
    //        updateState: (context) => context.ResourceSnapshot.HealthStatus == HealthStatus.Healthy && context.ResourceSnapshot?.State != SqlServerDatabaseImportService.ImportingState
    //            ? ResourceCommandState.Enabled
    //            : ResourceCommandState.Disabled,
    //        displayDescription: "Create a BacPac from the import source, drops the existing database and imports the BacPac to replace it.",
    //        confirmationMessage: "This will drop the existing database. Are you sure you want to proceed?",
    //        iconName: "ArrowImport");

    //    return target;
    //}

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
                    var services =
                        new DacServices(await target.Resource.ConnectionStringExpression.GetValueAsync(context.CancellationToken));

                    services.Message += (s, e) => Debug.WriteLine(e.Message.ToString());
                    services.ProgressChanged += (s, e) =>
                    {
                        Debug.WriteLine($"{e.OperationId} {e.Status}: {e.Message}");
                    };

                    var package = BacPackage.Load("C:\\temp\\crashing.bacpac");

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