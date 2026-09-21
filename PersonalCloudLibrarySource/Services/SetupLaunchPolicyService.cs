namespace PersonalCloudLibrarySource
{
    public enum SetupLaunchAction
    {
        None = 0,
        OpenWizard = 1,
        ShowReminder = 2
    }

    public sealed class SetupLaunchContext
    {
        public bool PluginEnabled { get; set; }
        public bool SetupValid { get; set; }
        public bool SetupCompleted { get; set; }
        public bool SetupDismissed { get; set; }
        public bool ShowReminders { get; set; }
    }

    public sealed class SetupLaunchPolicyService
    {
        public SetupLaunchAction Evaluate(SetupLaunchContext context)
        {
            if (context == null || !context.PluginEnabled || context.SetupValid)
            {
                return SetupLaunchAction.None;
            }

            if (!context.SetupCompleted && !context.SetupDismissed)
            {
                return SetupLaunchAction.OpenWizard;
            }

            return context.ShowReminders
                ? SetupLaunchAction.ShowReminder
                : SetupLaunchAction.None;
        }
    }
}
