using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.ReplyMarkups;
using TelegaBotService.Utils;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using TelegaBotService.Database;
using Microsoft.EntityFrameworkCore;

namespace TelegaBotService
{
    public class TelegramBotBackgroundService : BackgroundService
    {
        private readonly ILogger<TelegramBotBackgroundService> _logger;
        private readonly ITelegramBotClient _botClient;
        private Dictionary<long, TaskTemplate> _taskHolder = [];
        private MessageManager _messageManager;
        private IServiceProvider _serviceProvider;
        public TelegramBotBackgroundService(
            ITelegramBotClient BotClient,
            ILogger<TelegramBotBackgroundService> Logger,
            IServiceProvider ServiceProvider)
        {
            _botClient = BotClient;
            _logger = Logger;
            _serviceProvider = ServiceProvider;
            SetBotCommands();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            ReceiverOptions receiverOptions = new()
            {
                AllowedUpdates = []
            };

            while (!stoppingToken.IsCancellationRequested)
            {
                await _botClient.ReceiveAsync(
                    updateHandler: HandleUpdateAsync,
                    errorHandler: HandleErrorAsync,
                    receiverOptions: receiverOptions,
                    cancellationToken: stoppingToken);
            }
        }

        private async Task HandleUpdateAsync(
            ITelegramBotClient botClient,
            Telegram.Bot.Types.Update update,
            CancellationToken cancellationToken)
        {
            var handler = update switch
            {
                { Message: {} message } => MessageTextHandler(message, cancellationToken),
                { CallbackQuery: {} query } => CallbackQueryHandler(query, cancellationToken),
                _ => UnknownUpdateHandlerAsync(update, cancellationToken)
            };

            await handler;
        }


        private async Task MessageTextHandler(Message Message, CancellationToken CancellationToken)
        {
            var userId = Message.From.Id;
            if (!_taskHolder.ContainsKey(userId))
                _taskHolder[userId] = new TaskTemplate();

            if (Message.Text is not { } messageText)
                return;

            if (_messageManager == null)
                _messageManager = new(_botClient, Message.Chat.Id);

            _messageManager.AddMessageId(Message.From.Id, Message.Id);

            switch (messageText)
            {
                case "/new":
                    _taskHolder[userId] = new TaskTemplate();
                    await _messageManager.RemoveMessages(Message.From.Id, CancellationToken);
                    await SendDateMenu(Message, CancellationToken);
                    break;
                case "/clear":
                    _taskHolder[userId] = new TaskTemplate();
                    break;
            }

            var task = _taskHolder[userId].State switch
            {
                TaskDataState.AskedTaskType => SaveDescriptionAndAskLocation(Message, CancellationToken),
                TaskDataState.AskedTaskDescription => SaveLocationAndAskPerformers(Message.From.Id, Message.From.Username, messageText, CancellationToken),
                _ => Task.CompletedTask
            };

            await task;
        }

        private async Task CallbackQueryHandler(CallbackQuery Query, CancellationToken CancellationToken)
        {
            if (Query == null) return;

            if (_messageManager == null)
                _messageManager = new(_botClient, Query.Message.Chat.Id);
            _messageManager.AddMessageId(Query.From.Id, Query.Message.Id);

            if (_taskHolder.TryGetValue(Query.From.Id, value: out TaskTemplate taskTemplate))
            {
                var task = taskTemplate.State switch
                {
                    TaskDataState.None => SaveDateAndAskTaskType(Query, CancellationToken),
                    TaskDataState.AskedDate => SaveTaskTypeAndAskDescription(Query, CancellationToken),
                    TaskDataState.AskedTaskDescription => TrySaveLocation(Query, CancellationToken),
                    TaskDataState.AskedLocation => AskPerformers(Query, CancellationToken),
                    TaskDataState.AskingExecutorName => AskPerformers(Query, CancellationToken),
                    _ => Task.CompletedTask
                };

                await task;
            }
        }

        #region User input processing

        private async Task SendDateMenu(Message Message, CancellationToken CancellationToken)
        {
            var botMessage =  await _botClient.SendMessage(
                chatId: Message.Chat.Id,
                text: $"@{Message.From.Username} Выберите дату:",
                replyMarkup: Keyboard.GetKeyboard(KeyboardType.Date),
                cancellationToken: CancellationToken
            );
        }

