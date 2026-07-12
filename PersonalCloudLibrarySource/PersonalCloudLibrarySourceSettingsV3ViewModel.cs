namespace PersonalCloudLibrarySource
{
    public sealed class PersonalCloudLibrarySourceSettingsV3ViewModel : PersonalCloudLibrarySourceSettingsViewModel
    {
        public PersonalCloudLibrarySourceSettingsV3ViewModel(PersonalCloudLibrarySource plugin)
            : base(plugin)
        {
            var loadedSettings = plugin.LoadPluginSettings<PersonalCloudLibrarySourceSettingsV3>();
            var migration = SettingsMigrationService.Migrate(loadedSettings);
            base.Settings = migration.Settings;

            if (migration.WasMigrated)
            {
                plugin.SavePluginSettings(migration.Settings);
            }
        }

        public new PersonalCloudLibrarySourceSettingsV3 Settings
        {
            get => (PersonalCloudLibrarySourceSettingsV3)base.Settings;
            set => base.Settings = value;
        }
    }
}
