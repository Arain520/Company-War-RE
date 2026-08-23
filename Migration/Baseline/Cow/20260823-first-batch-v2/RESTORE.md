# Cow snapshot restore procedure

This snapshot is an inert recovery artifact. Never restore it over `D:\UNITY2\Cow`.

1. Verify every entry in `SHA256SUMS`.
2. Clone Git history into a new temporary directory from `Cow-history.bundle`.
3. Extract `Cow-working-tree.zip` over that temporary clone.
4. Apply `tracked-deletions.txt` to reproduce tracked deletions captured in the source working tree.
5. Compare restored files with `Cow-working-tree-files.csv` by path, byte count, and SHA-256.
6. Confirm the restored Git status matches `git-status-porcelain-v2.txt`.

`restore-verification.txt` records the completed extraction and file-hash verification performed in the target project's temporary directory.
