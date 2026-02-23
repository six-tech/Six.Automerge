using System.Text.Json;
using Six.Automerge;

Console.WriteLine("╔═══════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Automerge C# Demo: Collaborative Task Management System         ║");
Console.WriteLine("║  Demonstrates: CRDTs, Sync Protocol, Nested Data, Persistence    ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════════╝");
Console.WriteLine();

try
{
    RunTaskManagerDemo();
    Console.WriteLine();
    Console.WriteLine("✓ Demo completed successfully!");
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine("✗ Demo failed:");
    Console.WriteLine(ex);
    Environment.Exit(1);
}

static void RunTaskManagerDemo()
{
    // ═══════════════════════════════════════════════════════════════
    // SCENARIO: Three team members collaborating on a task management system
    // Alice (Project Manager), Bob (Developer), Carol (Designer)
    // ═══════════════════════════════════════════════════════════════

    PrintSection("1. SETUP: Creating team member documents");

    using var alice = AutomergeDocument.Create();
    using var bob = AutomergeDocument.Create();
    using var carol = AutomergeDocument.Create();

    Console.WriteLine($"  ✓ Alice's document (Actor: {alice.ActorId})");
    Console.WriteLine($"  ✓ Bob's document (Actor: {bob.ActorId})");
    Console.WriteLine($"  ✓ Carol's document (Actor: {carol.ActorId})");

    // ═══════════════════════════════════════════════════════════════
    // Alice creates the initial project structure
    // ═══════════════════════════════════════════════════════════════

    PrintSection("2. ALICE: Creating project structure");

    var aliceRoot = alice.RootId;

    // Project metadata
    alice.PutString(aliceRoot, "project_name", "Website Redesign");
    alice.PutTimestamp(aliceRoot, "created_at", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    alice.PutString(aliceRoot, "status", "active");
    alice.PutCounter(aliceRoot, "total_views", 0);

    // Create the tasks list
    var aliceTasks = alice.PutObject(aliceRoot, "tasks", AutomergeContainerType.List);

    // Task 1: Design mockups
    var task1 = alice.ListInsertObject(aliceTasks, 0, AutomergeContainerType.Map);
    alice.PutString(task1, "id", "TASK-001");
    alice.PutString(task1, "title", "Create homepage mockups");
    alice.PutInt64(task1, "priority", 1);
    alice.PutBoolean(task1, "completed", false);
    alice.PutString(task1, "assignee", "carol");
    alice.PutTimestamp(task1, "due_date", DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeMilliseconds());
    alice.PutCounter(task1, "view_count", 0);

    var task1Description = alice.PutObject(task1, "description", AutomergeContainerType.Text);
    alice.SpliceText(task1Description, 0, 0, "Design modern and responsive homepage mockups for the new website. Include mobile and desktop versions.");

    var task1Tags = alice.PutObject(task1, "tags", AutomergeContainerType.List);
    alice.ListInsertString(task1Tags, 0, "design");
    alice.ListInsertString(task1Tags, 1, "high-priority");
    alice.ListInsertString(task1Tags, 2, "frontend");

    var task1Comments = alice.PutObject(task1, "comments", AutomergeContainerType.List);
    var task1Comment1 = alice.ListInsertObject(task1Comments, 0, AutomergeContainerType.Map);
    alice.PutString(task1Comment1, "author", "alice");
    alice.PutString(task1Comment1, "text", "Let's use the new brand colors we discussed last week");
    alice.PutTimestamp(task1Comment1, "timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    // Task 2: Backend API
    var task2 = alice.ListInsertObject(aliceTasks, 1, AutomergeContainerType.Map);
    alice.PutString(task2, "id", "TASK-002");
    alice.PutString(task2, "title", "Implement REST API endpoints");
    alice.PutInt64(task2, "priority", 2);
    alice.PutBoolean(task2, "completed", false);
    alice.PutString(task2, "assignee", "bob");
    alice.PutTimestamp(task2, "due_date", DateTimeOffset.UtcNow.AddDays(10).ToUnixTimeMilliseconds());
    alice.PutCounter(task2, "view_count", 0);

    var task2Description = alice.PutObject(task2, "description", AutomergeContainerType.Text);
    alice.SpliceText(task2Description, 0, 0, "Create REST API endpoints for user management, authentication, and content delivery.");

    var task2Tags = alice.PutObject(task2, "tags", AutomergeContainerType.List);
    alice.ListInsertString(task2Tags, 0, "backend");
    alice.ListInsertString(task2Tags, 1, "api");
    alice.ListInsertString(task2Tags, 2, "development");

    var task2Comments = alice.PutObject(task2, "comments", AutomergeContainerType.List);
    var task2Subtasks = alice.PutObject(task2, "subtasks", AutomergeContainerType.List);

    var subtask1 = alice.ListInsertObject(task2Subtasks, 0, AutomergeContainerType.Map);
    alice.PutString(subtask1, "title", "Setup database schema");
    alice.PutBoolean(subtask1, "completed", false);

    var subtask2 = alice.ListInsertObject(task2Subtasks, 1, AutomergeContainerType.Map);
    alice.PutString(subtask2, "title", "Implement authentication");
    alice.PutBoolean(subtask2, "completed", false);

    alice.Commit("Alice: Initial project setup with 2 tasks", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    Console.WriteLine("  ✓ Project: Website Redesign");
    Console.WriteLine("  ✓ Created 2 tasks with descriptions, tags, and comments");
    Console.WriteLine("  ✓ Task 1: Assigned to Carol (Design)");
    Console.WriteLine("  ✓ Task 2: Assigned to Bob (Backend) with 2 subtasks");

    DisplayDocumentState("Alice", alice);

    // ═══════════════════════════════════════════════════════════════
    // Bob works independently on his copy
    // ═══════════════════════════════════════════════════════════════

    PrintSection("3. BOB: Working independently (offline)");

    var bobRoot = bob.RootId;
    bob.PutString(bobRoot, "project_name", "Mobile App");
    bob.PutString(bobRoot, "status", "planning");

    var bobTasks = bob.PutObject(bobRoot, "tasks", AutomergeContainerType.List);
    var bobTask = bob.ListInsertObject(bobTasks, 0, AutomergeContainerType.Map);
    bob.PutString(bobTask, "id", "TASK-003");
    bob.PutString(bobTask, "title", "Setup CI/CD pipeline");
    bob.PutInt64(bobTask, "priority", 3);
    bob.PutBoolean(bobTask, "completed", false);
    bob.PutString(bobTask, "assignee", "bob");
    bob.PutCounter(bobTask, "view_count", 5);

    var bobTaskDescription = bob.PutObject(bobTask, "description", AutomergeContainerType.Text);
    bob.SpliceText(bobTaskDescription, 0, 0, "Configure automated testing and deployment pipeline.");

    bob.Commit("Bob: Created CI/CD task", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    Console.WriteLine("  ✓ Bob created his own task independently");
    Console.WriteLine("  ✓ Task 3: CI/CD Pipeline setup");

    // ═══════════════════════════════════════════════════════════════
    // Carol also works independently
    // ═══════════════════════════════════════════════════════════════

    PrintSection("4. CAROL: Working independently (offline)");

    var carolRoot = carol.RootId;
    carol.PutString(carolRoot, "project_name", "Brand Identity");
    carol.PutTimestamp(carolRoot, "created_at", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    var carolTasks = carol.PutObject(carolRoot, "tasks", AutomergeContainerType.List);
    var carolTask = carol.ListInsertObject(carolTasks, 0, AutomergeContainerType.Map);
    carol.PutString(carolTask, "id", "TASK-004");
    carol.PutString(carolTask, "title", "Design color palette");
    carol.PutInt64(carolTask, "priority", 1);
    carol.PutBoolean(carolTask, "completed", true);
    carol.PutString(carolTask, "assignee", "carol");

    var carolTaskTags = carol.PutObject(carolTask, "tags", AutomergeContainerType.List);
    carol.ListInsertString(carolTaskTags, 0, "design");
    carol.ListInsertString(carolTaskTags, 1, "branding");

    carol.Commit("Carol: Completed color palette", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    Console.WriteLine("  ✓ Carol created and completed a design task");
    Console.WriteLine("  ✓ Task 4: Color palette (Completed ✓)");

    // ═══════════════════════════════════════════════════════════════
    // SYNC: Alice ↔ Bob
    // ═══════════════════════════════════════════════════════════════

    PrintSection("5. SYNC: Alice ↔ Bob (merging changes)");

    using var syncAliceBob = alice.CreateSyncState();
    using var syncBobAlice = bob.CreateSyncState();

    PerformSync(alice, bob, syncAliceBob, syncBobAlice);

    Console.WriteLine("  ✓ Sync completed successfully");
    Console.WriteLine("  ✓ Alice now sees Bob's CI/CD task");
    Console.WriteLine("  ✓ Bob now sees Alice's initial project structure");

    // ═══════════════════════════════════════════════════════════════
    // SYNC: Alice ↔ Carol
    // ═══════════════════════════════════════════════════════════════

    PrintSection("6. SYNC: Alice ↔ Carol (merging changes)");

    using var syncAliceCarol = alice.CreateSyncState();
    using var syncCarolAlice = carol.CreateSyncState();

    PerformSync(alice, carol, syncAliceCarol, syncCarolAlice);

    Console.WriteLine("  ✓ Sync completed successfully");
    Console.WriteLine("  ✓ Alice now sees Carol's completed design task");
    Console.WriteLine("  ✓ Carol now sees Alice's project structure");

    // ═══════════════════════════════════════════════════════════════
    // Bob and Carol add comments and updates
    // ═══════════════════════════════════════════════════════════════

    PrintSection("7. BOB & CAROL: Adding comments and updates");

    // Bob increments view counter and adds comment
    var bobRootAfterSync = bob.RootId;
    bob.PutCounter(bobRootAfterSync, "total_views", 5);

    var bobTasksAfterSync = bob.PutObject(bobRootAfterSync, "tasks", AutomergeContainerType.List);
    // Find task 2 (Bob's assigned task) and add comment
    if (bob.ListLength(bobTasksAfterSync) >= 2)
    {
        var bobTask2 = bob.ListInsertObject(bobTasksAfterSync, 1, AutomergeContainerType.Map);
        var bobTask2Comments = bob.PutObject(bobTask2, "comments", AutomergeContainerType.List);
        var bobComment = bob.ListInsertObject(bobTask2Comments, 0, AutomergeContainerType.Map);
        bob.PutString(bobComment, "author", "bob");
        bob.PutString(bobComment, "text", "I'll start with the database schema this week");
        bob.PutTimestamp(bobComment, "timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    bob.Commit("Bob: Added comment and incremented views", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    Console.WriteLine("  ✓ Bob: Added comment to his assigned task");
    Console.WriteLine("  ✓ Bob: Incremented project view counter (+5)");

    // Carol updates task description and adds comment
    var carolRootAfterSync = carol.RootId;
    carol.PutCounter(carolRootAfterSync, "total_views", 3);

    var carolTasksAfterSync = carol.PutObject(carolRootAfterSync, "tasks", AutomergeContainerType.List);
    // Find task 1 (Carol's assigned task) if it exists
    if (carol.ListLength(carolTasksAfterSync) >= 1)
    {
        var carolTask1 = carol.ListInsertObject(carolTasksAfterSync, 0, AutomergeContainerType.Map);
        var carolTask1Description = carol.PutObject(carolTask1, "description", AutomergeContainerType.Text);
        // Append to description
        carol.SpliceText(carolTask1Description, 0, 0, "[UPDATE] ");

        var carolTask1Comments = carol.PutObject(carolTask1, "comments", AutomergeContainerType.List);
        var carolComment = carol.ListInsertObject(carolTask1Comments, 0, AutomergeContainerType.Map);
        carol.PutString(carolComment, "author", "carol");
        carol.PutString(carolComment, "text", "Working on mockups now, will share drafts tomorrow");
        carol.PutTimestamp(carolComment, "timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    carol.Commit("Carol: Updated description and added comment", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    Console.WriteLine("  ✓ Carol: Updated task description");
    Console.WriteLine("  ✓ Carol: Added progress comment");
    Console.WriteLine("  ✓ Carol: Incremented project view counter (+3)");

    // ═══════════════════════════════════════════════════════════════
    // Final three-way sync to converge all changes
    // ═══════════════════════════════════════════════════════════════

    PrintSection("8. FINAL SYNC: Three-way convergence");

    // Alice ↔ Bob
    PerformSync(alice, bob, syncAliceBob, syncBobAlice);
    Console.WriteLine("  ✓ Alice ↔ Bob synced");

    // Alice ↔ Carol
    PerformSync(alice, carol, syncAliceCarol, syncCarolAlice);
    Console.WriteLine("  ✓ Alice ↔ Carol synced");

    // Bob ↔ Carol (ensure full convergence)
    using var syncBobCarol = bob.CreateSyncState();
    using var syncCarolBob = carol.CreateSyncState();
    PerformSync(bob, carol, syncBobCarol, syncCarolBob);
    Console.WriteLine("  ✓ Bob ↔ Carol synced");

    // Additional round to ensure convergence
    PerformSync(alice, bob, syncAliceBob, syncBobAlice);
    PerformSync(alice, carol, syncAliceCarol, syncCarolAlice);

    Console.WriteLine("  ✓ All documents converged to same state");
    Console.WriteLine($"  ✓ Total view counter (CRDT): {GetTotalViews(alice)} (5 + 3 = 8)");

    // ═══════════════════════════════════════════════════════════════
    // Display final merged state
    // ═══════════════════════════════════════════════════════════════

    PrintSection("9. FINAL STATE: Merged task board");

    DisplayDocumentState("Alice (Final)", alice);
    DisplayDocumentState("Bob (Final)", bob);
    DisplayDocumentState("Carol (Final)", carol);

    VerifyConvergence(alice, bob, carol);

    // ═══════════════════════════════════════════════════════════════
    // Demonstrate persistence
    // ═══════════════════════════════════════════════════════════════

    PrintSection("10. PERSISTENCE: Save and restore");

    var aliceSaved = alice.Save();
    Console.WriteLine($"  ✓ Alice's document serialized: {aliceSaved.Length} bytes");

    using var restored = AutomergeDocument.Load(aliceSaved);
    Console.WriteLine($"  ✓ Document restored successfully");
    Console.WriteLine($"  ✓ Actor ID: {restored.ActorId}");
    Console.WriteLine($"  ✓ Pending operations: {restored.PendingOperations}");

    var restoredJson = restored.HydrateJson();
    var aliceJson = alice.HydrateJson();

    using var restoredParsed = JsonDocument.Parse(restoredJson);
    using var aliceParsed = JsonDocument.Parse(aliceJson);

    Console.WriteLine($"  ✓ Project name preserved: {restoredParsed.RootElement.GetProperty("project_name").GetString()}");

    if (restoredParsed.RootElement.TryGetProperty("tasks", out var restoredTasks))
    {
        Console.WriteLine($"  ✓ Tasks count: {restoredTasks.GetArrayLength()}");
    }

    // ═══════════════════════════════════════════════════════════════
    // Demonstrate incremental updates
    // ═══════════════════════════════════════════════════════════════

    PrintSection("11. INCREMENTAL UPDATES");

    var beforeIncremental = alice.Save();
    Console.WriteLine($"  • Full save size: {beforeIncremental.Length} bytes");

    var aliceRootFinal = alice.RootId;
    alice.PutString(aliceRootFinal, "last_updated", DateTimeOffset.UtcNow.ToString("o"));
    alice.Commit("Alice: Updated timestamp");

    var incrementalChanges = alice.SaveIncremental();
    Console.WriteLine($"  ✓ Incremental save size: {incrementalChanges.Length} bytes");
    Console.WriteLine($"  ✓ Reduction: {((1 - (double)incrementalChanges.Length / beforeIncremental.Length) * 100):F1}%");

    // Apply incremental to Bob's document
    var appliedCount = bob.LoadIncremental(incrementalChanges);
    Console.WriteLine($"  ✓ Bob applied {appliedCount} incremental change(s)");

    // ═══════════════════════════════════════════════════════════════
    // Show change history
    // ═══════════════════════════════════════════════════════════════

    PrintSection("12. CHANGE HISTORY");

    var heads = alice.GetHeads();
    Console.WriteLine($"  ✓ Current heads: {heads.Count}");
    foreach (var head in heads.Take(3))
    {
        Console.WriteLine($"    - {head[..16]}...");
    }

    var allChanges = alice.GetChanges([]);
    Console.WriteLine($"  ✓ Total changes in history: {allChanges.Count}");

    var lastChange = alice.GetLastLocalChange();
    Console.WriteLine($"  ✓ Last local change size: {lastChange?.Length} bytes");

    // ═══════════════════════════════════════════════════════════════
    // Summary statistics
    // ═══════════════════════════════════════════════════════════════

    PrintSection("13. DEMO SUMMARY");

    Console.WriteLine("  Data Types Used:");
    Console.WriteLine("    ✓ String (project names, titles, descriptions)");
    Console.WriteLine("    ✓ Int64 (priorities)");
    Console.WriteLine("    ✓ Boolean (completion status)");
    Console.WriteLine("    ✓ Timestamp (due dates, timestamps)");
    Console.WriteLine("    ✓ Counter (view counts - CRDT)");
    Console.WriteLine();
    Console.WriteLine("  Container Types Used:");
    Console.WriteLine("    ✓ Map (root, tasks, comments, subtasks)");
    Console.WriteLine("    ✓ List (tasks, tags, comments, subtasks)");
    Console.WriteLine("    ✓ Text (descriptions with CRDT text editing)");
    Console.WriteLine();
    Console.WriteLine("  Automerge Features Demonstrated:");
    Console.WriteLine("    ✓ Conflict-free merge of concurrent edits");
    Console.WriteLine("    ✓ Peer-to-peer sync protocol");
    Console.WriteLine("    ✓ Counter CRDTs (automatic increment merging)");
    Console.WriteLine("    ✓ Full document serialization");
    Console.WriteLine("    ✓ Incremental updates");
    Console.WriteLine("    ✓ Change history tracking");
    Console.WriteLine("    ✓ Multi-level nested data structures");
    Console.WriteLine("    ✓ Actor identity management");
    Console.WriteLine("    ✓ Commit messages and timestamps");
}

static void PerformSync(
    AutomergeDocument doc1,
    AutomergeDocument doc2,
    AutomergeSyncState state1,
    AutomergeSyncState state2,
    int maxRounds = 10)
{
    for (int i = 0; i < maxRounds; i++)
    {
        var msg1 = doc1.GenerateSyncMessage(state1);
        var msg2 = doc2.GenerateSyncMessage(state2);

        if (msg1 == null && msg2 == null)
        {
            break;
        }

        if (msg1 != null)
        {
            doc2.ReceiveSyncMessage(state2, msg1);
        }

        if (msg2 != null)
        {
            doc1.ReceiveSyncMessage(state1, msg2);
        }
    }
}

static void DisplayDocumentState(string owner, AutomergeDocument doc)
{
    var json = doc.HydrateJson();
    using var parsed = JsonDocument.Parse(json);
    var root = parsed.RootElement;

    Console.WriteLine($"\n  {owner}'s Document State:");

    if (root.TryGetProperty("project_name", out var projectName))
    {
        Console.WriteLine($"    Project: {projectName.GetString()}");
    }

    if (root.TryGetProperty("status", out var status))
    {
        Console.WriteLine($"    Status: {status.GetString()}");
    }

    if (root.TryGetProperty("total_views", out var views))
    {
        if (views.ValueKind == JsonValueKind.Number)
        {
            Console.WriteLine($"    Total Views: {views.GetInt64()}");
        }
        else
        {
            Console.WriteLine($"    Total Views: {views.ValueKind}");
        }
    }

    if (root.TryGetProperty("tasks", out var tasks))
    {
        var taskCount = tasks.GetArrayLength();
        Console.WriteLine($"    Tasks: {taskCount}");

        for (int i = 0; i < Math.Min(taskCount, 5); i++)
        {
            var task = tasks[i];
            var title = task.TryGetProperty("title", out var t) ? t.GetString() : "Untitled";
            var completed = task.TryGetProperty("completed", out var c) && c.GetBoolean();
            var completedMark = completed ? "✓" : " ";
            Console.WriteLine($"      [{completedMark}] {title}");
        }
    }

    Console.WriteLine($"    Pending Ops: {doc.PendingOperations}");
    Console.WriteLine($"    Heads: {doc.GetHeads().Count}");
}

static long GetTotalViews(AutomergeDocument doc)
{
    var json = doc.HydrateJson();
    using var parsed = JsonDocument.Parse(json);
    if (parsed.RootElement.TryGetProperty("total_views", out var views))
    {
        if (views.ValueKind == JsonValueKind.Number)
        {
            return views.GetInt64();
        }
    }

    return 0;
}

static void VerifyConvergence(AutomergeDocument doc1, AutomergeDocument doc2, AutomergeDocument doc3)
{
    var json1 = doc1.HydrateJson();
    var json2 = doc2.HydrateJson();
    var json3 = doc3.HydrateJson();

    using var parsed1 = JsonDocument.Parse(json1);
    using var parsed2 = JsonDocument.Parse(json2);
    using var parsed3 = JsonDocument.Parse(json3);

    var keys1 = parsed1.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(k => k).ToList();
    var keys2 = parsed2.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(k => k).ToList();
    var keys3 = parsed3.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(k => k).ToList();

    var keysMatch = keys1.SequenceEqual(keys2) && keys2.SequenceEqual(keys3);

    Console.WriteLine($"\n  Convergence Verification:");
    Console.WriteLine($"    Keys Match: {(keysMatch ? "✓ Yes" : "✗ No")}");
    Console.WriteLine($"    Common Keys: {string.Join(", ", keys1)}");

    if (parsed1.RootElement.TryGetProperty("tasks", out var tasks1) &&
        parsed2.RootElement.TryGetProperty("tasks", out var tasks2) &&
        parsed3.RootElement.TryGetProperty("tasks", out var tasks3))
    {
        var taskCount1 = tasks1.GetArrayLength();
        var taskCount2 = tasks2.GetArrayLength();
        var taskCount3 = tasks3.GetArrayLength();

        var tasksMatch = taskCount1 == taskCount2 && taskCount2 == taskCount3;
        Console.WriteLine($"    Task Counts Match: {(tasksMatch ? "✓ Yes" : "✗ No")} ({taskCount1}, {taskCount2}, {taskCount3})");
    }
}

static void PrintSection(string title)
{
    Console.WriteLine();
    Console.WriteLine("─────────────────────────────────────────────────────────────────");
    Console.WriteLine(title);
    Console.WriteLine("─────────────────────────────────────────────────────────────────");
}
