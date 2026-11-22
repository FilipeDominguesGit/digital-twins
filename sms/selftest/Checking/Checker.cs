namespace SmsTwin.SelfTest.Checking;

/// <summary>Counts the checks per group and reports the failures with their group's name.</summary>
internal sealed class Checker
{
    private readonly List<GroupResult> groups = [];

    public int PassedCount => groups.Sum(g => g.Passed);

    public int FailedCount => groups.Sum(g => g.Failures.Count);

    public void Begin(string group) => groups.Add(new GroupResult(group));

    public void Check(string name, bool ok, string detail = "")
    {
        var group = groups[^1];
        if (ok)
        {
            group.Passed++;
        }
        else
        {
            group.Failures.Add(detail.Length > 0 ? $"{name}: {detail}" : name);
        }
    }

    public void Crash(Exception e) => groups[^1].Failures.Add($"the group stopped: {e.GetType().Name}: {e.Message}");

    public void Report(TextWriter output)
    {
        foreach (var group in groups)
        {
            output.WriteLine($"{group.Passed}/{group.Passed + group.Failures.Count} {group.Name}");
        }

        output.WriteLine($"{PassedCount}/{PassedCount + FailedCount} passed");
        foreach (var group in groups)
        {
            foreach (var failure in group.Failures)
            {
                output.WriteLine($"FAIL [{group.Name}] {failure}");
            }
        }
    }
}
