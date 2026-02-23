# DemoApp.FigmaClone

A collaborative canvas editor demo built with Avalonia and Automerge CRDTs. Demonstrates spatial data operations: maps for shape properties, lists for layers, doubles for coordinates, and real-time sync via Repo.

## What It Shows

- **Map operations**: Each shape is an Automerge map with typed properties (strings for name/color, doubles for position/size, int64 for z-order)
- **Double values**: Position (x, y) and dimensions (width, height) use Automerge's f64 type for precise coordinate storage
- **List operations**: Layers and their shapes are stored in Automerge lists, supporting ordered insert and remove
- **Nested containers**: The document schema nests shape maps inside layer maps inside a root list
- **Real-time sync**: Uses RepoService + SignalR for automatic peer-to-peer document synchronization
- **DocHandle events**: Subscribes to DocHandle.Changed for remote change detection and UI refresh

## Document Schema

```
root (map)
  └── "layers" (list)
        └── [0..n] (map)
              ├── "name"      (string)
              ├── "visible"   (boolean)
              └── "shapes"    (list)
                    └── [0..m] (map)
                          ├── "name"    (string)
                          ├── "type"    (string)  — "Rectangle" | "Ellipse"
                          ├── "x"       (f64)
                          ├── "y"       (f64)
                          ├── "width"   (f64)
                          ├── "height"  (f64)
                          ├── "color"   (string)  — hex "#RRGGBB"
                          └── "zOrder"  (int64)
```

## Running

```bash
dotnet run --project apps/DemoApp.FigmaClone
```

## Architecture

```
CanvasViewModel
  ├── AutomergeService (wraps DocHandle's document)
  │     └── AutomergeDocument
  │           └── Layers (List of Maps)
  │                 └── Shapes (List of Maps)
  ├── RepoService (SignalR-based sync)
  │     └── Repo → DocHandle → auto-sync
  └── DocHandle.Changed → RefreshFromDocument()
```

- **AutomergeService** wraps `AutomergeDocument` with typed operations for shapes and layers
- **RepoService** manages SignalR connection and document lifecycle (create, join, resume)
- **DocHandle** provides automatic sync — local changes are broadcast, remote changes trigger events
- **CanvasViewModel** exposes observable shape and layer collections, selection state, and commands
- **MainWindow** renders shapes on an Avalonia Canvas with pointer-based drawing, dragging, and selection

## Key APIs Used

| API | Purpose |
|-----|---------|
| `PutObject(id, key, Map)` | Create a shape or layer map |
| `PutObject(id, key, List)` | Create a shapes list inside a layer |
| `PutString(id, key, value)` | Set name, type, color |
| `PutDouble(id, key, value)` | Set x, y, width, height coordinates |
| `PutInt64(id, key, value)` | Set z-order |
| `PutBoolean(id, key, value)` | Set layer visibility |
| `ListInsertObject(list, index, Map)` | Add a shape or layer |
| `ListRemove(list, index)` | Remove a shape or layer |
| `GetDouble(id, key)` | Read coordinate values |
| `GetBoolean(id, key)` | Read visibility state |
| `DocHandle.Changed` | React to remote document changes |
| `AutomergeService.FromDocument()` | Wrap existing document without ownership |
