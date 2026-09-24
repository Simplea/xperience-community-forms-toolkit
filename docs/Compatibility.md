# Xperience compatibility

Forms Toolkit separates its minimum supported Xperience version from the latest
version used by the integration site.

## Supported range

- Minimum supported Xperience version: `30.11.0`.
- Latest verified Xperience version: `31.9.0`. The packed package was built and
  runtime smoke-tested at both `30.11.0` and `31.9.0`; see the validation history.
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

The administration UI, at least one export in every supported format, and cloning
a representative form should be smoke-tested because successful compilation alone
cannot prove client-side or runtime compatibility. The clone check must verify its
field definition and settings, independent table, zero submissions, and unchanged
source integrations.

The form-cloning implementation uses only documented public Xperience APIs.
Cloning behaviors that would require `.Internal` namespaces, reflection over
non-public members, or copied internal administration components are excluded until
a public alternative exists. This keeps one toolkit package valid across the
supported range instead of adding release-specific implementations that can break
on a refresh.

## Release validation history

The full checklist was executed again on 2026-09-24 for toolkit `1.1.0`, using a
package packed locally from `main` at `9d993d7`, against `30.11.0` and `31.9.0`.
`31.9.0` was published to NuGet that day and Kentico's changelog did not yet
describe it, so this verification rests on testing alone.

- Each version ran in a separate host generated from Kentico's own
  `kentico-xperience-sample-mvc` template at that exact version, with a fresh
  database. Each host consumed the packed NuGet package (resolved as a package,
  not a project reference) and registered it exactly as the Usage Guide
  describes. The packed assembly contained the embedded administration client
  bundle, and the package declares only `Kentico.Xperience.Admin` `30.11.0` or
  newer, so the minimum-version host stayed on `30.11.0`.
- At both versions, with identical results: **Advanced export** in CSV, Excel,
  and XML with row-level content checks, including spreadsheet formula
  neutralization and CSV quoting; **Export selected** returning exactly the
  checked rows; **Delete selected**; **Advanced delete** with a date range,
  record limit, and preview; and **Clone form**, checked for an independent
  table, zero submissions, the same field definition, Form Builder layout, and
  contact mapping, and an unchanged source form (compared by hash). Neither host
  logged an error.
- This repository's Dancing Goat site was upgraded to `31.9.0` and the same
  features were re-checked against its data.
- Hosts generated from the sample template share the template's fixed
  `UserSecretsId`, which this repository's Dancing Goat also uses. In
  Development they therefore read Dancing Goat's connection string instead of
  their own `appsettings.json`. Remove the `UserSecretsId` from validation hosts
  before running them. Xperience's startup version check stopped the `30.11.0`
  host from using the upgraded `31.9.0` database.

The full checklist above (packed artifact, both version boundaries, admin UI,
every export format, and cloning) was executed end to end on 2026-09-15 for
toolkit version `1.0.0-beta.4`:

- The packed `XperienceCommunity.FormsToolkit` NuGet package (not a
  `ProjectReference`) was consumed by a separate, minimal host application at
  both `30.11.0` and `31.8.4`. Reflecting on the packed assembly confirmed the
  embedded administration client bundle is present
  (`AdminResources.xperience_community.forms.toolkit.entry.kxh...js`), which a
  `ProjectReference` build cannot verify.
- At `30.11.0`, a from-scratch minimal host needed `AddAuthentication()`,
  `UseStaticFiles()`/`UseCookiePolicy()`, and `app.Kentico().MapRoutes()` in
  addition to what the Usage Guide's quick-start snippet shows — none of
  these are toolkit-specific, but their absence produces confusing failures
  (`ICompositeViewEngine` resolution errors; a 404 loading the Form Builder
  iframe) that are easy to mistake for a toolkit or version incompatibility.
- **Advanced export**, **Advanced delete**, **Export selected**, and
  **Delete selected** all rendered and functioned correctly at both versions.
  CSV, Excel, and XML exports were verified with real row-level content
  (headers and data matched the exported rows exactly) at both versions.
  **Clone form** was verified at both versions: an independent form ID, zero
  submissions, and the source's field layout preserved.
- DancingGoat itself (the repository's own integration site) cannot compile
  at `30.11.0` — its bundled commerce sample code requires a newer Xperience
  version, unrelated to this toolkit. This is why minimum-version validation
  uses a separate, minimal host rather than DancingGoat with packages
  downgraded.

Separately, `1.0.0-beta.4` is running in a real, external production project
on `31.1.2` (report only; not independently re-verified as part of this
history).

## Updating versions

- Retest and update the latest verified version after every Xperience refresh.
- Do not raise the minimum merely because a newer Xperience release is available.
- Raise the minimum only when required by an API, security fix, runtime lifecycle,
  or an intentional change to the support window.
- After a stable Forms Toolkit release, raising the minimum Xperience version is a
  breaking compatibility change and requires a new major toolkit version.

## Known platform overlaps

Xperience by Kentico `31.8.0` added a native **Export** action to the
Submissions listing's static header toolbar (CSV only, exports the full
filtered/searched listing regardless of row selection). This coexists with,
and is independent from, Forms Toolkit's own **Advanced export** header
action and **Export selected** mass action — verified running against
`31.8.4` on 2026-09-15 and `31.9.0` on 2026-09-24.

The native action is rendered from a fixed, `Internal`-namespaced
`ExportAction` slot on the listing template, separate from the
`HeaderActions`/`MassActions` collections `PageExtender<FormSubmissionsTab>`
writes to. It cannot be hidden, moved, or relabeled from this toolkit,
consistent with the public-API-only policy above.

Net effect for administrators: once a row is selected, two visually distinct
**Export** controls are on screen at once (native: filled, static header;
toolkit: text-style, selection-only toolbar, with its own "Export selected
submissions" hover tooltip that the native action lacks). This was evaluated
and accepted as-is rather than renaming the toolkit's mass-action labels —
the failure mode is a redundant CSV download, not data loss, and Delete has
no equivalent native counterpart.
