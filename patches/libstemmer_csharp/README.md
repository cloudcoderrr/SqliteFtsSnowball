# Snowball (libstemmer) C# patches

This folder holds **unified-diff** patches that are applied to the official
[`libstemmer_csharp`](https://snowballstem.org/download.html) source distribution
after it is downloaded and before it is compiled into the `SqliteFtsSnowball` NuGet.

## How patches are applied

[`scripts/fetch-snowball.ps1`](../../scripts/fetch-snowball.ps1):

1. downloads `libstemmer_csharp-<version>.tar.gz` and extracts it,
2. runs `git apply` for every `*.patch` file in this folder, in filename order
   (so prefix them `0001-`, `0002-`, …), with paths **relative to the extracted
   tarball root** (e.g. `csharp/Snowball/Stemmer.cs`),
3. copies the resulting `csharp/Snowball/**/*.cs` into
   `src/SqliteFtsSnowball/Generated/`.

## Creating a patch

```bash
# 1. Extract a clean copy of the upstream tarball somewhere.
tar -xzf libstemmer_csharp-3.2.0.tar.gz
cp -r libstemmer_csharp-3.2.0 libstemmer_csharp-3.2.0.orig

# 2. Edit files under libstemmer_csharp-3.2.0/csharp/...

# 3. Produce a -p1, git-style unified diff rooted at the tarball directory:
diff -ruN libstemmer_csharp-3.2.0.orig libstemmer_csharp-3.2.0 \
  | sed -e 's#libstemmer_csharp-3.2.0.orig/#a/#' -e 's#libstemmer_csharp-3.2.0/#b/#' \
  > patches/libstemmer_csharp/0001-my-change.patch
```

Verify it applies cleanly:

```bash
pwsh ./scripts/fetch-snowball.ps1 -Force
```

## Updating the Snowball version

Change `<SnowballVersion>` in [`Directory.Build.props`](../../Directory.Build.props)
(or pass `-Version` to the script / set `SNOWBALL_VERSION`). If the upstream layout
changed, refresh the patches here.

> No patches are required for a vanilla build — this folder may legitimately be empty
> apart from this README.
