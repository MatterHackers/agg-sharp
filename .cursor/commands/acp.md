Add, commit, and push all pending changes directly to `main`. CI runs tests automatically on push.

## 1. Clean up
- Delete any temporary scripts, scratch files, or generated artifacts that were created during this session.
- Add any files we want to keep but should never check in to `.gitignore`.

## 2. Review before staging
- Run `git status` to see all changes.
- Never stage sensitive files (`.env`, credentials, API keys, secrets).
- Stage files by name rather than using `git add -A` or `git add .`.

## 3. Commit
- All work is done on `main`. Push directly to `origin/main`. No feature branches.
- Write a concise commit message that explains the *why*, not just the *what*.
- If the pre-commit hook fails, fix the underlying code (not the tests) using /fix-test-failures, re-stage, and create a NEW commit — do not amend.

## 4. Push
- Push `main` to `origin`: `git push origin main`.
- After pushing, verify `git status` shows a clean working tree with no unstaged changes.

## 5. Monitor CI
- Use `gh.exe run list --branch main` to find the triggered workflow run.
- Poll with `gh.exe run watch <run-id>` to monitor progress.
- If tests fail, investigate with `gh.exe run view <run-id> --log-failed` and fix the issue.

## Shell: PowerShell on Windows
Do NOT use bash heredoc syntax (`<<'EOF'`). Use PowerShell string variables with backtick-n for newlines:

```powershell
$msg = "Subject line`n`nBody text"; git commit -m $msg
```
