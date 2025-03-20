using Microsoft.AspNetCore.Mvc.ApiExplorer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelegaBotService
{
    public enum TaskDataState
    {
        None,
        AskedDate,
        AskedTaskType,
        AskedTaskDescription,
        AskedLocation,
        AskingExecutorName,
        AskedExecutorName
    }
    public class TaskTemplate
    {
        public TaskDataState State { get; set; } = TaskDataState.None;
        public string Date { get; set; } = string.Empty;
        public string TaskType { get; set; } = string.Empty;
        public string TaskDescription { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public LinkedList<string> Performers { get; set; } = [];

        public override string ToString()
        {
            return string.Format(
                $"{Date}" +
                $"\n{TaskType}" +
                $"\n{TaskDescription}" +
                $"\n{Location}" +
                $"\n{string.Join('\n', Performers)}");
        }

        //public string GetPerformers()
        //{
        //    return string.Format($"Вы выбрали:\n{string.Join( "\n", Performers )}");
        //}
    }
}
