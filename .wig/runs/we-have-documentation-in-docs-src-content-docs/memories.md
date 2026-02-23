# 2026-02-20 02:20:00 UTC | Spec: we-have-documentation-in-docs-src-content-docs | Task: sprint-1-task-8 | Sprint: Scaffold New Directory Structure and Migrate Existing Pages

**Description**: Update `docs/astro.config.mjs` sidebar configuration for all Sprint 1 pages (Getting Started, Data Types sections). Delete old `automerge/` files that have been migrated. Keep `automerge/index.mdx` as a redirect or remove it.

Docs site uses Starlight (Astro). Build: `cd docs && npm run build`. Sidebar config in `docs/astro.config.mjs` uses `slug:` keys (no file extension). All internal links use absolute paths with base prefix `/Six.Automerge/`. Empty directories created for future sprints: rich-text, history, branching, conflicts, persistence, sync, object-graph, repo/storage, repo/network, examples, api.

# 2026-02-20 03:03:56 UTC | Spec: we-have-documentation-in-docs-src-content-docs | Task: sprint-6-task-8 | Sprint: Conflicts, Persistence, and Sync Documentation

**Description**: Update `docs/astro.config.mjs` sidebar for Conflicts and Persistence sections

MDX files in the Starlight docs site interpret `<` and `>` as JSX. Use words like "under", "over", "less than" instead of angle brackets in prose/tables to avoid build errors.

# 2026-02-20 05:05:25 UTC | Spec: we-have-documentation-in-docs-src-content-docs | Task: sprint-14-task-8 | Sprint: Example App — Collaborative Rich Text Editor

**Description**: Verify the app builds with `dotnet build apps/DemoApp.CollaborativeEditor`

When syncing two Automerge documents, they must share the same text object ID (created from the same history). Use `AutomergeDocument.Fork()` to create a peer that shares the same schema/objects. Two independently created documents with `PutObject(root, "content", Text)` will create separate text objects that don't sync content to each other.

# 2026-02-20 05:44:47 UTC | Spec: we-have-documentation-in-docs-src-content-docs | Task: sprint-16-task-7 | Sprint: Examples Overview, Final Sidebar Config, and Polish

**Description**: Verify the documentation site builds: run `npm run build` or `pnpm build` in `docs/` directory

Documentation code examples were using wrong namespace `Six.Automerge` instead of actual C# namespace `Six.Automerge`. The `Six.Automerge` name is only the documentation site URL base path, not the C# namespace or NuGet package name. Actual namespaces: `Six.Automerge`, `Six.Automerge.FFI`, `Six.Automerge.Repository`, `Six.Automerge.Repository.Storage`, `Six.Automerge.Repository.Network`.

