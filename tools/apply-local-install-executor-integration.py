from pathlib import Path

path = Path("PersonalCloudLibrarySource/PersonalCloudLibrarySource.cs")
text = path.read_text(encoding="utf-8-sig")
old = """                        rcloneFileCopier,
                        localFileCopier));"""
new = """                        rcloneFileCopier,
                        localFileCopier,
                        GetTransferManager(),
                        GetTransferExecutor()));"""

if new not in text:
    if old not in text:
        raise SystemExit("Local install controller integration target was not found.")
    text = text.replace(old, new, 1)

path.write_text(text, encoding="utf-8-sig")
