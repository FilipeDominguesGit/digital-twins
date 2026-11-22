// SMS twin: a behavioural clone of an SMS provider, for tests and previews.
// Send API like a provider's, an inspection API, a fault API, and incoming messages delivered
// to a signed webhook. State lives in memory.
// Contract: ../README.md. Product-agnostic: it knows nothing about any product.
using SmsTwin.Faults;
using SmsTwin.Health;
using SmsTwin.Http;
using SmsTwin.Inbound;
using SmsTwin.Messages;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Services.AddSingleton(Webhook.FromEnvironment());
builder.Services.AddSingleton<MessageStore>();
builder.Services.AddSingleton<FaultRules>();
builder.Services.AddSingleton<InboundStore>();

var app = builder.Build();
app.MapHealth();
app.MapMessages(ApiKeyFilter.FromEnvironment());
app.MapFaults();
app.MapInbound();
app.Run();
