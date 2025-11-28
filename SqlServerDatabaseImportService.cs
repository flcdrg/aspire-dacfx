// From https://gist.github.com/egil/f3a9d42f58862913d95dbc0b6bba494e
// requires <PackageVersion Include="Microsoft.SqlServer.DacFx" Version="162.5.57" />

using Aspire.Hosting.Eventing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.Dac;

namespace AspireDacFx;

internal sealed partial class SqlServerDatabaseImportService(
    ResourceLoggerService resourceLoggerService,
    ResourceNotificationService resourceNotificationService,
    IDistributedApplicationEventing eventing,
    IServiceProvider serviceProvider)
{
    public const string ImportingState = "Importing";

    public async Task ImportDatabase(string sourceConnectionString, SqlServerDatabaseResource target, CancellationToken cancellationToken)
    {
        var logger = resourceLoggerService.GetLogger(target);
        var bacPacFilename = string.Empty;
        try
        {
            await resourceNotificationService.PublishUpdateAsync(
                target,
                state => state with { State = new ResourceStateSnapshot(ImportingState, KnownResourceStateStyles.Info) });

            bacPacFilename = ExportBacPac(
                target.DatabaseName,
                sourceConnectionString,
                logger,
                cancellationToken);
        }
        catch (Exception ex)
        {
            LogFailedDatabaseImport(logger, ex, target.Name);
            await resourceNotificationService.PublishUpdateAsync(
                target,
                state => state with { State = new ResourceStateSnapshot(KnownResourceStates.Finished, KnownResourceStateStyles.Error), });
        }

        await ImportBacPacFile(bacPacFilename, target, cancellationToken);
    }

    public async Task ImportBacPacFile(string bacPacFileName, SqlServerDatabaseResource target, CancellationToken cancellationToken)
    {
        var logger = resourceLoggerService.GetLogger(target);

        try
        {
            await resourceNotificationService.PublishUpdateAsync(
                target,
                state => state with { State = new ResourceStateSnapshot(ImportingState, KnownResourceStateStyles.Info) });

            await DropExistingDatabase(
                target,
                logger,
                cancellationToken);

            LogImportingDatabase(logger, bacPacFileName, target.Name);
            await ImportBacPac(
                target,
                BacPackage.Load(bacPacFileName),
                logger,
                cancellationToken);

            await resourceNotificationService.PublishUpdateAsync(
                target,
                state => state with { State = new ResourceStateSnapshot(KnownResourceStates.Running, KnownResourceStateStyles.Success) });

            await eventing.PublishAsync(new ResourceReadyEvent(target, serviceProvider), cancellationToken);
        }
        catch (Exception ex)
        {
            LogFailedDatabaseImport(logger, ex, target.Name);
            await resourceNotificationService.PublishUpdateAsync(
                target,
                state => state with { State = new ResourceStateSnapshot(KnownResourceStates.Finished, KnownResourceStateStyles.Error), });
        }
    }

    private static string ExportBacPac(string databaseName, string sourceConnectionString, ILogger logger, CancellationToken cancellationToken)
    {
        var bacPacFile = Path.Combine(Directory.CreateTempSubdirectory().FullName, databaseName + ".bacpac");
        LogExportingDatabase(logger, sourceConnectionString, bacPacFile);

        var exportDacService = new DacServices(sourceConnectionString);
        exportDacService.Message += (sender, args) => LogExportMessage(logger, args.Message.ToString());

        exportDacService.ExportBacpac(
            bacPacFile,
            databaseName,
            cancellationToken: cancellationToken);

        return bacPacFile;
    }

    private static async Task DropExistingDatabase(SqlServerDatabaseResource target, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            LogAttemptToDropDatabase(logger, target.Name);

            var targetConnectionString = await target.ConnectionStringExpression.GetValueAsync(cancellationToken);
            await using var connection = new SqlConnection(targetConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
            USE master;
            ALTER DATABASE [{target.DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
            DROP DATABASE [{target.DatabaseName}];
            """;
            await command.ExecuteNonQueryAsync(cancellationToken);

            LogDatabaseDropped(logger, target.Name);
        }
        catch (SqlException ex) when (ex.ErrorCode == -2146232060)
        {
            LogNoExistingDatabase(logger, ex, target.Name);
        }
    }

    private static async Task ImportBacPac(SqlServerDatabaseResource target, BacPackage bacpac, ILogger logger, CancellationToken cancellationToken)
    {
        var targetConnectionString = await target.ConnectionStringExpression.GetValueAsync(cancellationToken);

        var dacService = new DacServices(targetConnectionString);
        dacService.Message += (sender, args) => LogImportMessage(logger, args.Message.ToString());

        dacService.ImportBacpac(
            bacpac,
            target.DatabaseName,
            cancellationToken
            );
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Exporting database backup from '{SourceConnectionString}' to '{BacPacFile}'.")]
    public static partial void LogExportingDatabase(ILogger logger, string sourceConnectionString, string bacPacFile);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Message}")]
    public static partial void LogExportMessage(ILogger logger, string message);

    [LoggerMessage(Level = LogLevel.Information, Message = "Importing database backup from '{BacPacFile}' to '{TargetDatabaseResourceName}'.")]
    public static partial void LogImportingDatabase(ILogger logger, string bacPacFile, string targetDatabaseResourceName);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Message}")]
    public static partial void LogImportMessage(ILogger logger, string message);

    [LoggerMessage(Level = LogLevel.Information, Message = "Attempting to drop existing database {TargetDatabaseResourceName}.")]
    public static partial void LogAttemptToDropDatabase(ILogger logger, string targetDatabaseResourceName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Database {TargetDatabaseResourceName} dropped.")]
    public static partial void LogDatabaseDropped(ILogger logger, string targetDatabaseResourceName);

    [LoggerMessage(Level = LogLevel.Information, Message = "The target database {TargetDatabaseResourceName} did not exist.")]
    public static partial void LogNoExistingDatabase(ILogger logger, Exception exception, string targetDatabaseResourceName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to retrieve connection string for target database {TargetDatabaseResourceName}.")]
    public static partial void LogNoConnectionString(ILogger logger, string targetDatabaseResourceName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to import database {TargetDatabaseResourceName}.")]
    public static partial void LogFailedDatabaseImport(ILogger logger, Exception exception, string targetDatabaseResourceName);
}