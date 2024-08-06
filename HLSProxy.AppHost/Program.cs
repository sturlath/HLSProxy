var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.HLSProxy_ApiService>("apiservice");

builder.AddProject<Projects.HLSProxy_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService);

builder.Build().Run();