        private async Task SaveDateAndAskTaskType(CallbackQuery Query, CancellationToken CancellationToken)
        {
            var taskTemplate = _taskHolder[Query.From.Id];
            taskTemplate.Date = Query.Data;
            taskTemplate.State = TaskDataState.AskedDate;

            await _messageManager.RemoveMessages(Query.From.Id,CancellationToken);

            var botMessage = await _botClient.SendMessage(
                chatId: Query.Message.Chat.Id,
                text: $"@{Query.From.Username} Выберите тип работ:",
                replyMarkup: Keyboard.GetKeyboard(KeyboardType.TasksType),
                cancellationToken: CancellationToken);
        }

        private async Task SaveTaskTypeAndAskDescription(CallbackQuery Query, CancellationToken CancellationToken)
        {
            var taskTemplate = _taskHolder[Query.From.Id];
            taskTemplate.TaskType = Query.Data;
            taskTemplate.State = TaskDataState.AskedTaskType;

            await _messageManager.RemoveMessages(Query.From.Id, CancellationToken);

            var botMessage = await _botClient.SendMessage(
                chatId: Query.Message.Chat.Id,
                text: $"@{Query.From.Username} Опишите выполненные работы:",
                cancellationToken: CancellationToken);
            _messageManager.AddMessageId(Query.From.Id, botMessage.Id);
        }

        private async Task SaveDescriptionAndAskLocation(Message Message, CancellationToken CancellationToken)
        {
            var taskTemplate = _taskHolder[Message.From.Id];
            taskTemplate.TaskDescription = Message.Text;
            taskTemplate.State = TaskDataState.AskedTaskDescription;

            await _messageManager.RemoveMessages(Message.From.Id, CancellationToken);

            var message = await _botClient.SendMessage(
                chatId: Message.Chat.Id,
                text: $"@{Message.From.Username} Выберите место проведения работ:",
                replyMarkup: Keyboard.GetKeyboard(KeyboardType.Locations),
                cancellationToken: CancellationToken);
        }

        private async Task TrySaveLocation(CallbackQuery Query, CancellationToken CancellationToken)
        {
            await _messageManager.RemoveMessages(Query.From.Id, CancellationToken);

            if (Query.Data != "location")
            {
                await SaveLocationAndAskPerformers(Query.From.Id, Query.From.Username, Query.Data, CancellationToken);
                return;
            }
            else
            {
                var message = await _botClient.SendMessage(
                    chatId: Query.From.Id,
                    text: $"@{Query.From.Username} Введите место проведения работ:",
                    cancellationToken: CancellationToken);
            }
        }

        private async Task SaveLocationAndAskPerformers(long Id, string Username, string location, CancellationToken CancellationToken)
        {
            var taskTemplate = _taskHolder[Id];
            taskTemplate.Location = location;
            taskTemplate.State = TaskDataState.AskedLocation;

            await SendPerformersKeyboard(Id, Username, CancellationToken);
        }

        private async Task AskPerformers(CallbackQuery Query, CancellationToken CancellationToken)
        {
            var taskTemplate = _taskHolder[Query.From.Id];
            var message = Query.Data;
            taskTemplate.State = message == "done" ? TaskDataState.AskedExecutorName : TaskDataState.AskingExecutorName;

            if (message != "done")
                taskTemplate.Performers.AddLast(message);

            var task = taskTemplate.State switch
            {
                TaskDataState.AskingExecutorName => SendPerformersKeyboard(Query.From.Id, Query.From.Username, CancellationToken),
                TaskDataState.AskedExecutorName => SandCompletedTaskDescription(Query, CancellationToken),
                _ => Task.CompletedTask
            };

            await task;
        }

