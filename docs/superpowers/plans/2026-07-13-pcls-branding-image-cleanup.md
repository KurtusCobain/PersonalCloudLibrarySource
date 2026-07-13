# PCLS Branding Image Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace inaccurate generated PCLS branding with transparent PNG assets derived faithfully from the two user-provided references.

**Architecture:** Treat the supplied raster references as immutable visual masters. Check final PNGs directly into the plugin and docs, remove the base64/SVG materialization chain, and retain the existing XAML pack URI so runtime behavior changes only at the asset boundary.

**Tech Stack:** PNG raster assets, WPF XAML pack resources, MSBuild/.NET Framework 4.6.2, NUnitLite, PowerShell packaging, System.Drawing validation.

## Global Constraints

- Work only on `feature/user-friendly-dashboard-cleanup`.
- Use `C:\Users\katie\Downloads\PCLSLOGO2.png` as the icon source and `C:\Users\katie\Downloads\PCLSLOGO.png` as the full-brand source.
- Preserve the supplied artwork and exact subtitle `Personal Cloud Library Source`; do not redraw or restyle it.
- Keep changes limited to branding assets, resource/package wiring, focused tests, CI validation, and branding documentation.
- Do not change plugin behavior, IDs, manifest schema, transfers, dashboard logic, or unrelated documentation.

---

### Task 1: Define the direct-PNG asset contract

**Files:**
- Modify: `PersonalCloudLibrarySource.Tests/Ui/UiContractTests.cs`

**Interfaces:**
- Consumes: repository-relative asset locations and the existing wide-logo XAML pack URI.
- Produces: focused tests for PNG dimensions, alpha transparency, XAML usage, and removal of legacy materialization files.

- [ ] **Step 1: Replace encoded-payload assertions with direct PNG assertions**

Add tests that load `PersonalCloudLibrarySource/icon.png`, `PersonalCloudLibrarySource/Assets/pcls-logo-wide.png`, and `docs/assets/pcls-logo-full.png` through `System.Drawing.Bitmap`; require 512 x 512 for the icon and 1400 x 420 for the wide logo; require alpha-capable pixel formats and transparent corner pixels for runtime assets. Assert the dashboard and setup wizard still contain `/PersonalCloudLibrarySource;component/Assets/pcls-logo-wide.png`. Assert the legacy SVGs, `tools/decode-brand-assets.ps1`, `tools/pcls-logo-wide.b64`, `tools/apply-0.3.2-assets-note.txt`, and `tools/assets/pcls-*.part*` no longer exist.

- [ ] **Step 2: Run the focused tests and verify failure**

Run:

```powershell
& .\PersonalCloudLibrarySource.Tests\bin\Debug\PersonalCloudLibrarySource.Tests.exe --noheader --test=PersonalCloudLibrarySource.Tests.Ui.UiContractTests
```

Expected: failure because the old assets and materialization files are still present and current PNGs do not satisfy the new source-faithful contract.

### Task 2: Produce the reference-faithful PNG assets

**Files:**
- Replace: `PersonalCloudLibrarySource/icon.png`
- Replace: `PersonalCloudLibrarySource/Assets/pcls-logo-wide.png`
- Replace: `docs/assets/pcls-logo-full.png`

**Interfaces:**
- Consumes: the exact reference files and hashes pinned in the design spec.
- Produces: direct PNG assets consumed by WPF, Playnite, docs, and packaging.

- [ ] **Step 1: Extract Reference A without redesigning it**

Use the built-in image editing path with `PCLSLOGO2.png` as the edit target. Request only background replacement with a flat removable chroma color; preserve the cloud, controller, pixel trail, glow, geometry, and colors. Remove the chroma locally, crop to the foreground with balanced padding, and resize with high-quality resampling to a transparent 512 x 512 PNG.

- [ ] **Step 2: Extract Reference B without regenerating text**

Use the built-in image editing path with `PCLSLOGO.png` as the edit target. Request only background replacement with a flat removable chroma color; preserve the mark, `PCLS` lettering, divider accents, and subtitle verbatim. Remove the chroma locally and save a high-quality full-brand transparent PNG for docs.

- [ ] **Step 3: Compose the wide header from extracted Reference B pixels**

Arrange the extracted Reference B mark on the left and its existing `PCLS` wordmark, accents, and subtitle on the right on a transparent 1400 x 420 canvas. Do not typeset replacement text or redraw the mark. Preserve aspect ratios and add enough transparent padding to avoid clipping in WPF.

