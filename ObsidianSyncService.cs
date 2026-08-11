using System.IO;
using System.Text;
using System.Windows.Threading;

namespace PaperTodo;

internal enum ObsidianSyncStatus
{
    Succeeded,
    Disabled,
    Busy,
    Failed
}

internal sealed record ObsidianSyncResult(ObsidianSyncStatus Status, string? FilePath = null)
{
    public static ObsidianSyncResult Succeeded(string filePath) => new(ObsidianSyncStatus.Succeeded, filePath);
    public static ObsidianSyncResult Disabled() => new(ObsidianSyncStatus.Disabled);
    public static ObsidianSyncResult Busy() => new(ObsidianSyncStatus.Busy);
    public static ObsidianSyncResult Failed() => new(ObsidianSyncStatus.Failed);
}

internal sealed class ObsidianSyncService : IDisposable
{
    private const string SyncBlockStart = "<!-- PaperTodo Obsidian Sync Start -->";
    private const string SyncBlockEnd = "<!-- PaperTodo Obsidian Sync End -->";
    private readonly AppController _controller;
    private readonly DispatcherTimer _timer;
    private bool _isSyncing;

    public ObsidianSyncService(AppController controller)
    {
        _controller = controller;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _timer.Tick += async (_, _) => await SyncScheduledAsync();
    }

    public void Start() => _timer.Start();

    public void Dispose() => _timer.Stop();

    public async Task<ObsidianSyncResult> SyncTodayAsync()
    {
        if (_isSyncing)
        {
            return ObsidianSyncResult.Busy();
        }

        var state = _controller.State;
        if (string.IsNullOrWhiteSpace(state.ObsidianVaultPath))
        {
            return ObsidianSyncResult.Disabled();
        }

        _isSyncing = true;
        try
        {
            _controller.CommitPendingNoteContentsForSave();

            var today = DateTime.Today;
            var outputDirectory = ResolveOutputDirectory(state);
            Directory.CreateDirectory(outputDirectory);
            var filePath = Path.Combine(outputDirectory, $"{today:yyyy-MM-dd}.md");
            var markdown = BuildDailyMarkdown(today, state.Papers);
            var existingMarkdown = File.Exists(filePath)
                ? await File.ReadAllTextAsync(filePath)
                : "";

            await WriteAtomicallyAsync(filePath, MergeDailyMarkdown(existingMarkdown, markdown));
            state.LastObsidianSyncAt = DateTimeOffset.Now;
            _controller.MarkDirty();
            return ObsidianSyncResult.Succeeded(filePath);
        }
        catch (Exception)
        {
            return ObsidianSyncResult.Failed();
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private async Task SyncScheduledAsync()
    {
        var state = _controller.State;
        var now = DateTime.Now;
        if (string.IsNullOrWhiteSpace(state.ObsidianVaultPath) ||
            now.TimeOfDay < state.ObsidianSyncTime ||
            state.LastObsidianSyncAt?.LocalDateTime.Date == now.Date)
        {
            return;
        }

        await SyncTodayAsync();
    }

    private static string ResolveOutputDirectory(AppState state)
    {
        var vaultPath = Path.GetFullPath(state.ObsidianVaultPath);
        if (!Directory.Exists(vaultPath))
        {
            throw new DirectoryNotFoundException();
        }

        var configuredOutput = state.ObsidianOutputDirectory?.Trim() ?? "";
        if (Path.IsPathRooted(configuredOutput))
        {
            throw new InvalidOperationException();
        }

        var outputDirectory = Path.GetFullPath(Path.Combine(vaultPath, configuredOutput));
        var relativePath = Path.GetRelativePath(vaultPath, outputDirectory);
        if (relativePath == ".." || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException();
        }

        return outputDirectory;
    }

    private static string BuildDailyMarkdown(DateTime today, IEnumerable<PaperData> papers)
    {
        var paperList = papers.ToList();
        var builder = new StringBuilder();
        builder.AppendLine(SyncBlockStart);
        builder.AppendLine($"# {today:yyyy-MM-dd}");
        builder.AppendLine();
        builder.AppendLine("## 待办");
        builder.AppendLine();

        foreach (var paper in paperList.Where(paper => paper.Type == PaperTypes.Todo))
        {
            builder.AppendLine($"### {DisplayTitle(paper)}");
            foreach (var item in paper.Items.OrderBy(item => item.Order))
            {
                builder.AppendLine($"- [{(item.Done ? "x" : " ")}] {item.Text}");
            }
            builder.AppendLine();
        }

        builder.AppendLine("## 灵感");
        builder.AppendLine();
        foreach (var paper in paperList.Where(paper =>
                     paper.Type == PaperTypes.Note &&
                     paper.UpdatedAt != DateTimeOffset.MinValue &&
                     paper.UpdatedAt.LocalDateTime.Date == today.Date &&
                     !string.IsNullOrWhiteSpace(paper.Content)))
        {
            builder.AppendLine($"### {DisplayTitle(paper)}");
            builder.AppendLine(paper.Content.Trim());
            if (!string.IsNullOrWhiteSpace(paper.Tags))
            {
                builder.AppendLine();
                builder.AppendLine(paper.Tags.Trim());
            }
            builder.AppendLine();
        }

        builder.AppendLine(SyncBlockEnd);
        return builder.ToString();
    }

    private static string MergeDailyMarkdown(string existingMarkdown, string syncMarkdown)
    {
        var start = existingMarkdown.IndexOf(SyncBlockStart, StringComparison.Ordinal);
        if (start >= 0)
        {
            var end = existingMarkdown.IndexOf(SyncBlockEnd, start + SyncBlockStart.Length, StringComparison.Ordinal);
            if (end >= 0)
            {
                return string.Concat(
                    existingMarkdown.AsSpan(0, start),
                    syncMarkdown,
                    existingMarkdown.AsSpan(end + SyncBlockEnd.Length));
            }
        }

        if (string.IsNullOrWhiteSpace(existingMarkdown))
        {
            return syncMarkdown;
        }

        return string.Concat(existingMarkdown.TrimEnd(), Environment.NewLine, Environment.NewLine, syncMarkdown);
    }

    private static async Task WriteAtomicallyAsync(string filePath, string markdown)
    {
        var temporaryPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, markdown, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string DisplayTitle(PaperData paper)
    {
        return string.IsNullOrWhiteSpace(paper.Title)
            ? (paper.Type == PaperTypes.Todo ? "待办" : "灵感")
            : paper.Title.Trim();
    }
}
