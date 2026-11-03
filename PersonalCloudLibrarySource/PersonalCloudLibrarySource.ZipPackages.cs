using Playnite.SDK.Models;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PersonalCloudLibrarySource
{
    public partial class PersonalCloudLibrarySource
    {
        private readonly ConcurrentDictionary<Guid, CancellationTokenSource>
            activeZipExtractions = new ConcurrentDictionary<Guid, CancellationTokenSource>();

        // This is deliberately a separate, user-initiated extraction operation.
        // It never calls Playnite.InstallGame, sets IsInstalled or launches code.
        private async void ExtractZipGameTargetAsync(GameCommandTarget selected)
        {
            var title = PclsResources.Get("LOCPLSZipExtractTitle",
                "Extract ZIP to Separate Cache Folder");
            CancellationTokenSource cancellation = null;
            var registered = false;
            try
            {
                if (selected?.Game == null || selected.Game.PluginId != Id)
                    return;
                var gameId = selected.Game.Id;
                if (activeZipExtractions.ContainsKey(gameId))
                    return;

                var currentSettings = settings.GetRuntimeSettingsSnapshot();
                if (currentSettings == null || !currentSettings.Enabled ||
                    !currentSettings.AllowDownloads)
                    return;

                var manifest = LoadValidatedManifest(currentSettings);
                var current = gameCommandService.ResolveTargets(
                    new[] { selected.Game }, manifest.Items, currentSettings, Id).Single();
                var candidate = new ZipGameReviewTargetPolicy()
                    .Resolve(current, currentSettings, Id);
                if (!candidate.Available)
                {
                    playniteApi.Dialogs.ShowMessage(
                        PclsResources.Get("LOCPLSZipReviewUnavailable",
                            "A matching local ZIP is no longer available. No files were changed."),
                        title);
                    return;
                }

                // A large archive must never be hashed on Playnite's UI thread.
                var review = await Task.Run(() => new ZipPackageReviewService()
                    .Inspect(candidate.ArchivePath, current.Item.Title));
                if (!review.PassedPreflight)
                {
                    playniteApi.Dialogs.ShowMessage(
                        PclsResources.Format("LOCPLSZipReviewRejected",
                            "ZIP review was refused: {0}{1}No files were changed.",
                            review.Reason, Environment.NewLine),
                        title);
                    return;
                }

                // Re-read settings and manifest following the async review.
                // User-facing approval is only for the same current ZIP source.
                currentSettings = settings.GetRuntimeSettingsSnapshot();
                if (currentSettings == null || !currentSettings.Enabled ||
                    !currentSettings.AllowDownloads)
                    return;
                manifest = LoadValidatedManifest(currentSettings);
                current = gameCommandService.ResolveTargets(
                    new[] { selected.Game }, manifest.Items, currentSettings, Id).Single();
                var refreshedSource = new ZipGameReviewTargetPolicy()
                    .Resolve(current, currentSettings, Id);
                if (!refreshedSource.Available ||
                    !string.Equals(candidate.ArchivePath, refreshedSource.ArchivePath,
                        StringComparison.OrdinalIgnoreCase))
                    return;

                var receipts = new CacheOwnershipReceiptStore(Path.Combine(
                    GetPluginDataDirectory(), "ownership-receipts"));
                var extractor = new ZipManagedExtractionService();
                var plan = extractor.Prepare(review, candidate.ArchivePath,
                    currentSettings.LocalCacheFolder, current.Item.Id, receipts);
                if (plan == null)
                {
                    playniteApi.Dialogs.ShowMessage(
                        PclsResources.Get("LOCPLSZipExtractUnavailable",
                            "Extraction is unavailable: the destination is unsafe, already occupied or already recorded. No files were changed."),
                        title);
                    return;
                }

                // Yes = extract; No = keep ZIP. No dialog or policy heuristic
                // is allowed to turn an absent answer into extraction consent.
                var decision = playniteApi.Dialogs.ShowMessage(
                    PclsResources.Format("LOCPLSZipExtractConfirmation",
                        "Keep the current ZIP (No), or extract its verified contents to this new folder (Yes)?{0}{0}{1}{0}{0}No existing files will be overwritten. No executable or installer will run. Playnite installed state and game launch settings will NOT change. The result is a separate managed-cache extraction.",
                        Environment.NewLine, plan.Destination),
                    title, MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (decision != MessageBoxResult.Yes)
                    return;

                cancellation = new CancellationTokenSource();
                if (!activeZipExtractions.TryAdd(gameId, cancellation))
                    return;
                registered = true;

                // The extraction, archive hash, inventory scan and receipt
                // issuance may be expensive; keep them off the UI thread.
                var receiptStore = receipts;
                var approvedPlan = plan;
                var token = cancellation.Token;
                var result = await Task.Run(() => extractor.ExecuteApproved(
                    approvedPlan, true, receiptStore, token));

                if (result.Cancelled)
                {
                    playniteApi.Dialogs.ShowMessage(
                        PclsResources.Get("LOCPLSZipExtractCancelled",
                            "ZIP extraction was cancelled. Existing source and game files were preserved."),
                        title);
                }
                else if (!result.Succeeded)
                {
                    playniteApi.Dialogs.ShowMessage(
                        PclsResources.Get("LOCPLSZipExtractFailed",
                            "ZIP extraction was refused or failed. Inspect the archive and cache settings. Existing files were not overwritten."),
                        title);
                }
                else if (!result.ReceiptRecorded)
                {
                    playniteApi.Dialogs.ShowMessage(
                        PclsResources.Format("LOCPLSZipExtractUntracked",
                            "ZIP files were extracted to {0}, but an inventory receipt could not be saved. Preserve these files and inspect them manually. The game has NOT been marked installed.",
                            result.Destination),
                        title);
                }
                else
                {
                    playniteApi.Dialogs.ShowMessage(
                        PclsResources.Format("LOCPLSZipExtractComplete",
                            "ZIP contents extracted and recorded at:{0}{1}{0}{0}The ZIP source was preserved. Playnite installed state, launch settings and uninstall permissions were not changed. Configure a launcher explicitly before playing.",
                            Environment.NewLine, result.Destination),
                        title);
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "PCLS ZIP extraction flow failed.");
                playniteApi.Dialogs.ShowMessage(
                    PclsResources.Get("LOCPLSZipExtractFailed",
                        "ZIP extraction was refused or failed. Inspect the archive and cache settings. Existing files were not overwritten."),
                    title);
            }
            finally
            {
                if (registered && selected?.Game != null)
                {
                    CancellationTokenSource removed;
                    activeZipExtractions.TryRemove(selected.Game.Id, out removed);
                }
                cancellation?.Dispose();
            }
        }

        // Preview only; there is deliberately NO Play action, launcher execution
        // or installed-state update attached to the candidate ranking.
        private async void ReviewExtractedZipLauncherAsync(GameCommandTarget selected)
        {
            var title = PclsResources.Get("LOCPLSZipLauncherReviewTitle",
                "Review Extracted ZIP Launchers");
            try
            {
                if (selected?.Game == null || selected.Game.PluginId != Id)
                    return;
                var snapshot = settings.GetRuntimeSettingsSnapshot();
                if (snapshot == null || !snapshot.Enabled)
                    return;
                var manifest = LoadValidatedManifest(snapshot);
                var current = gameCommandService.ResolveTargets(
                    new[] { selected.Game }, manifest.Items, snapshot, Id).Single();
                if (current.Item == null)
                    return;
                var receipts = new CacheOwnershipReceiptStore(Path.Combine(
                    GetPluginDataDirectory(), "ownership-receipts"));
                var policy = new ZipExtractedLauncherReviewService();
                if (!policy.HasRecordedExtraction(snapshot.LocalCacheFolder,
                        current.Item.Id, receipts))
                {
                    playniteApi.Dialogs.ShowMessage(
                        PclsResources.Get("LOCPLSZipLauncherReviewUnavailable",
                            "No managed extracted ZIP and matching inventory receipt were found."),
                        title);
                    return;
                }

                var result = await Task.Run(() => policy.Inspect(
                    snapshot.LocalCacheFolder, current.Item.Id,
                    current.Item.Title, receipts));
                if (!result.InventoryVerified)
                {
                    playniteApi.Dialogs.ShowMessage(
                        PclsResources.Get("LOCPLSZipLauncherReviewRefused",
                            "Extracted files or their ownership inventory have changed. No launcher is approved. Review the files manually."),
                        title);
                    return;
                }

                var top = result.Launchers.Candidates.Take(6)
                    .Select(candidate => "  " + candidate.RelativePath + " — " +
                        candidate.Kind).ToList();
                var preview = top.Count == 0
                    ? PclsResources.Get("LOCPLSZipLauncherReviewEmpty",
                        "No supported Windows launcher candidates were found.")
                    : string.Join(Environment.NewLine, top);
                playniteApi.Dialogs.ShowMessage(
                    PclsResources.Format("LOCPLSZipLauncherReviewCompleted",
                        "Verified extracted ZIP inventory:{0}{1}{0}{0}Candidate launchers (first six):{0}{2}{0}{0}These are only suggestions. No launcher or installer has been approved or executed. Playnite install state and game actions are unchanged.",
                        Environment.NewLine, result.ExtractedDirectory, preview),
                    title);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "PCLS could not verify extracted ZIP launcher candidates.");
                playniteApi.Dialogs.ShowMessage(
                    PclsResources.Get("LOCPLSZipLauncherReviewFailed",
                        "Launcher review could not be completed. No files were changed."),
                    title);
            }
        }

        private void CancelZipExtraction(Guid gameId)
        {
            CancellationTokenSource cancellation;
            if (!activeZipExtractions.TryGetValue(gameId, out cancellation))
                return;
            try { cancellation.Cancel(); }
            catch (ObjectDisposedException) { }
        }

        private void CancelAllZipExtractions()
        {
            foreach (var entry in activeZipExtractions)
                CancelZipExtraction(entry.Key);
        }
    }
}
