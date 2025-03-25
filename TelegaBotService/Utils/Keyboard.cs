using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegaBotService.Utils
{
    public enum KeyboardType
    {
        Date,
        TasksType,
        Locations,
        Performers
    }
    public static class Keyboard
    {
        public static InlineKeyboardMarkup GetKeyboard(KeyboardType Type) => Type switch
        {
            KeyboardType.Date => GetDateKeyboard(),
            KeyboardType.TasksType => GetTaskTypeKeyboard(),
            KeyboardType.Locations => GetLocationsKeyboard(),
            KeyboardType.Performers => GetPerformersKeyboard()
        };

        private static InlineKeyboardMarkup GetDateKeyboard()
        {
            var chunkedWeekDays = DateUtils.GetWeekDays().Chunk(3);
            var inlineKeyboard = new InlineKeyboardMarkup(
                    chunkedWeekDays.Select(group => group.Select(
                        date => InlineKeyboardButton.WithCallbackData(
                            text: date.ToShortDateString(),
                            callbackData: date.ToShortDateString()
                            )))
            );
            return inlineKeyboard;
        }

        private static InlineKeyboardMarkup GetTaskTypeKeyboard() => 
            new([
                [
                    InlineKeyboardButton.WithCallbackData("Заявка подразделения", "Заявка подразделения"),
                    InlineKeyboardButton.WithCallbackData("Аварийные работы", "Аварийные работы")
                ],
                [
                    InlineKeyboardButton.WithCallbackData("Плановые работы", "Плановые работы"),
                    InlineKeyboardButton.WithCallbackData("Распоряжение руководства", "Распоряжение руководства")
                ]
            ]);

        private static InlineKeyboardMarkup GetLocationsKeyboard() =>
            new([
                [
                    InlineKeyboardButton.WithCallbackData("МК", "МК"),
                    InlineKeyboardButton.WithCallbackData("М9", "М9"),
                    InlineKeyboardButton.WithCallbackData("М11", "М11")
                ],
                [
                    InlineKeyboardButton.WithCallbackData("К16", "К16"),
                    InlineKeyboardButton.WithCallbackData("БКМ", "БКМ"),
                    InlineKeyboardButton.WithCallbackData("Алабино", "Алабино")
                ],
                [
                    InlineKeyboardButton.WithCallbackData("Другой вариант", "location")
                ]
            ]);

        private static InlineKeyboardMarkup GetPerformersKeyboard() =>
            new([
                [
                    InlineKeyboardButton.WithCallbackData("Кирейцев А.В.", "Кирейцев А.В."),
                    InlineKeyboardButton.WithCallbackData("Воробьёв А.С.", "Воробьёв А.С.")
                ],
                [
                    InlineKeyboardButton.WithCallbackData("Матвеев М.А.", "Матвеев М.А."),
                    InlineKeyboardButton.WithCallbackData("Шестериков В.О.", "Шестериков В.О.")
                ],
                [
                    InlineKeyboardButton.WithCallbackData("Малахов А.В.", "Малахов А.В."),
                    InlineKeyboardButton.WithCallbackData("Маляров М.В.", "Маляров М.В.")
                ],
                [
                    InlineKeyboardButton.WithCallbackData("Закончить ввод исполнителей", "done")
                ]
            ]);

    }
}
