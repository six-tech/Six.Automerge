# Collaborative Rich Text Editor

A desktop demo application demonstrating real-time collaborative rich text editing using Automerge CRDTs with Avalonia UI.

## Features

- **Rich text formatting**: Bold, italic, underline, and link marks using the Automerge text CRDT
- **Block structure**: Paragraph splitting and joining via Automerge block markers
- **Real-time sync**: Bidirectional sync between local and peer documents on a 500ms timer
- **Formatting toolbar**: Toggle buttons that reflect the active marks at the cursor position
- **Split-pane UI**: Editable local document on the left, read-only peer view on the right

## Architecture

```
EditorViewModel
  |
  +-- AutomergeService (local)   -- wraps AutomergeDocument with rich text ops
  |     |
  +-- AutomergeService (peer)    -- forked from local for shared history
  |     |
  +-- SyncService                -- bidirectional sync on 500ms timer
  |     |
  |     +-- AutomergeSyncState (local to peer)
  |     +-- AutomergeSyncState (peer to local)
  |
  +-- FormattingState            -- tracks active marks at cursor position
```

### Key Components

- **AutomergeService**: Wraps `AutomergeDocument` with rich text operations (SpliceText, Mark/Unmark for bold/italic/underline/link, SplitBlock/JoinBlock, GetSpans)
- **SyncService**: Runs the Automerge sync protocol between two `AutomergeService` instances on a timer
- **FormattingState**: Reads marks from the document and determines which formatting is active at the cursor
- **EditorViewModel**: Bridges the CRDT layer to Avalonia UI with reactive bindings and formatting commands

### Peer Forking

Unlike the plain text editor demo, this app uses `AutomergeDocument.Fork()` to create the peer. This ensures both services share the same text object ID and history, enabling true collaborative sync where marks and text propagate correctly between peers.

## Running

```bash
dotnet run --project apps/DemoApp.CollaborativeEditor
```

## APIs Demonstrated

| API | Purpose |
|-----|---------|
| `AutomergeDocument.Create()` | Create a new CRDT document |
| `AutomergeDocument.Fork()` | Fork a document for peer sync |
| `PutObject(root, key, Text)` | Create a Text container |
| `SpliceText(id, index, del, ins)` | Insert and delete text |
| `Mark(id, start, end, name, value, expand)` | Apply formatting marks |
| `Unmark(id, name, start, end, expand)` | Remove formatting marks |
| `GetText(id)` | Read current text |
| `GetMarks(id)` | Read active marks |
| `GetSpans(id)` | Read text segmented by marks and blocks |
| `SplitBlock(id, index)` | Insert a block marker |
| `JoinBlock(id, index)` | Remove a block marker |
| `Commit(message)` | Commit pending changes |
| `Save()` / `Load(bytes)` | Full document persistence |
| `CreateSyncState()` | Initialize sync protocol |
| `GenerateSyncMessage(state)` | Produce a sync message |
| `ReceiveSyncMessage(state, msg)` | Apply a sync message |
