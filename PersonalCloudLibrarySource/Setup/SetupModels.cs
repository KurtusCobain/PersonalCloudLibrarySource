namespace PersonalCloudLibrarySource
{
    public enum SetupSourceKind
    {
        ExistingManifest = 0,
        LocalFolder = 1,
        NetworkFolder = 2,
        RcloneRemote = 3
    }

    public enum SetupWizardStep
    {
        ChooseSource = 0,
        ConfigureSource = 1,
        ScanPreview = 2,
        CacheBehavior = 3,
        Review = 4,
        Completed = 5
    }
}
