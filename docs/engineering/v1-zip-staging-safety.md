# PCLS v1 package import: ZIP staging implementation checkpoint

**Status: draft development code, not a shipped or enabled Playnite install mode.** This is a coding-first candidate on [PR #21](https://github.com/KurtusCobain/PersonalCloudLibrarySource/pull/21). The public v0.3.2 extension remains unchanged.

## Implemented

- `PackageDiscoveryPolicy` classifies archives, direct-file types and executables conservatively. ZIP is ambiguous: some emulators load ZIP ROMs directly, so no format triggers unattended extraction or execution.
- `GameLauncherCandidatePolicy` provides read-only executable ranking for a future extracted-package preview. Common installers, dependency setups, crash reporters and uninstallers are flagged as helpers. Multiple similarly plausible launchers remain ambiguous. A suggestion **always requires user confirmation** and cannot execute or publish a play action by itself.
- `ArchiveInspectionPolicy` enforces Windows-aware relative paths, reserved names, duplicate file paths, file-parent collisions, entry/total size limits, excessive expansion limits and rejection of declared links.
- `ZipCentralDirectoryInspector` reads ZIP32 central metadata without relying on `ZipArchiveEntry.ExternalAttributes`, which is absent from the Playnite-supported .NET Framework 4.6.2 API. Encrypted, ZIP64, multi-disk, ambiguous legacy-codepage names, unsupported compression methods, Unix links/special types and inconsistent records are refused. ZIP64 and non-ASCII legacy codepages are **not** supported yet; this is intentional until qualified.
- `ZipPackageStagingExtractor.ExtractApprovedZip` opens a single existing ZIP read-only, verifies central metadata, checks against the pure preflight, and streams into a unique private staging directory inside a pre-existing cache parent. It refuses an existing destination and publishes only after length and central CRC32 checks. It supports cooperative cancellation and reports byte progress.
- Failure/cancellation cleanup removes only files created by this job and then attempts **nonrecursive removal of empty staging directories**. Unknown/symlinked/changed content is not recursively removed; incomplete cleanup is reported for manual inspection instead of risking unrelated files.
- `ZipPackageReviewService.Inspect` now reads an **actual local ZIP without extracting it**, cross-checks strict ZIP32 central metadata, validates the entire entry set through `ArchiveInspectionPolicy`, previews launcher candidates, and classifies single-ROM ZIPs as potential keep-as-archive items (not forced extraction). It binds the read-only review to the file's normalized path and SHA-256 digest. `ExtractAfterExplicitChoice` requires the caller to supply an affirmative extraction choice, match the previously reviewed path, and rehash the ZIP before calling the existing safe staging extractor. Missing consent, changed bytes, invalid review, 7z/RAR masquerades, and occupied destinations fail closed. This is a **service-level guard, not evidence of a shipping Playnite confirmation dialog**. It must not be wired without a real user decision; same-file mutation during the later extractor reopen remains a security-review risk.
- Deterministic Windows NUnit fixtures cover successful ZIP publish, cancellation, existing target, malicious traversal and reserved paths, ZIP symlink external attributes, case/file-parent collisions, preexisting staging, outside-cache refusal, and corrupted payload CRC.

## Not yet implemented / release blockers

1. ZIP inspection and the approval-bound extraction service are implemented, but **not wired to an active Playnite confirmation UI or install-controller workflow**. The UI must obtain an explicit extract-vs-keep selection rather than fabricating the Boolean consent argument, and must never silently extract ROM-ready ZIPs or run installers.
2. 7z and RAR need an independently reviewed extractor/parser integration with the **same** path, link, decompression, staging and cancellation invariants. Do not treat the ZIP implementation as support for those formats.
3. The ZIP32 parser is intentionally strict: ZIP64, split volumes, encrypted archives, and ambiguous legacy filename encodings fail closed. Unsupported formats must show actionable user guidance.
4. ZIP header CRC checks detect normal accidental corruption, but neither CRC32 nor user-supplied archive metadata is a trusted cryptographic proof of publisher authenticity. No signature or malware scanning is claimed.
5. Concurrent filesystem mutation/reparse-point races still require a Windows-specific adversarial review; the API does not claim handle-pinned filesystem isolation.
6. Source-specific package selection, executable ranking, directory ownership receipts, re-import, and safe uninstall must be integrated with multi-source settings and tested with disposable data.
7. Native Windows Playnite acceptance follows **only after code-complete, integrated CI, and Claude/Grok/Nova review** per [issue #17](https://github.com/KurtusCobain/PersonalCloudLibrarySource/issues/17).

## User safety boundaries

No automatic execution, no source deletion, no destination overwrite, no existing cache adoption, no new recursive deletion permission, no remote account/credential changes. Test fixture teardown only removes its own isolated temporary test directories.

Do **not** advertise all ZIP/7z/RAR installs as working or publish a 1.0 package based on this checkpoint alone.
