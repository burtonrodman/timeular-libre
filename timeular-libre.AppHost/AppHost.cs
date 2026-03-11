using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var service = builder.AddProject<Timeular_Service>("service");
var web = builder.AddProject<Timeular_Web>("web");

builder.Build().Run();
