namespace SmsTwin.Http;

/// <summary>A request record read by <see cref="JsonBody"/>.</summary>
internal interface IJsonRequest
{
    /// <summary>The 400 message when the field with this JSON name has the wrong JSON type.</summary>
    static abstract string ProblemFor(string field);
}
