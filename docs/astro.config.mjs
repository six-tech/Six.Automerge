import {defineConfig} from 'astro/config';
import starlight from '@astrojs/starlight';
import starlightThemeSix from '@six-tech/starlight-theme-six'

export default defineConfig({
    base: '/Six.Automerge/', // Last fragment for GitHub pages (your repo name) - i.e: /Six.SolutionTemplate/
    site: 'https://six-tech.github.io', // Base URL for your docs site (your GitHub pages URL) - i.e: https://six-tech.github.io
    integrations: [
        starlight({
            title: 'Six.Automerge',
            description: 'Automerge CRDT Bindings',
            editLink: {
                // Base url for edit links on GitHub. Basically edit-url to the docs' folder.
                baseUrl: 'https://github.com/six-tech/Six.SolutionTemplate/edit/main/docs/', // i.e: https://github.com/six-tech/Six.SolutionTemplate
            },
            social: [
                {
                    icon: 'github',
                    label: 'GitHub',
                    href: 'https://github.com/six-tech/Six.SolutionTemplate'
                },
            ],
            plugins: [
                starlightThemeSix({
                    navLinks: [
                        {
                            label: 'Docs',
                            title: 'Link to main documentation page',
                            link: '/automerge',
                        },
                        {
                            label: 'Six',
                            title: 'Link to Six homepage',
                            link: 'https://six.technology',
                            attrs: {
                                target: '_blank',
                                rel: 'noopener',
                            },
                        },
                    ],
                }),
            ],
            sidebar: [
                {
                    label: 'Automerge Library',
                    items: [
                        {label: 'Overview', slug: 'automerge'},
                        {label: 'Guides', slug: 'guides'},
                        {label: 'Getting Started', slug: 'guides/getting-started'},
                        {label: 'Core Concepts', slug: 'guides/core-concepts'},
                    ],
                },
                {
                    label: 'Maps',
                    items: [
                        {label: 'Basic Operations', slug: 'guides/maps/basic-operations'},
                        {label: 'Nested Maps', slug: 'guides/maps/nested-maps'},
                        {label: 'Counters and Timestamps', slug: 'guides/maps/counters-and-timestamps'},
                        {label: 'Advanced Queries', slug: 'guides/maps/advanced-queries'},
                    ],
                },
                {
                    label: 'Lists',
                    items: [
                        {label: 'Basic Operations', slug: 'guides/lists/basic-operations'},
                        {label: 'Splice Operations', slug: 'guides/lists/list-splice'},
                        {label: 'ListPut Operations', slug: 'guides/lists/list-put'},
                        {label: 'Advanced Queries', slug: 'guides/lists/advanced-queries'},
                    ],
                },
                {
                    label: 'Text',
                    items: [
                        {label: 'Basic Operations', slug: 'guides/text/basic-operations'},
                        {label: 'Collaborative Editing', slug: 'guides/text/collaborative-editing'},
                        {label: 'Cursors', slug: 'guides/text/cursors'},
                    ],
                },
                {
                    label: 'Rich Text',
                    items: [
                        {label: 'Overview', slug: 'guides/rich-text'},
                        {label: 'Marks', slug: 'guides/rich-text/marks'},
                        {label: 'Expand Modes', slug: 'guides/rich-text/expand-modes'},
                        {label: 'Reading Marks', slug: 'guides/rich-text/reading-marks'},
                        {label: 'Spans', slug: 'guides/rich-text/spans'},
                        {label: 'Blocks', slug: 'guides/rich-text/blocks'},
                        {label: 'Update Spans', slug: 'guides/rich-text/update-spans'},
                        {label: 'Concurrent Editing', slug: 'guides/rich-text/concurrent-rich-text'},
                    ],
                },
                {
                    label: 'History',
                    items: [
                        {label: 'Commits and Heads', slug: 'guides/history/commits-and-heads'},
                        {label: 'Time Travel', slug: 'guides/history/time-travel'},
                        {label: 'Diffing', slug: 'guides/history/diffing'},
                        {label: 'Change Metadata', slug: 'guides/history/change-metadata'},
                        {label: 'Isolate and Integrate', slug: 'guides/history/isolate-integrate'},
                    ],
                },
                {
                    label: 'Branching',
                    items: [
                        {label: 'Fork and Merge', slug: 'guides/branching/fork-and-merge'},
                        {label: 'Fork at Heads', slug: 'guides/branching/fork-at-heads'},
                        {label: 'Clone', slug: 'guides/branching/clone'},
                    ],
                },
                {
                    label: 'Conflicts',
                    items: [
                        {label: 'Understanding Conflicts', slug: 'guides/conflicts/understanding-conflicts'},
                        {label: 'Detecting Conflicts', slug: 'guides/conflicts/detecting-conflicts'},
                        {label: 'Conflict Values', slug: 'guides/conflicts/conflict-values'},
                    ],
                },
                {
                    label: 'Persistence',
                    items: [
                        {label: 'Save and Load', slug: 'guides/persistence/save-and-load'},
                        {label: 'Incremental Saves', slug: 'guides/persistence/incremental-saves'},
                        {label: 'Save Options', slug: 'guides/persistence/save-options'},
                        {label: 'Storage Strategies', slug: 'guides/persistence/storage-strategies'},
                    ],
                },
                {
                    label: 'Synchronization',
                    items: [
                        {label: 'Sync Protocol', slug: 'guides/sync/sync-protocol'},
                        {label: 'Multi-Peer Sync', slug: 'guides/sync/multi-peer-sync'},
                        {label: 'Sync State Management', slug: 'guides/sync/sync-state-management'},
                        {label: 'Advanced Sync', slug: 'guides/sync/advanced-sync'},
                    ],
                },
                {
                    label: 'Object Graph',
                    items: [
                        {label: 'Navigation', slug: 'guides/object-graph/navigation'},
                        {label: 'Container Types', slug: 'guides/object-graph/container-types'},
                    ],
                },
                {
                    label: 'Repository',
                    items: [
                        {label: 'Overview', slug: 'repo'},
                        {label: 'Getting Started', slug: 'repo/getting-started'},
                        {label: 'Doc Handles', slug: 'repo/doc-handles'},
                        {label: 'Synchronizers', slug: 'repo/synchronizer'},
                        {label: 'Identifiers', slug: 'repo/identifiers'},
                    ],
                },
                {
                    label: 'Storage',
                    items: [
                        {label: 'Storage Adapters', slug: 'repo/storage'},
                        {label: 'InMemory Adapter', slug: 'repo/storage/in-memory'},
                        {label: 'FileSystem Adapter', slug: 'repo/storage/file-system'},
                    ],
                },
                {
                    label: 'Networking',
                    items: [
                        {label: 'Network Adapters', slug: 'repo/network'},
                        {label: 'InMemory Adapter', slug: 'repo/network/in-memory'},
                        {label: 'SignalR Adapter', slug: 'repo/network/signalr'},
                    ],
                },
                {
                    label: 'API Reference',
                    items: [
                        {label: 'Overview', slug: 'api'},
                        {label: 'AutomergeDocument', slug: 'api/automerge-document'},
                        {label: 'AutomergeSyncState', slug: 'api/automerge-sync-state'},
                        {label: 'AutomergeCursor', slug: 'api/automerge-cursor'},
                        {label: 'Spans', slug: 'api/automerge-spans'},
                        {label: 'Marks and Scalar Values', slug: 'api/automerge-marks'},
                        {label: 'Patches', slug: 'api/automerge-patches'},
                        {label: 'Conflict Values', slug: 'api/automerge-conflicts'},
                        {label: 'Options and Utility Types', slug: 'api/automerge-options'},
                        {label: 'Repo', slug: 'api/repo'},
                        {label: 'DocHandle', slug: 'api/doc-handle'},
                        {label: 'Storage', slug: 'api/storage'},
                        {label: 'Network', slug: 'api/network'},
                        {label: 'Identifiers', slug: 'api/identifiers'},
                    ],
                },
                {
                    label: 'Examples',
                    items: [
                        {label: 'Overview', slug: 'examples'},
                        {label: 'Collaborative Text Editor', slug: 'examples/collaborative-text-editor'},
                        {label: 'Collaborative Rich Text Editor', slug: 'examples/collaborative-editor'},
                        {label: 'Task Board', slug: 'examples/collaborative-task-board'},
                        {label: 'Figma Clone', slug: 'examples/collaborative-drawing'},
                    ],
                },
                {
                    label: 'Project Information',
                    items: [
                        {label: 'Introduction', slug: 'introduction'},
                        {label: 'Setup and Configuration', slug: 'introduction/setup-and-configuration'}
                    ],
                },
            ],

        }),
    ],
});

