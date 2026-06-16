Icon set sync (personal icons)

When you need to bring developer's personal Icons8-derived set into this repository for local testing or build-time assets, follow one of these approaches. Only do this when you have explicit permission to redistribute the icons.

1) Git submodule (recommended when set is on GitHub)

- Add submodule pointing at icon repo (from repo root):
  - git submodule add <icon-repo-url> external/icons
- Update submodule after clone:
  - git submodule update --init --recursive
- Advantages: keeps external history separate, easy to update.
- Remember: do not commit the actual icon binaries into main repo if license forbids redistribution; prefer submodule reference.

2) Git subtree (if you want files copied into repo)

- Add subtree (copies files into repo history):
  - git subtree add --prefix=Assets/Icons <icon-repo-url> main --squash
- Update subtree later with `git subtree pull --prefix=Assets/Icons <icon-repo-url> main --squash`.
- Use only if license and project policy allow icons to be stored directly in this repository.

3) Local path for development only

- Keep icons outside repo at `C:\Apps\Icons\icons8-personal-set` (developer machine).
- Do not commit absolute paths. If you need to reference resources at build time, use relative paths inside XBVault/Assets and ensure CI uses the same layout (or submodule).

Checklist before committing icons
- Confirm license allows redistribution and bundling.
- Add entries to docs/ATTRIBUTIONS.md with source and license.
- If bundling, add icons to .gitignore only if they are intentionally excluded from VCS; prefer explicit add when allowed.
