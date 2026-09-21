# PCLS Branding Image Cleanup Design

## Goal

Replace the dashboard branch's inaccurate generated branding with clean raster assets derived from the two user-provided reference images. Keep the pass limited to branding assets, their Playnite/XAML/resource wiring, packaging, documentation, and focused validation.

## Source of truth

- Icon reference: `C:\Users\katie\Downloads\PCLSLOGO2.png`, 1024 x 1024, SHA-256 `9E988A8AA5871FBA9D97B424CDD508765CBCF9889D982FD766627D9FE3E77ED0`.
- Full-brand reference: `C:\Users\katie\Downloads\PCLSLOGO.png`, 1254 x 1254, SHA-256 `EE7AF26039EBD2C0DC6CE51E47056C3427FD9D7A06AE5115CC096758B99D739F`.

The references control the cloud/gamepad mark, colors, `PCLS` wordmark, and the subtitle `Personal Cloud Library Source`. Asset processing must preserve those visual elements instead of redrawing or restyling them.

## Asset outputs

- `PersonalCloudLibrarySource/icon.png`: Reference A foreground on transparency, square 512 x 512 PNG, used by Playnite's extension manifest and release listing.
- `PersonalCloudLibrarySource/Assets/pcls-logo-wide.png`: transparent 1400 x 420 header PNG derived from Reference B. Its existing mark, wordmark, and subtitle may be rearranged into a horizontal composition, but the artwork and lettering must not be regenerated.
- `docs/assets/pcls-logo-full.png`: high-quality full-brand PNG derived from Reference B for documentation and repository presentation.

No smaller variants will be added unless an existing consumer proves one is required.

## Cleanup and wiring

- Remove the inaccurate hand-built SVG branding files when they are not faithful to the references.
- Remove the base64 fragments, standalone base64 payload, decoder script, and one-time asset note that exist only to materialize generated branding.
- Check in the final PNGs directly; packaging must copy them without a decode/materialization step.
- Keep the existing XAML pack URI for the wide PNG when possible, adjusting only image sizing needed for the corrected composition.
- Update the project file, package script, CI/package inspection, branding documentation, and focused UI contract tests to reflect direct PNG assets.
- Do not change plugin behavior, identifiers, manifest schema, transfer logic, dashboard logic, or unrelated documentation.

## Validation

- Decode every final PNG and verify dimensions.
- Verify the square icon and wide header contain meaningful alpha transparency, including transparent corners.
- Build the full solution in Debug and Release configurations.
- Run the full NUnitLite test executable.
- Build the `.pext`, inspect its archive contents, and decode the packaged `icon.png` and `Assets/pcls-logo-wide.png`.
- Review final assets visually against both references before committing.

## Git handling

Work only on `feature/user-friendly-dashboard-cleanup`. The root checkout's unrelated uncommitted changes on `main` remain untouched. Commit and push the focused branding changes to the existing feature branch.
