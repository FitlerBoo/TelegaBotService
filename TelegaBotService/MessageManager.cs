using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;

namespace TelegaBotService
{
    public static class MessageManager
    {
        private static Dictionary<long, int> _userIdToLastUserMessageId = [];
        private static Dictionary<long, int> _userMessageIdToBotMessageId = [];

        private static async Task RemoveMessage(ITelegramBotClient BotClient, long ChatId, long UserId, int MessageId, CancellationToken CancellationToken)
        {
            await BotClient.DeleteMessage(ChatId, MessageId, CancellationToken);
            if(_userMessageIdToBotMessageId.TryGetValue(UserId, out int botMessageId))
                await BotClient.DeleteMessage(ChatId, botMessageId, CancellationToken);
        }

        public static async Task SetMessageIdAndRemovePrevious(
            ITelegramBotClient BotClient,
            long ChatId, 
            long UserId,
            int UserMessageId, 
            int BotMessageId, 
            CancellationToken CancellationToken)
        {
            if (_userIdToLastUserMessageId.TryGetValue(UserId, out int messageId))
                await RemoveMessage(BotClient, ChatId, UserId, messageId, CancellationToken);

            _userIdToLastUserMessageId[UserId] = UserMessageId;
            _userMessageIdToBotMessageId[UserMessageId] = BotMessageId;
        }
    }
}
