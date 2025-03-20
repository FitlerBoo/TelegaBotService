using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Data.Common;
using TelegaBotService;
using TelegaBotService.Database;
using TelegaBotService.Options;
using Telegram.Bot;

var builder = Host.CreateApplicationBuilder(args);
var configuration = builder.Configuration;
builder.Services.AddHostedService<TelegramBotBackgroundService>();

builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.Telegram));
builder.Services.AddTransient<ITelegramBotClient, TelegramBotClient>(serviceProvider =>
    {
        var token = serviceProvider.GetRequiredService<IOptions<TelegramOptions>>().Value.Token;

        return new TelegramBotClient(token);
    }
);

builder.Services.AddDbContext<TelegaBotDbContext>(options =>
{
    options.UseSqlite(configuration.GetConnectionString(nameof(TelegaBotDbContext)));
});

var host = builder.Build();
host.Run();
