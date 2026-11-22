using System.Text.Json.Serialization;
using SmsTwin.Http;

namespace SmsTwin.Faults;

/// <summary>What a fault rule does; in JSON <c>error</c>, <c>delay</c> or <c>accept-then-error</c>.</summary>
[JsonConverter(typeof(KebabCaseEnumConverter))]
internal enum FaultAction
{
    Error,
    Delay,
    AcceptThenError,
}
