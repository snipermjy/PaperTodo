# PaperTodo

PaperTodo is a lightweight Windows desktop app for personal todos and quick notes.

Instead of a central task-management screen, every todo paper and note paper is its own window. Keep the things that matter on your desktop, collapse individual papers into edge-docked capsules when you need space, and sync the day's work to Obsidian when you are ready.

## What it includes

- Independent todo papers for creating, editing, completing, and clearing tasks.
- Independent note papers for ideas, longer text, Markdown, tags, and images.
- Links from todo items to related notes.
- Per-paper desktop capsules for both todos and notes. Capsules can dock to screen edges and reveal their close control on hover.
- Multi-monitor-friendly edge docking and dragging.
- Manual and scheduled daily Obsidian sync.

## Obsidian sync

Choose an Obsidian Vault, an output subdirectory, and a daily sync time in Settings. PaperTodo creates a date-based Markdown note containing the current day's todo items and notes updated that day.

Sync updates only the marked PaperTodo block in the daily file, preserving your own text outside that block. The app rejects invalid Vault paths and never writes outside the configured Vault.

## Privacy

- No account or cloud service is required.
- App data is stored locally in `data.json` with a local backup.
- Obsidian sync writes Markdown only to the Vault you explicitly configure.

## Run

### Development

Windows and the .NET 10 SDK are required.

```powershell
dotnet run --project .\PaperTodo.csproj
```

### Build

```powershell
dotnet build .\PaperTodo.csproj
```

The build output is written to `输出\PaperTodo-v<version>\`.

## Technology

- Windows desktop application
- .NET 10
- WPF

PaperTodo keeps daily tasks visible, makes ideas easy to capture, and lets Obsidian become the long-term home for the day.
