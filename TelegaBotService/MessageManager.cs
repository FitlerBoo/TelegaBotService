using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;

namespace TelegaBotService
{
    public class MessageManager
    {
        private ConcurrentDictionary<long, ConcurrentQueue<int>> _userIdToLastMessagesId = [];
        private LinkedList<int> _ignoredMessages = [];
        private long _chatId;
        private ITelegramBotClient _botClient;

        public MessageManager(ITelegramBotClient BotClient, long ChatId)
        {
            _botClient = BotClient;
            _chatId = ChatId;
        }

        public async Task RemoveMessages(long UserId, CancellationToken CancellationToken)
        {
            if (_userIdToLastMessagesId.TryGetValue(UserId, out var messagesId))
            {
                if (!messagesId.IsEmpty)
                {
                    await _botClient.DeleteMessages(_chatId, messagesId, CancellationToken);
                    messagesId.Clear();
                }
            }
        }

        public async Task AddMessageId(long UserId, int MessageId)
        {
            if (!_userIdToLastMessagesId.ContainsKey(UserId))
                _userIdToLastMessagesId[UserId] = [];
            if (!_ignoredMessages.Contains(MessageId))
                _userIdToLastMessagesId[UserId].Enqueue(MessageId);
        }

        public void AddIgnoredMessageId(int MessageId) => _ignoredMessages.AddLast(MessageId);
    }
}
