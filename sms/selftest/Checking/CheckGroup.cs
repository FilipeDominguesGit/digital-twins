namespace SmsTwin.SelfTest.Checking;

/// <summary>A named set of checks. The twin is reset before a group runs, so a group builds all the state it needs.</summary>
internal abstract class CheckGroup
{
    public abstract string Name { get; }

    public virtual GroupMode Mode => GroupMode.Always;

    public abstract Task Run(CheckContext c);
}
