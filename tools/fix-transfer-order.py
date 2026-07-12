from pathlib import Path


def replace_once(text, old, new, label):
    if new in text:
        return text
    if old not in text:
        raise SystemExit(f"Patch target missing for {label}: {old}")
    return text.replace(old, new, 1)


manager_path = Path("PersonalCloudLibrarySource/Transfers/CloudTransferManager.cs")
manager = manager_path.read_text(encoding="utf-8-sig")
manager = replace_once(
    manager,
    """                return jobs
                    .Where(job => job.GameId == gameId && !job.IsTerminal)
                    .OrderByDescending(job => job.CreatedAt)
                    .FirstOrDefault();""",
    """                return jobs
                    .Select((job, index) => new { Job = job, Index = index })
                    .Where(value => value.Job.GameId == gameId && !value.Job.IsTerminal)
                    .OrderByDescending(value => value.Job.CreatedAt)
                    .ThenByDescending(value => value.Index)
                    .Select(value => value.Job)
                    .FirstOrDefault();""",
    "active transfer ordering",
)
manager = replace_once(
    manager,
    """                return jobs
                    .Where(job =>
                        job.GameId == gameId &&
                        (job.State == CloudTransferState.Failed || job.State == CloudTransferState.Cancelled))
                    .OrderByDescending(job => job.CompletedAt ?? job.CreatedAt)
                    .FirstOrDefault();""",
    """                return jobs
                    .Select((job, index) => new { Job = job, Index = index })
                    .Where(value =>
                        value.Job.GameId == gameId &&
                        (value.Job.State == CloudTransferState.Failed || value.Job.State == CloudTransferState.Cancelled))
                    .OrderByDescending(value => value.Job.CompletedAt ?? value.Job.CreatedAt)
                    .ThenByDescending(value => value.Job.CreatedAt)
                    .ThenByDescending(value => value.Index)
                    .Select(value => value.Job)
                    .FirstOrDefault();""",
    "retryable transfer ordering",
)
manager_path.write_text(manager, encoding="utf-8-sig")

gitignore_path = Path(".gitignore")
gitignore = gitignore_path.read_text(encoding="utf-8")
for line in (
    "PersonalCloudLibrarySource/packages/",
    "*.log",
    "TestResult.xml",
):
    if line not in gitignore.splitlines():
        gitignore = gitignore.rstrip() + "\n" + line + "\n"
gitignore_path.write_text(gitignore, encoding="utf-8")

print("Deterministic transfer ordering and artifact ignores applied.")
