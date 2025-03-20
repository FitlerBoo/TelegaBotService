using System;
using System.Collections.Generic;

namespace TelegaBotService.Utils
{
    public static class DateUtils
    {
        // Метод для получения списка ближайших дат
        public static List<DateTime> GetWeekDays()
        {
            return Enumerable.Range(0, 7)
                .Select(i => DateTime.Today.AddDays(-i))
                .ToList();
        }
    }
}