        private async Task SendPerformersKeyboard(long UserId, string Username, CancellationToken CancellationToken)
        {
            _taskHolder[UserId].State = TaskDataState.AskingExecutorName;

            await _messageManager.RemoveMessages(UserId, CancellationToken);

            await _botClient.SendMessage(
                chatId: UserId,
                text: $"{(_taskHolder[UserId].Performers.Count > 0 ? string.Format($"{_taskHolder[UserId].GetPerformers()}\n\n") : string.Empty)}"+
                $"@{Username} Выберите исполнителей работ:",
                replyMarkup: Keyboard.GetKeyboard(KeyboardType.Performers),
                cancellationToken: CancellationToken);
        }

        private async Task SandCompletedTaskDescription(CallbackQuery Query, CancellationToken CancellationToken)
        {
            var taskTemplate = _taskHolder[Query.From.Id];
            taskTemplate.State = TaskDataState.AskedExecutorName;

            await _messageManager.RemoveMessages(Query.From.Id, CancellationToken);

            var resultMessage = await _botClient.SendMessage(
                chatId: Query.Message.Chat.Id,
                text: $"{taskTemplate}",
                cancellationToken: CancellationToken
                );

            await SaveDataToDbAndSendTaskDescription(Query, resultMessage.Id, CancellationToken);
        }

        private async Task SaveDataToDbAndSendTaskDescription(CallbackQuery Query, int ignoredMessageId, CancellationToken CancellationToken)
        {
            _messageManager.AddIgnoredMessageId(ignoredMessageId);
            var taskTemplate = _taskHolder[Query.From.Id];

            int savedData;
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<TelegaBotDbContext>();

                context.Database.EnsureCreated();
                await context.Tasks.AddAsync(new()
                {
                    Id = Guid.NewGuid(),
                    Date = DateTime.Parse(taskTemplate.Date),
                    Type = taskTemplate.TaskType,
                    Description = taskTemplate.TaskDescription,
                    Location = taskTemplate.Location,
                    Performers = taskTemplate.GetPerformers(),
                }, CancellationToken);

                savedData = await context.SaveChangesAsync(CancellationToken);
            }

            taskTemplate.State = TaskDataState.Done;

            await SendSaveResultMessage(savedData, Query, CancellationToken);
        }

        private async Task SendSaveResultMessage(int SavedData, CallbackQuery Query, CancellationToken CancellationToken)
        {
            var handle = SavedData switch
            {
                0 => _botClient.SendMessage(
                    chatId: Query.Message.Chat.Id,
                    text: $"@{Query.From.Username} Данные не удалось сохранить, повторите попытку",
                    cancellationToken: CancellationToken),

                _ => _botClient.SendMessage(
                    chatId: Query.Message.Chat.Id,
                    text: $"@{Query.From.Username} Данные успешно сохранены",
                    cancellationToken: CancellationToken)
            };

            var botMessage = await handle;
            await _messageManager.AddMessageId(Query.From.Id, botMessage.Id);

            await DeleteResultMessage(Query, CancellationToken);
        }

        private async Task DeleteResultMessage(CallbackQuery Query, CancellationToken CancellationToken)
        {
            await Task.Delay(5000);
            await _messageManager.RemoveMessages(Query.From.Id, CancellationToken);
        }

        #endregion


        #region Think about it
        private async Task UnknownUpdateHandlerAsync(Telegram.Bot.Types.Update update, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Unknown type update");
        }

        // Обработка ошибок
        private Task HandleErrorAsync(ITelegramBotClient botClient,
            Exception exception,
            CancellationToken cancellationToken)
        {
            switch (exception)
            {
                case ApiRequestException apiRequestException:
                    _logger.LogError(apiRequestException,
                        "Телеграм API Error:\n[{errorCode}]\n[{message}]",
                        apiRequestException.ErrorCode,
                        apiRequestException.Message);
                    return Task.CompletedTask;

                default:
                    _logger.LogError(exception, "Error while processing in telegram bot");
                    return Task.CompletedTask;
            }
        }

        // Доступные команды
        private async Task SetBotCommands()
        {
            var commands = new List<BotCommand>
        {
            new() { Command = "new", Description = "Создать выполненную работу" },
            new() { Command = "clear", Description = "Очистить форму"}
        };

            await _botClient.SetMyCommands(commands);
            Console.WriteLine("Команды бота настроены!");
        }
    }
    #endregion
}
