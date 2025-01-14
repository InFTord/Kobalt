﻿using System.Reflection;
using System.Text;
using Kobalt.Bot.Handlers;
using Kobalt.Bot.Services;
using Kobalt.Bot.Services.Discord;
using Kobalt.Infrastructure.Extensions;
using Kobalt.Infrastructure.Services;
using Kobalt.Infrastructure.Services.Booru;
using Kobalt.Infrastructure.Types;
using Kobalt.Shared.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Remora.Discord.API.Abstractions.Gateway.Commands;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Gateway.Commands;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Responders;
using Remora.Discord.Extensions.Extensions;
using Remora.Discord.Gateway;
using Remora.Discord.Gateway.Extensions;
using Remora.Discord.Interactivity.Extensions;
using RemoraHTTPInteractions.Extensions;
using RemoraHTTPInteractions.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
       .AddEnvironmentVariables()
       .AddUserSecrets(Assembly.GetExecutingAssembly(), true);

builder.Services.AddSerilogLogging();
ConfigureKobaltBotServices(builder.Configuration, builder.Services);
// TODO: replace either with runtimeProperties.json or a config
builder.WebHost.ConfigureKestrel(c => c.ListenLocalhost(8001));


var host = builder.Build();

host.MapPost("/interaction", async (HttpContext ctx, WebhookInteractionHelper handler, IOptions<KobaltConfig> config) =>
    {
        var hasHeaders = DiscordHeaders.TryExtractHeaders(ctx.Request.Headers, out var timestamp, out var signature);
        var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
        
        if (!hasHeaders || !DiscordHeaders.VerifySignature(body, timestamp!, signature!, config.Value.Discord.PublicKey!))
        {
            ctx.Response.StatusCode = 401;
            return;
        }

        var result = await handler.HandleInteractionAsync(body);
        if (!result.IsDefined(out var content))
        {
            ctx.Response.StatusCode = 500;
            return;
        }

        if (!content.Item2.HasValue)
        {
            ctx.Response.Headers.ContentType = "application/json";
            await ctx.Response.WriteAsync(content.Item1);
            return;
        }
        else
        {
            var ret = new MultipartResult().AddPayload(new MemoryStream(Encoding.UTF8.GetBytes(result.Entity.Item1)));
            
            foreach ((var key, var value) in result.Entity.Item2.Value)
            {
                ret.Add(key, value);
            }
            
            await ret.ExecuteResultAsync(new ActionContext(ctx, ctx.GetRouteData(), new()));
        }
    }
);

await host.RunAsync();

void ConfigureKobaltBotServices(IConfiguration hostConfig, IServiceCollection services)
{
    var config = hostConfig.Get<KobaltConfig>()!;
    services.AddSingleton(Options.Create(config));
    
    var token = config.Discord.Token;

    services.AddDiscordGateway(_ => token);
    services.AddHTTPInteractionAPIs();
    services.AddDiscordCommands(true);
    services.Configure<InteractionResponderOptions>(s => s.UseEphemeralResponses = true);
    services.AddCommandGroupsFromAssembly(Assembly.GetExecutingAssembly());
    services.AddInteractivityFromAssembly(Assembly.GetExecutingAssembly());
    services.AddHostedService<KobaltDiscordGatewayService>();
    services.AddTransient<ImageOverlayService>();
    
    services.AddPostExecutionEvent<PostExecutionHandler>();

    services.AddHttpClient("booru");
    services.AddTransient<BooruSearchService>();
    
    services.Configure<DiscordGatewayClientOptions>
    (
        options =>
        {
            options.Intents |= GatewayIntents.MessageContents;
            options.Presence = new UpdatePresence
            (
                Status: UserStatus.DND,
                IsAFK: false,
                Since: null,
                Activities: new IActivity[]
                {
                    new Activity
                    (
                        Name: "Code being written",
                        Type: ActivityType.Watching
                    )
                }
            );
        }
    );

    services.AddInteractivity();
    services.AddInteractivityFromAssembly(Assembly.GetExecutingAssembly());
}