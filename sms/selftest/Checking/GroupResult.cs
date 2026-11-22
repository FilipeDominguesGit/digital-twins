namespace SmsTwin.SelfTest.Checking;

/// <summary>One group's passed count and failures.</summary>
internal sealed class GroupResult(string name)
{
    public string Name { get; } = name;

    public int Passed { get; set; }

    public List<string> Failures { get; } = [];
}
