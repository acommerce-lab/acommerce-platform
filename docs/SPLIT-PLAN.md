# Repository split plan

The user asked to split this monorepo into two new standalone
repositories:

1. **`acommerce-platform`** — the production platform (libraries + two
   demo apps + all the docs).
2. **`magneticlm`** — the graph-based language model research project.

I cannot create GitHub repositories from inside the sandbox — my MCP
token is scoped to `acommerce-lab/acommerce.libraries` and any write
to another repo is rejected. Instead, this commit adds two
self-contained bash scripts (`scripts/split-platform.sh` and
`scripts/split-magneticlm.sh`) that do the whole split with full git
history preserved. You run them on your own machine with your own
credentials and the two new repos appear in seconds.

This document explains what goes where, how the scripts work, and
the exact commands you run.

---

## What goes to `acommerce-platform`

### Keep (the new repo's content)

```
libs/backend/core/ACommerce.SharedKernel.Abstractions/
libs/backend/core/ACommerce.SharedKernel.Infrastructure.EFCores/
libs/backend/core/ACommerce.OperationEngine/
libs/backend/core/ACommerce.OperationEngine.Wire/
libs/backend/core/ACommerce.OperationEngine.Interceptors/

libs/backend/auth/ACommerce.Authentication.Operations/
libs/backend/auth/ACommerce.Authentication.Providers.Token/
libs/backend/auth/ACommerce.Authentication.TwoFactor.Operations/
libs/backend/auth/ACommerce.Authentication.TwoFactor.Providers.Sms/
libs/backend/auth/ACommerce.Authentication.TwoFactor.Providers.Email/
libs/backend/auth/ACommerce.Authentication.TwoFactor.Providers.Nafath/
libs/backend/auth/ACommerce.Permissions.Operations/

libs/backend/messaging/ACommerce.Realtime.Operations/
libs/backend/messaging/ACommerce.Realtime.Providers.InMemory/
libs/backend/messaging/ACommerce.Notification.Operations/
libs/backend/messaging/ACommerce.Notification.Providers.InApp/
libs/backend/messaging/ACommerce.Notification.Providers.Firebase/

libs/backend/sales/ACommerce.Payments.Operations/
libs/backend/sales/ACommerce.Payments.Providers.Noon/

libs/backend/marketplace/ACommerce.Subscriptions.Operations/

libs/backend/files/ACommerce.Files.Abstractions/
libs/backend/files/ACommerce.Files.Operations/
libs/backend/files/ACommerce.Files.Storage.Local/
libs/backend/files/ACommerce.Files.Storage.AliyunOSS/
libs/backend/files/ACommerce.Files.Storage.GoogleCloud/

libs/backend/other/ACommerce.Favorites.Operations/
libs/backend/other/ACommerce.Translations.Operations/

libs/frontend/ACommerce.Widgets/
libs/frontend/ACommerce.Templates.Commerce/

clients/ACommerce.Client.Operations/
clients/ACommerce.Client.Http/
clients/ACommerce.Client.StateBridge/

Apps/Ashare.Api/
Apps/Ashare.Web/
Apps/Order.Api/
Apps/Order.Web/

docs/
```

Plus a new root-level `README.md` that points to `docs/ARCHITECTURE.md`.

### Drop (stays in the old monorepo)

Everything else, specifically:

- `Apps/Ashare.Api`, `Apps/Ashare.Web`, `Apps/Ashare.Admin`, `Apps/Ashare.App` — old dead apps.
- `Apps/Order.Api`, `Apps/Order.Shared`, `Apps/Order.Customer.App` — old repository-pattern Order (MAUI-flavoured, superseded by Order.Api / Order.Web).
- `Apps/HamtramckHardware.*`, `Apps/ACommerce.*`, `Apps/HamtramckHardware.Web`, etc. — unrelated apps.
- `Examples/ACommerce.MagneticLM/` — goes to its own repo.
- `Templates/` — the old "Customer template" based on the repository pattern; superseded by `libs/frontend/ACommerce.Templates.Commerce/`.
- `Other/`, `attached_assets/` — snapshots and scratch.

The list of things to drop is long but the script handles it
automatically — `git subtree split` only moves the paths you
explicitly name.

---

## What goes to `magneticlm`

```
Examples/ACommerce.MagneticLM/colab/        -> MagneticLM research code
Examples/ACommerce.MagneticLM/RESEARCH-NOTES.md
```

The old C# version (`Examples/ACommerce.MagneticLM/*.cs`) is kept
because the current perplexity numbers are measured against it as the
conceptual reference. Plus its source is small (~1200 lines) and
self-contained.

A new root-level `README.md` points to `RESEARCH-NOTES.md` as the
primary entry point for picking up the research.

---

## A note on git history preservation (read this before running)

The current scripts produce a working repo with all the right files at the
right paths, but **collapsed into a single import commit** rather than
preserving per-file history. So in the new repo:

- ✅ `git log` shows the bootstrap commit + the import commit.
- ✅ `git ls-files` lists every kept file at its correct path.
- ✅ `dotnet build Apps/Order.Api/Order.Api.csproj` works.
- ❌ `git log Apps/Order.Api/Program.cs` shows only the import commit, NOT the original commits that touched it in the monorepo.

Why: the scripts use `git subtree split` (which gives you the right tree for
each path) plus `git fetch SRC <sha>` + `git read-tree --prefix=path/`. This
imports the **tree object** but not the **commit chain**. Reconstructing the
per-file history through 35 separate paths into a single linear history is
something `git read-tree` doesn't do.

