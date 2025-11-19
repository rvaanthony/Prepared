var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Prepared_Client>("prepared-client");

builder.Build().Run();
