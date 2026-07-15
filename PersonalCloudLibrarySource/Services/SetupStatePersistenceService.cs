using System;

namespace PersonalCloudLibrarySource
{
    public sealed class SetupStatePersistenceService
    {
        public void MarkDismissed(
            PersonalCloudLibrarySourceSettingsV3 settings,
            Action<PersonalCloudLibrarySourceSettingsV3> save)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            settings.SetupDismissed = true;
            var snapshot = SettingsMigrationService.CloneForEditing(settings) as PersonalCloudLibrarySourceSettingsV3;
            save(snapshot);
        }

        public void MarkCompleted(PersonalCloudLibrarySourceSettingsV3 settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.SetupCompleted = true;
            settings.SetupDismissed = false;
        }
    }
}
