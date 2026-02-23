# DemoApp.TaskBoard

A Kanban-style task board demo built with Avalonia and Automerge CRDTs. Demonstrates structured CRDT operations: maps for columns and tasks, lists for ordering, and counters for votes.

## What It Shows

- **Map operations**: Each column and task is an Automerge map with typed properties (strings, integers, counters)
- **List operations**: Columns and tasks are stored in Automerge lists, supporting ordered insert, remove, and move
- **Counter CRDT**: Vote counts use Automerge's counter type, which merges concurrent increments without conflicts
- **Nested containers**: The document schema nests maps inside lists inside maps, showing how to build rich data structures with CRDTs

## Document Schema

```
root (map)
  └── "columns" (list)
        └── [0..n] (map)
              ├── "name"   (string)
              ├── "order"  (int64)
              └── "tasks"  (list)
                    └── [0..m] (map)
                          ├── "title"       (string)
                          ├── "description" (string)
                          ├── "status"      (string)
                          └── "votes"       (counter)
```

## Running

```bash
dotnet run --project apps/DemoApp.TaskBoard
```

## Architecture

```
BoardViewModel
  └── AutomergeService
        └── AutomergeDocument
              └── Columns (List of Maps)
                    └── Tasks (List of Maps with Counters)
```

- **AutomergeService** wraps `AutomergeDocument` with typed CRUD operations for columns and tasks
- **BoardViewModel** exposes observable collections and commands for the Avalonia UI
- **MainWindow** renders a horizontal column layout with vertical task lists

## Key APIs Used

| API | Purpose |
|-----|---------|
| `PutObject(id, key, Map)` | Create a column or task map |
| `PutObject(id, key, List)` | Create a tasks list inside a column |
| `PutString(id, key, value)` | Set name, title, description, status |
| `PutInt64(id, key, value)` | Set column order |
| `PutCounter(id, key, value)` | Initialize vote counter |
| `Increment(id, key, delta)` | Increment vote counter |
| `ListInsertObject(list, index, Map)` | Add a column or task |
| `ListRemove(list, index)` | Remove a column or task |
| `ListLength(list)` | Get count of items |
| `ListGetObject(list, index)` | Read a nested object |
| `GetString(id, key)` | Read string properties |
| `GetCounter(id, key)` | Read counter value |
