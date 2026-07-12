using System;

namespace PersonalCloudLibrarySource
{
    public sealed class SettingsMigrationResult
    {
        public SettingsMigrationResult(
            PersonalCloudLibrarySourceSettingsV3 settings,
            int previousVersion,
            bool wasMigrated)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            PreviousVersion = previousVersion;
            WasMigrated = wasMigrated;
        }

        public PersonalCloudLibrarySourceSettingsV3 Settings { get; }

        public int PreviousVersion { get; }

        public bool WasMigrated { get; }
    }

    public static class SettingsMigrationService
    {
        public static SettingsMigrationResult Migrate(PersonalCloudLibrarySourceSettingsV3 settings)
        {
            settings = settings ?? new PersonalCloudLibrarySourceSettingsV3();

            var previousVersion = settings.SettingsVersion;
            var wasMigrated = previousVersion < PersonalCloudLibrarySourceSettingsV3.CurrentSettingsVersion;

            if (settings.TransferConcurrency < 1 || settings.TransferConcurrency > 4)
            {
                settings.TransferConcurrency = 1;
            }

            if (wasMigrated)
            {
                settings.SettingsVersion = PersonalCloudLibrarySourceSettingsV3.CurrentSettingsVersion;
            }

            return new SettingsMigrationResult(settings, previousVersion, wasMigrated);
        }
    }
}
