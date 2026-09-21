using Playnite.SDK.Models;
using System;
using System.Collections.Generic;

namespace PersonalCloudLibrarySource
{
    public enum ImportFailureKind
    {
        None = 0,
        SourceUnavailable = 1,
        InvalidManifest = 2,
        UnsupportedSchema = 3
    }

    public sealed class ImportOutcome
    {
        private ImportOutcome(
            bool succeeded,
            ImportFailureKind failureKind,
            string source,
            string error,
            IReadOnlyList<PersonalCloudLibraryItem> validItems,
            IReadOnlyList<GameMetadata> games,
            IReadOnlyList<string> diagnostics)
        {
            Succeeded = succeeded;
            FailureKind = failureKind;
            Source = source ?? string.Empty;
            Error = error ?? string.Empty;
            ValidItems = validItems ?? new PersonalCloudLibraryItem[0];
            Games = games ?? new GameMetadata[0];
            Diagnostics = diagnostics ?? new string[0];
        }

        public bool Succeeded { get; }
        public ImportFailureKind FailureKind { get; }
        public string Source { get; }
        public string Error { get; }
        public IReadOnlyList<PersonalCloudLibraryItem> ValidItems { get; }
        public IReadOnlyList<GameMetadata> Games { get; }
        public IReadOnlyList<string> Diagnostics { get; }

        public static ImportOutcome Success(
            string source,
            IReadOnlyList<PersonalCloudLibraryItem> validItems,
            IReadOnlyList<GameMetadata> games,
            IReadOnlyList<string> diagnostics = null)
        {
            return new ImportOutcome(true, ImportFailureKind.None, source, string.Empty, validItems, games, diagnostics);
        }

        public static ImportOutcome Failure(
            ImportFailureKind kind,
            string source,
            string error,
            IReadOnlyList<string> diagnostics = null)
        {
            return new ImportOutcome(false, kind, source, error, null, null, diagnostics);
        }
    }

    public sealed class ImportOutcomeException : InvalidOperationException
    {
        public ImportOutcomeException(ImportOutcome outcome)
            : base(outcome?.Error ?? "Manifest import failed.")
        {
            Outcome = outcome;
        }

        public ImportOutcome Outcome { get; }
    }

    public static class ImportExecutionPolicy
    {
        public static IReadOnlyList<GameMetadata> Complete(ImportOutcome outcome)
        {
            if (outcome == null)
            {
                throw new ArgumentNullException(nameof(outcome));
            }

            if (!outcome.Succeeded)
            {
                throw new ImportOutcomeException(outcome);
            }

            return outcome.Games;
        }
    }

    public sealed class ImportOutcomeService
    {
        private readonly ManifestLoader loader;
        private readonly ManifestParserValidator parser;
        private readonly ManifestItemMapper mapper;

        public ImportOutcomeService()
            : this(new ManifestLoader(), new ManifestParserValidator(), new ManifestItemMapper())
        {
        }

        public ImportOutcomeService(ManifestLoader loader, ManifestParserValidator parser, ManifestItemMapper mapper)
        {
            this.loader = loader ?? throw new ArgumentNullException(nameof(loader));
            this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
            this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public ImportOutcome Import(
            PersonalCloudLibrarySourceSettings settings,
            Func<PersonalCloudLibrarySourceSettings, string> readRclone)
        {
            var diagnostics = new List<string>();
            var load = loader.Load(settings, readRclone);
            var source = ResolveSource(settings, load.Source);
            diagnostics.Add("manifestSource=" + source);
            if (!load.Succeeded)
            {
                diagnostics.Add("sourceError=" + load.Error);
                return ImportOutcome.Failure(
                    ImportFailureKind.SourceUnavailable,
                    source,
                    load.Error,
                    diagnostics);
            }

            var parse = parser.Parse(load.Json);
            if (!parse.Succeeded)
            {
                var kind = parse.Manifest != null &&
                    (parse.Manifest.Version < ManifestParserValidator.MinimumSupportedVersion ||
                     parse.Manifest.Version > ManifestParserValidator.MaximumSupportedVersion)
                    ? ImportFailureKind.UnsupportedSchema
                    : ImportFailureKind.InvalidManifest;
                diagnostics.Add("manifestError=" + parse.Error);
                return ImportOutcome.Failure(kind, source, parse.Error, diagnostics);
            }

            diagnostics.Add("manifestItemCount=" + parse.ValidItems.Count);
            var games = mapper.Map(parse.ValidItems, settings, diagnostics);
            diagnostics.Add("returnedGameCount=" + games.Count);
            return ImportOutcome.Success(source, parse.ValidItems, games, diagnostics);
        }

        private static string ResolveSource(PersonalCloudLibrarySourceSettings settings, string loaderSource)
        {
            if (string.Equals(
                PersonalCloudLibrarySource.GetProviderType(settings),
                PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType,
                StringComparison.OrdinalIgnoreCase))
            {
                return (settings?.RcloneRemoteName ?? string.Empty).Trim() + ":" +
                    (settings?.RcloneManifestPath ?? string.Empty).Trim();
            }

            return loaderSource ?? string.Empty;
        }
    }
}
