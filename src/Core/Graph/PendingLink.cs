namespace Snail.MCP.Memory.Core.Graph;

/// <summary>An unconfirmed link suggestion between two projects, queued until confirmed or dismissed.</summary>
/// <remarks>The pair is undirected: direction and type appear only when <c>link_projects</c> confirms it.</remarks>
/// <param name="Project">Project whose record produced the suggestion.</param>
/// <param name="Other">The mentioned project.</param>
/// <param name="Evidence">One-line handle of the record that triggered the suggestion; may be empty.</param>
/// <param name="Updated">When the suggestion was queued or last touched.</param>
public sealed record PendingLink(string Project, string Other, string Evidence, DateTimeOffset Updated);