- [ ] **Step 4: Inspect all three outputs visually**

Confirm the icon matches Reference A, the full and wide logos match Reference B, text is exact, no background remains, edges are clean, and no element is malformed or blurred.

### Task 3: Remove the generated asset chain and wire direct PNGs

**Files:**
- Modify: `PersonalCloudLibrarySource/PersonalCloudLibrarySource.csproj`
- Modify: `tools/package-extension.ps1`
- Modify: `.github/workflows/build.yml`
- Modify: `docs/branding.md`
- Delete: `PersonalCloudLibrarySource/Assets/pcls-icon.svg`
- Delete: `PersonalCloudLibrarySource/Assets/pcls-logo-wide.svg`
- Delete: `docs/assets/pcls-logo-full.svg`
- Delete: `tools/decode-brand-assets.ps1`
- Delete: `tools/pcls-logo-wide.b64`
- Delete: `tools/apply-0.3.2-assets-note.txt`
- Delete: `tools/assets/pcls-icon.part01`
- Delete: `tools/assets/pcls-icon.part02`
- Delete: all `tools/assets/pcls-logo-full.part*`
- Delete: all `tools/assets/pcls-logo-wide.part*`

**Interfaces:**
- Consumes: the direct PNG files produced by Task 2.
- Produces: build output and `.pext` contents containing `icon.png` and `Assets/pcls-logo-wide.png` without decoding.

- [ ] **Step 1: Simplify project resources**

Keep `icon.png` as a copied `None` item and `Assets\pcls-logo-wide.png` as a WPF `Resource` with `CopyToOutputDirectory` set to `PreserveNewest`, so the same file is both available through the pack URI and present in the package staging directory. Remove SVG entries. Do not change the pack URI or XAML unless visual inspection proves sizing needs adjustment.

- [ ] **Step 2: Remove package-time materialization**

Delete the decoder invocation from `tools/package-extension.ps1`. Rely on MSBuild's copied icon and embedded/direct wide PNG while retaining the existing required-file and asset-directory checks.

- [ ] **Step 3: Align CI and documentation**

Remove CI checks that reconstruct encoded payloads and retain direct package decode/dimension checks. Update `docs/branding.md` to describe the supplied PNG masters and their runtime/docs roles without claiming SVG or base64 generation.

- [ ] **Step 4: Delete every obsolete generated asset file**

Remove only the SVG/base64/part/decoder/note files listed above. Leave screenshots and unrelated images untouched.

- [ ] **Step 5: Rebuild and run the focused tests**

Run the Debug solution build, then the focused UI contract fixture. Expected: build succeeds and all UI contract tests pass.

### Task 4: Verify build, tests, package, and final branch

**Files:**
- Verify: `dist/PersonalCloudLibrarySource-0.3.2.pext`

**Interfaces:**
- Consumes: the complete branding cleanup.
- Produces: evidence that source, build output, tests, and release package all carry valid corrected assets.

- [ ] **Step 1: Run full Debug build and tests**

Build `PersonalCloudLibrarySource/PersonalCloudLibrarySource.sln` with Configuration `Debug` and Platform `Any CPU`, then run `PersonalCloudLibrarySource.Tests/bin/Debug/PersonalCloudLibrarySource.Tests.exe`. Expected: zero build errors and all tests pass.

- [ ] **Step 2: Build the Release package**

Run:

```powershell
& .\tools\package-extension.ps1
```

Expected: `dist/PersonalCloudLibrarySource-0.3.2.pext` is created.

- [ ] **Step 3: Inspect the package**

Expand a copied `.zip` form of the package into a temporary inspection directory. Require `PersonalCloudLibrarySource.dll`, `extension.yaml`, `icon.png`, `Localization/en_US.xaml`, and `Assets/pcls-logo-wide.png`. Decode the two PNGs, verify 512 x 512 and 1400 x 420 respectively, and confirm transparent corners.

- [ ] **Step 4: Check scope and commit**

Run `git diff --check`, inspect `git status --short`, and confirm only branding/test/build/documentation files changed. Commit with:

```text
fix(branding): use supplied PCLS artwork
```

- [ ] **Step 5: Push the existing feature branch**

Push `feature/user-friendly-dashboard-cleanup` to `origin`, then record the final commit SHA.
