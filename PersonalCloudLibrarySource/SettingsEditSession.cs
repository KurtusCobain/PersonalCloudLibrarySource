using System;

namespace PersonalCloudLibrarySource
{
    public sealed class SettingsEditSession
    {
        private readonly Func<bool> validate;
        private readonly Action<PersonalCloudLibrarySourceSettings> save;
        private PersonalCloudLibrarySourceSettings preEditSnapshot;
        private PersonalCloudLibrarySourceSettings committedSnapshot;
        private bool isActive;

        public SettingsEditSession(
            Func<bool> validate,
            Action<PersonalCloudLibrarySourceSettings> save)
        {
            this.validate = validate ?? throw new ArgumentNullException(nameof(validate));
            this.save = save ?? throw new ArgumentNullException(nameof(save));
        }

        public PersonalCloudLibrarySourceSettings BeginEdit(PersonalCloudLibrarySourceSettings current)
        {
            if (isActive)
            {
                return current;
            }

            preEditSnapshot = SettingsMigrationService.CloneForEditing(current);
            committedSnapshot = null;
            isActive = true;
            return current;
        }

        public PersonalCloudLibrarySourceSettings CancelEdit(PersonalCloudLibrarySourceSettings current)
        {
            if (!isActive)
            {
                return current;
            }

            SettingsMigrationService.RestoreSnapshot(preEditSnapshot, current);
            preEditSnapshot = null;
            isActive = false;
            return current;
        }

        public bool EndEdit(PersonalCloudLibrarySourceSettings current)
        {
            if (!isActive || !validate())
            {
                return false;
            }

            // Persistence and later runtime consumers receive a stable copy,
            // never the mutable object still referenced by the settings UI.
            committedSnapshot = SettingsMigrationService.CloneForEditing(current);
            save(SettingsMigrationService.CloneForEditing(committedSnapshot));
            preEditSnapshot = null;
            isActive = false;
            return true;
        }

        public PersonalCloudLibrarySourceSettings GetCommittedSnapshot()
        {
            return committedSnapshot == null
                ? null
                : SettingsMigrationService.CloneForEditing(committedSnapshot);
        }
    }
}
