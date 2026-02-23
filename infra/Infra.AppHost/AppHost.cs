using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// RESOURCES
// -------------------------------------------------------------------------------


// PROJECTS (APPS)
// -------------------------------------------------------------------------------

// Backend for collaborative apps
var api = builder.AddProject<Projects.Server_Api>("api")
  .WithExternalHttpEndpoints()
  .WithReplicas(1);


// DEMO APPS
// -------------------------------------------------------------------------------

// Collaborative Editor — plain text CRDT demo (3 replicas with isolated storage)
for(int i = 0; i < 3; i++)
{
  builder.AddProject<Projects.DemoApp_CollaborativeEditor>($"collaborative-editor-{i}")
    .WithReference(api)
    .WaitFor(api)
    .WithExplicitStart()
    .WithEnvironment("REPLICA_INDEX", i.ToString())
    .ExcludeFromManifest();
}

// Figma Clone — collaborative canvas demo (3 replicas with isolated storage)
for(int i = 0; i < 3; i++)
{
  builder.AddProject<Projects.DemoApp_CollaborativeDrawing>($"collaborative-drawing-{i}")
    .WithReference(api)
    .WaitFor(api)
    //.WithExplicitStart()
    .WithEnvironment("REPLICA_INDEX", i.ToString())
    .ExcludeFromManifest();
}

// Task Board — collaborative kanban board demo (3 replicas with isolated storage)
for(int i = 0; i < 3; i++)
{
  builder.AddProject<Projects.DemoApp_CollaborativeTaskBoard>($"collaborative-task-board-{i}")
    .WithReference(api)
    .WaitFor(api)
    .WithExplicitStart()
    .WithEnvironment("REPLICA_INDEX", i.ToString())
    .ExcludeFromManifest();
}

builder.Build().Run();
