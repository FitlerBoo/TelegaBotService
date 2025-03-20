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

        public TelegramBotBackgroundService(
            ITelegramBotClient BotClient,
            ILogger<TelegramBotBackgroundService> Logger)
        {
            _botClient = BotClient;
            _logger = Logger;
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
            
            switch (messageText)
            {
                case "/new":
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
                TaskDataState.AskedExecutorName => SaveDataToDbAndSendTaskDescription(),
                _ => Task.CompletedTask
            };

            await task;

        }

        private async Task CallbackQueryHandler(CallbackQuery Query, CancellationToken CancellationToken)
        {
            if(_taskHolder.TryGetValue(Query.From.Id, value: out TaskTemplate taskTemplate))
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
            //await MessageManager.SetMessageIdAndRemovePrevious(
            //    _botClient,
            //    Message.Chat.Id, Message.From.Id,
            //    Message.Id, botMessage.Id,
            //    CancellationToken);
        }

        private async Task SaveDateAndAskTaskType(CallbackQuery Query, CancellationToken CancellationToken)
        {
            var taskTemplate = _taskHolder[Query.From.Id];
            taskTemplate.Date = Query.Data;
            taskTemplate.State = TaskDataState.AskedDate;

            var botMessage = await _botClient.SendMessage(
                chatId: Query.Message.Chat.Id,
                text: $"@{Query.From.Username} Выберите тип работ:",
                replyMarkup: Keyboard.GetKeyboard(KeyboardType.TasksType),
                cancellationToken: CancellationToken);

            //await MessageManager.SetMessageIdAndRemovePrevious(
            //    _botClient,
            //    Query.Message.Chat.Id, Query.Message.From.Id,
            //    Query.Message.Id, botMessage.Id,
            //    CancellationToken);
        }

        private async Task SaveTaskTypeAndAskDescription(CallbackQuery Query, CancellationToken CancellationToken)
        {
            var taskTemplate = _taskHolder[Query.From.Id];
            taskTemplate.TaskType = Query.Data;
            taskTemplate.State = TaskDataState.AskedTaskType;

            var botMessage = await _botClient.SendMessage(
                chatId: Query.Message.Chat.Id,
                text: $"@{Query.From.Username} Опишите выполненные работы:",
                cancellationToken: CancellationToken);

            //await MessageManager.SetMessageIdAndRemovePrevious(
            //    _botClient,
            //    Query.Message.Chat.Id, Query.Message.From.Id,
            //    Query.Message.Id, botMessage.Id,
            //    CancellationToken);
        }

        private async Task SaveDescriptionAndAskLocation(Message Message, CancellationToken CancellationToken)
        {
            var taskTemplate = _taskHolder[Message.From.Id];
            taskTemplate.TaskDescription = Message.Text;
            taskTemplate.State = TaskDataState.AskedTaskDescription;

            await _botClient.SendMessage(
                chatId: Message.Chat.Id,
                text: $"@{Message.From.Username} Выберите место проведения работ:",
                replyMarkup: Keyboard.GetKeyboard(KeyboardType.Locations),
                cancellationToken: CancellationToken);

        }

        private async Task TrySaveLocation(CallbackQuery Query, CancellationToken CancellationToken)
        {
            if (Query.Data != "location")
            {
                await SaveLocationAndAskPerformers(Query.From.Id, Query.From.Username, Query.Data, CancellationToken);
                return;
            }

            await _botClient.SendMessage(
                chatId: Query.From.Id,
                text: $"@{Query.From.Username} Введите место проведения работ:",
                cancellationToken: CancellationToken);
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
            {
                taskTemplate.Performers.AddLast(message);
                await _botClient.SendMessage(
                    chatId: Query.Message.Chat.Id,
                    text: taskTemplate.ToString(),
                    cancellationToken: CancellationToken);
            }

            var task = taskTemplate.State switch
            {
                TaskDataState.AskingExecutorName => SendPerformersKeyboard(Query.From.Id, Query.From.Username, CancellationToken),
                TaskDataState.AskedExecutorName => SandCompletedTaskDescription(Query, CancellationToken),
                _ => Task.CompletedTask
            };

            await task;
        }

        private async Task SendPerformersKeyboard(long Id, string Username, CancellationToken CancellationToken)
        {
            _taskHolder[Id].State = TaskDataState.AskingExecutorName;

            await _botClient.SendMessage(
                chatId: Id,
                text: $"@{Username} Выберите исполнителей работ:",
                replyMarkup: Keyboard.GetKeyboard(KeyboardType.Performers),
                cancellationToken: CancellationToken);
        }

        private async Task SandCompletedTaskDescription(CallbackQuery Query, CancellationToken CancellationToken)
        {
            var taskTemplate = _taskHolder[Query.From.Id];
            taskTemplate.State = TaskDataState.AskedExecutorName;

            await _botClient.SendMessage(
                chatId: Query.From.Id,
                text: $"@{Query.From.Username}\n{taskTemplate}",
                cancellationToken: CancellationToken
                );
        }

        private async Task SaveDataToDbAndSendTaskDescription()
        {
            //_dbContext.Database.EnsureCreated();
            //await _dbContext.Tasks.AddAsync(new());
            //await _dbContext.SaveChangesAsync();
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
