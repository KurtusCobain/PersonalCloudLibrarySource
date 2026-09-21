using NUnit.Framework;

namespace PersonalCloudLibrarySource.Tests.Setup
{
    [TestFixture]
    public class SetupLaunchPolicyServiceTests
    {
        private readonly SetupLaunchPolicyService service = new SetupLaunchPolicyService();

        [Test]
        public void Evaluate_DisabledPlugin_DoesNothing()
        {
            var result = service.Evaluate(new SetupLaunchContext
            {
                PluginEnabled = false,
                SetupValid = false,
                SetupCompleted = false,
                SetupDismissed = false,
                ShowReminders = true
            });

            Assert.That(result, Is.EqualTo(SetupLaunchAction.None));
        }

        [Test]
        public void Evaluate_ValidSetup_DoesNothing()
        {
            var result = service.Evaluate(new SetupLaunchContext
            {
                PluginEnabled = true,
                SetupValid = true,
                SetupCompleted = false,
                SetupDismissed = false,
                ShowReminders = true
            });

            Assert.That(result, Is.EqualTo(SetupLaunchAction.None));
        }

        [Test]
        public void Evaluate_NewInvalidSetup_OpensWizard()
        {
            var result = service.Evaluate(new SetupLaunchContext
            {
                PluginEnabled = true,
                SetupValid = false,
                SetupCompleted = false,
                SetupDismissed = false,
                ShowReminders = true
            });

            Assert.That(result, Is.EqualTo(SetupLaunchAction.OpenWizard));
        }

        [Test]
        public void Evaluate_DismissedInvalidSetup_ShowsReminder()
        {
            var result = service.Evaluate(new SetupLaunchContext
            {
                PluginEnabled = true,
                SetupValid = false,
                SetupCompleted = false,
                SetupDismissed = true,
                ShowReminders = true
            });

            Assert.That(result, Is.EqualTo(SetupLaunchAction.ShowReminder));
        }

        [Test]
        public void Evaluate_PreviouslyCompletedButNowInvalid_ShowsReminderInsteadOfInterrupting()
        {
            var result = service.Evaluate(new SetupLaunchContext
            {
                PluginEnabled = true,
                SetupValid = false,
                SetupCompleted = true,
                SetupDismissed = false,
                ShowReminders = true
            });

            Assert.That(result, Is.EqualTo(SetupLaunchAction.ShowReminder));
        }

        [Test]
        public void Evaluate_RemindersDisabled_DoesNothingAfterDismissal()
        {
            var result = service.Evaluate(new SetupLaunchContext
            {
                PluginEnabled = true,
                SetupValid = false,
                SetupCompleted = false,
                SetupDismissed = true,
                ShowReminders = false
            });

            Assert.That(result, Is.EqualTo(SetupLaunchAction.None));
        }
    }
}