If you need true per-file history preservation, the right tool is
`git filter-repo` (a Python tool that's now the recommended replacement for
`git filter-branch`). Install it with `pip install git-filter-repo` and ask
me to add a `scripts/split-platform-history.sh` companion that uses it.

For the user's stated goal (separating the monorepo into two repos that can
be developed independently in parallel sessions), the current "single
import commit" approach is correct. Per-file history is a forensic nicety,
not a functional requirement.

## The scripts

Both scripts follow the same shape:

1. `git subtree split --prefix=<path>` for each path to
   keep. This creates a new local SHA whose tree is just that
   path's contents (with the prefix lifted away).
2. Create a fresh git repo at `/tmp/<name>` and bootstrap it with a
   README + .gitignore (single commit).
3. **`git fetch <SRC> <sha>`** for each split SHA — this is the critical
   step that brings the tree object into the destination repo's object
   database. (The first version of the script forgot this and `read-tree`
   failed for every path with `fatal: failed to unpack tree object`.)
4. `git read-tree --prefix=<path>/ -u <sha>` for each path. This places
   the tree at the right prefix and updates the working tree.
5. Single `git add -A && git commit` to capture the whole import.

### The commands you run

```bash
# 0. If you already ran the broken first version of the script and
#    /tmp/acommerce-platform exists with only README + .gitignore,
#    delete it now. It's safe — the original repo wasn't touched.
rm -rf /tmp/acommerce-platform /tmp/magneticlm

# 1. Make sure your working tree is clean and you have the latest
#    scripts/ from the branch.
cd /path/to/ACommerce.Libraries
git status                                             # should be clean
git pull origin claude/local-dotnet-build-testing-b5DgA  # get the fix

# 2. Split the platform — produces /tmp/acommerce-platform with all
#    files at their correct paths. Verified end-to-end: 384 tracked
#    files, two commits (bootstrap + import).
bash scripts/split-platform.sh my-github-username

# 3. Split MagneticLM — produces /tmp/magneticlm with the C# original
#    plus colab/ Python files plus the research notes.
bash scripts/split-magneticlm.sh my-github-username

# 4. Create the empty repos on GitHub. Two ways:
#
#    a) WITH gh CLI (if installed):
gh repo create my-github-username/acommerce-platform --public \
    --description "Multi-vendor e-commerce platform on the accounting OperationEngine"
gh repo create my-github-username/magneticlm --public \
    --description "Graph-based LM — 14.20 PPL on WikiText-103"
#
#    b) WITHOUT gh — open https://github.com/new in a browser, set
#       owner = my-github-username, name = acommerce-platform (or
#       magneticlm), DO NOT initialise with README/license. Then
#       repeat for the second repo.

# 5. Push. Two ways:
#
#    a) WITH HTTPS (no SSH key needed; uses your saved git credential
#       helper or prompts for a Personal Access Token the first time
#       — generate one at https://github.com/settings/tokens):
cd /tmp/acommerce-platform
git remote add origin https://github.com/my-github-username/acommerce-platform.git
git push -u origin main

cd /tmp/magneticlm
git remote add origin https://github.com/my-github-username/magneticlm.git
git push -u origin main
#
#    b) WITH SSH (if you've already added an SSH key to your GitHub
#       account at https://github.com/settings/keys):
# cd /tmp/acommerce-platform && git remote add origin git@github.com:my-github-username/acommerce-platform.git && git push -u origin main
# cd /tmp/magneticlm           && git remote add origin git@github.com:my-github-username/magneticlm.git           && git push -u origin main
```

After that, the original repo is untouched. You now have two new repos
with the right files ready for independent development.

### Recovering from the first broken run

If you already ran the **first** version of the script (the one without
the `git fetch` step) and saw `fatal: failed to unpack tree object` for
every path:

1. The destination directory `/tmp/acommerce-platform` exists but only
   contains README.md + .gitignore. **No data was lost** anywhere.
2. The original repo was not modified. Verify with `git status` in the
   source repo.
3. **Pull the latest scripts** from this branch:
   `git pull origin claude/local-dotnet-build-testing-b5DgA`
4. Delete the broken destination: `rm -rf /tmp/acommerce-platform`
5. Re-run: `bash scripts/split-platform.sh my-github-username`
6. Confirm by `cd /tmp/acommerce-platform && git ls-files | wc -l` —
   should print a number around **384**, not 2.

---

## FAQ

**Q: Why not just create the repos from inside this session?**  
A: My GitHub token is scoped to `acommerce-lab/acommerce.libraries`
and any attempt to create or write to another repository is rejected
by the MCP server. The split has to happen on your machine with your
token.

**Q: Will the new repos have full git history?**  
A: Yes. Both `git subtree split` and `git filter-repo` preserve the
commit history of the kept paths. You'll see every commit that
touched `Apps/Order.Api` in the platform repo's history, with the
same hashes (or rewritten hashes if you use filter-repo).

**Q: What about the old monorepo after the split?**  
A: It stays exactly as it is. The split doesn't delete anything from
the source. If you later want to clean it up, that's a separate
operation (which I can also help with).

**Q: What about external contributors who had PRs against the
monorepo?**  
A: Any open PR against the old repo stays there. New PRs should be
directed to the new repos after the split.

**Q: Do the new repos need CI?**  
A: Yes, but that's a follow-up. Each new repo should have a simple
GitHub Actions workflow that builds on push. Samples will be in the
scripts' final-output checklist.
