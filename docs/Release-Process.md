# Release process

Forms Toolkit uses Semantic Versioning and publishes NuGet packages from Git tags.
The package is owned and published on NuGet.org by `andresvillenas`.

## One-time configuration

1. Create a protected GitHub environment named `nuget-release` and require manual
   approval for deployments.
2. In the `andresvillenas` NuGet.org account, create a Trusted Publishing policy
   with:

   - repository owner: `Simplea`;
   - repository: `xperience-community-forms-toolkit`;
   - workflow file: `release.yml`; and
   - environment: `nuget-release`.

3. Confirm that the package ID `XperienceCommunity.FormsToolkit` is available on
   NuGet.org before the first release.

No long-lived NuGet API key is stored in GitHub. The release workflow exchanges its
GitHub identity for a short-lived NuGet.org credential immediately before publish.

## Version policy

- Major: breaking public API, configuration, or behavior changes.
- Minor: backward-compatible features.
- Patch: backward-compatible fixes.
- Prerelease: `beta.N` while gathering feedback and `rc.N` when preparing a stable
  release.

The first planned release is `1.0.0-beta.1`.

## Publishing a release

1. Ensure the release commit is merged into `main` and CI is successful.
2. Update public documentation and prepare GitHub release notes.
3. Create and push an annotated tag:

   ```powershell
   git tag -a v1.0.0-beta.1 -m "Release 1.0.0-beta.1"
   git push origin v1.0.0-beta.1
   ```

4. Approve the `nuget-release` environment deployment.
5. Verify the package metadata and installation instructions on NuGet.org.
6. Create the matching GitHub release and attach the package artifacts produced by
   the workflow when desired.

Published NuGet package versions are immutable. Never move or reuse a release tag;
publish a new version for every correction.
