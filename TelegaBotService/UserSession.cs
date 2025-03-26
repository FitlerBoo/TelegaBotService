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
        AskedExecutorName,
        Done
    }

    public class UserSession
    {
        private TaskDataState _currentState = TaskDataState.None;

        public UserSession() { }


    }
}
