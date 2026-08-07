# Xperience compatibility

Forms Toolkit separates its minimum supported Xperience version from the latest
version used by the integration site.

## Supported range

- Minimum supported Xperience version: `30.11.0`.
- Runtime smoke-tested Xperience version: `31.1.2`.
- Latest build-verified Xperience version: `31.7.2`.
- Toolkit version introducing this range: `1.0.0-beta.2`.

The NuGet package declares only the minimum version of
`Kentico.Xperience.Admin`. Consuming applications remain responsible for keeping
their Xperience packages aligned on a single version.

The absence of a NuGet upper bound does not mean that every future Xperience
release is automatically supported. The latest verified value records the newest
release covered by the project's compatibility checks.

## Development model

The library, unit tests, and embedded administration client compile against the
minimum supported version. This prevents contributors from accidentally using an
API that is unavailable to supported consumers.

The Dancing Goat integration site uses the latest verified version. It consumes
the library project built against the minimum, which checks that the resulting
library can be referenced by a current Xperience application.

Before release, validate the packed NuGet artifact in separate applications using:

1. the minimum supported Xperience version; and
2. the latest verified Xperience version.

The administration UI and at least one export in every supported format should be
smoke-tested because successful compilation alone cannot prove client-side or
runtime compatibility.

## Updating versions

- Retest and update the latest verified version after every Xperience refresh.
- Do not raise the minimum merely because a newer Xperience release is available.
- Raise the minimum only when required by an API, security fix, runtime lifecycle,
  or an intentional change to the support window.
- After a stable Forms Toolkit release, raising the minimum Xperience version is a
  breaking compatibility change and requires a new major toolkit version.
