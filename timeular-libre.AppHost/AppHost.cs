using Projects;

var builder = DistributedApplication.CreateBuilder(args);

// add the backend projects to the Aspire application
// service project has been retired; only use the log API below
var logApi  = builder.AddProject<Timeular_Log>("log");

// register the desktop user interface as another process managed by Aspire
var desktop = builder.AddProject<Timeular_Desktop>("desktop")
    .WithReference(logApi)      // UI might call the log API
    .WaitFor(logApi);

// NOTE: Aspire will start `log` before `desktop` and inject configuration
// references as needed; the desktop is just a normal service from Aspire's
// perspective.

// Aspire can also manage dependent resources such as a database container
// but we handle EF migrations in the API startup instead.


builder.Build().Run();
