namespace SmsTwin.Faults;

/// <summary>The fault rules still in use, in creation order.</summary>
internal sealed class FaultRules
{
    private readonly Lock gate = new();
    private readonly List<FaultRule> rules = [];

    public FaultRule Add(string? to, string? bodyContains, FaultAction action, int? status, int? delayMs, int times)
    {
        var rule = new FaultRule($"fault_{Guid.NewGuid():N}", to, bodyContains, action, status, delayMs, times);
        lock (gate)
        {
            rules.Add(rule);
        }

        return rule;
    }

    /// <summary>The first rule in creation order that matches this send, after using one of its <c>times</c>.</summary>
    public FaultRule? Take(string to, string body)
    {
        lock (gate)
        {
            var i = rules.FindIndex(r => r.Matches(to, body));
            if (i < 0)
            {
                return null;
            }

            var rule = rules[i];
            if (rule.Times <= 1)
            {
                rules.RemoveAt(i);
            }
            else
            {
                rules[i] = rule with { Times = rule.Times - 1 };
            }

            return rule;
        }
    }

    public List<FaultRule> List()
    {
        lock (gate)
        {
            return [.. rules];
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            rules.Clear();
        }
    }
}
