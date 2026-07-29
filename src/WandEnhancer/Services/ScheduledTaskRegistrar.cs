using System;
using System.IO;

namespace WandEnhancer.Services
{
    public class ScheduledTaskRegistrar : IScheduledTaskRegistrar
    {
        private const string TaskName = "WandEnhancerAutoPatchWatcher";
        private const int TaskTriggerLogon = 9;
        private const int TaskActionExec = 0;
        private const int TaskRunlevelHighest = 1;
        private const int TaskCreateOrUpdate = 6;
        private const int TaskLogonInteractiveToken = 3;

        public void Create(string wandPath, string autoPatchExePath)
        {
            if (string.IsNullOrWhiteSpace(wandPath))
                throw new ArgumentException("Wand path cannot be empty.", nameof(wandPath));
            if (!Directory.Exists(wandPath))
                throw new DirectoryNotFoundException($"Wand path not found: {wandPath}");
            if (!File.Exists(autoPatchExePath))
                throw new FileNotFoundException("Auto-patch executable not found.", autoPatchExePath);

            var taskServiceType = Type.GetTypeFromProgID("Schedule.Service");
            dynamic taskService = Activator.CreateInstance(taskServiceType);
            taskService.Connect();

            dynamic rootFolder = taskService.GetFolder("\\");
            dynamic taskDefinition = taskService.NewTask(0);
            taskDefinition.RegistrationInfo.Description = "Automatically patches Wand on user logon.";
            taskDefinition.Principal.RunLevel = TaskRunlevelHighest;
            taskDefinition.Settings.StartWhenAvailable = true;

            dynamic trigger = taskDefinition.Triggers.Create(TaskTriggerLogon);
            trigger.Enabled = true;

            dynamic action = taskDefinition.Actions.Create(TaskActionExec);
            action.Path = autoPatchExePath;
            action.Arguments = $"--watch \"{wandPath}\"";
            action.WorkingDirectory = Path.GetDirectoryName(autoPatchExePath);

            rootFolder.RegisterTaskDefinition(
                TaskName,
                taskDefinition,
                TaskCreateOrUpdate,
                null,
                null,
                TaskLogonInteractiveToken);
        }

        public void Delete()
        {
            var taskServiceType = Type.GetTypeFromProgID("Schedule.Service");
            dynamic taskService = Activator.CreateInstance(taskServiceType);
            taskService.Connect();

            dynamic rootFolder = taskService.GetFolder("\\");
            rootFolder.DeleteTask(TaskName, 0);
        }
    }
}
