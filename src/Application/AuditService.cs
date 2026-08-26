using Snail.MCP.Memory.Core.Formatting;

namespace Snail.MCP.Memory.Application;

/// <summary>The background audit: refills the pending queue from records the write-time scan could not cover.</summary>
/// <remarks>A mention written before the mentioned project existed — or before the queue itself — surfaces only here.</remarks>
public sealed class AuditService(
    IRecordStore records,
    IProjectStore projects,
    IGraphStore graph,
    IPendingLinkStore pending,
    TimeProvider timeProvider)
{
    /// <summary>Scans every project's records for mentions of other known projects that have no edge and no suggestion yet; returns how many suggestions were queued.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var all = await projects.ListAsync(cancellationToken);
        var queued = 0;

        foreach (var project in all)
        {
            var projectKey = ProjectIdentity.Key(project.Name);
            var others = all.Where(other => ProjectIdentity.Key(other.Name) != projectKey).ToList();

            if (others.Count == 0)
                continue;

            var own = await records.ListAsync(new RecordFilter(projectKey), int.MaxValue, cancellationToken);

            if (own.Count == 0)
                continue;

            var linked = (await graph.GetForProjectsAsync([projectKey], cancellationToken))
                .SelectMany(edge => new[] { ProjectIdentity.Key(edge.Source), ProjectIdentity.Key(edge.Target) })
                .ToHashSet();

            var now = timeProvider.GetUtcNow();

            foreach (var other in others.Where(other => !linked.Contains(ProjectIdentity.Key(other.Name))))
            {
                var mention = own.FirstOrDefault(record =>
                    ProjectMentions.Mentions($"{record.Title}\n{record.Content}", other.Name));

                if (mention is null)
                    continue;

                if (await pending.ProposeAsync(
                        new PendingLink(project.Name, other.Name, MemoryFormatter.Label(mention), now), cancellationToken))
                    queued++;
            }
        }

        return queued;
    }
}
