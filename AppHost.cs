using AspireDacFx;

var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
    .WithImageTag("2025-latest");

var db = sql.AddDatabase("db")
    .WithBacPacImportCommand("");

//var dacfx = builder.AddDacFxImport("dacfx-import")
//    .WaitFor(sql);

builder.Build().Run();
