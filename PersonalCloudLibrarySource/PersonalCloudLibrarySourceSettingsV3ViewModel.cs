using Playnite.SDK;
using Playnite.SDK.Data;
using System.Collections.Generic;

namespace PersonalCloudLibrarySource
{
    public sealed class PersonalCloudLibrarySourceSettingsV3ViewModel : PersonalCloudLibrarySourceSettingsViewModel, ISettings
    {
        private readonly PersonalCloudLibrarySource plugin;
        private PersonalCloudLibrarySourceSettingsV3 editingClone;

        public PersonalCloudLibrarySourceSettingsV3ViewModel(PersonalCloudLibrarySource plugin)
            : base(plugin)
        {
            this.plugin = plugin;

            var loadedSettings = plugin.LoadPluginSettings<PersonalCloudLibrarySourceSettingsV3>();
            var sourceSettings = loadedSettings != null
                ? (PersonalCloudLibrarySourceSettings)loadedSettings
                : base.Settings;
            var migration = SettingsMigrationService.Migrate(sourceSettings);
            base.Settings = migration.Settings;

            if (migration.WasMigrated)
            {
                plugin.SavePluginSettings(migration.Settings);
            }
        }

        public new PersonalCloudLibrarySourceSettingsV3 Settings
        {
            get
            {
                var versionedSettings = base.Settings as PersonalCloudLibrarySourceSettingsV3;
                if (versionedSettings != null)
                {
                    return versionedSettings;
                }

                var migration = SettingsMigrationService.Migrate(base.Settings);
                base.Settings = migration.Settings;
                plugin.SavePluginSettings(migration.Settings);
                return migration.Settings;
            }
            set => base.Settings = value ?? new PersonalCloudLibrarySourceSettingsV3
            {
                SettingsVersion = PersonalCloudLibrarySourceSettingsV3.CurrentSettingsVersion
            };
        }

        public new void BeginEdit()
        {
            editingClone = Serialization.GetClone(Settings);
        }

        public new void CancelEdit()
        {
            Settings = editingClone ?? SettingsMigrationService.Migrate(base.Settings).Settings;
            editingClone = null;
        }

        public new void EndEdit()
        {
            base.Settings = Settings;
            base.EndEdit();
            editingClone = null;
        }

        public new bool VerifySettings(out List<string> errors)
        {
            base.Settings = Settings;
            return base.VerifySettings(out errors);
        }
    }
}
