# Feature specification: Advanced submission removal

Status: Implemented; prerelease validation in progress. This revision renames
the **Delete** mass action to **Delete selected** to match **Export
selected**'s grammar, restyles the **Advanced delete** header action to pair
visually with **Advanced export** (see `form-submission-export.md`), and adds
a zero-submissions visibility gate to both header actions.
Repository: `Simplea/xperience-community-forms-toolkit`
Supported baseline: Xperience by Kentico `30.11.0` or newer; build-verified
through `31.7.2`; .NET 8
Research date: 2026-08-12

## Context

Xperience Community Forms Toolkit is an independently owned integration maintained
by Simplea for Xperience by Kentico. It is a reusable library, not an Xperience
website and not a Kentico-owned product. The Dancing Goat project under `examples/`
is the integration host used to run and verify the library. The toolkit already
provides **Clone form**, submission **Export**, and a proposed **Retention**
feature (scheduled, age-based auto-purge).

The running Submissions listing at `Forms -> <selected form> -> Submissions`
already includes a single-row delete action (a trash icon in the Actions column
of each row), confirmed directly in the administration UI rather than in public
documentation, which does not describe it. There is no way to remove more than
one submission per click, and no way to remove submissions by criteria (a date
range, or "everything currently on this page") without deleting rows one at a
time. This feature adds that missing bulk/filtered removal capability next to
the existing **Advanced export** action, without touching the native per-row
delete.

This feature is deliberately kept separate from the proposed Retention
specification. Retention is an unattended, ongoing, age-based policy configured
once on the form; this feature is an immediate, administrator-triggered,
one-time action taken from the Submissions listing — for example, clearing a
batch of spam or test entries, or removing everything submitted during a known
bad window. Both ultimately delete the same kind of data the same way, so both
are specified to share one deletion engine rather than maintaining two.

**Native mass actions.** Xperience by Kentico's listing UI page template has a
publicly documented, first-party **mass actions** mechanism
(`PageConfiguration.MassActions`) that renders a checkbox column and a
contextual action toolbar below the search bar once the administrator checks
at least one row, delivering the checked rows' primary keys to a page command
as `IEnumerable<int> identifiers`. `FormSubmissionsTab` (the page this feature
targets) uses the same listing-template base as Kentico's own Recycle Bin and
Content Hub pages, which already ship bulk-delete mass actions built the same
way. This has been verified three ways during research for this
specification: by inspecting the `Kentico.Xperience.Admin` package (the
`MassActions` property is present at both the minimum supported `30.11.0` and
the latest verified `31.7.2`), by the public documentation for the Listing UI
page template, and by a runtime check against a running `31.1.2`
administration instance, where a test mass action correctly rendered its
checkbox column and toolbar on this exact listing. The same check also
confirmed that a second, independent `PageExtender<FormSubmissionsTab>` (this
feature's own, separate from the existing Export extender) composes correctly
with the extender that already registers **Export** — both contributed to the
same page without conflict.

This means quick removal does not require this toolkit to build, own, or
extend any row-selection UI. It only needs to register a mass action and
handle the resulting IDs, the same pattern `form-submission-export.md`'s
**Export selected** mass action uses.

## Goal

Let an authorized administrator remove more than one submission at a time from
the Submissions listing — either any subset of the current page (including all
of it) using Xperience's native selection UI, or everything matching an
explicit date range and/or record cap via Advanced delete, with a mandatory
preview of how many records would be removed before that action can run. Never
remove data the administrator has not first seen the scope of.

## Entry point and navigation

`Forms -> <selected form> -> Submissions`

This feature adds two independent entry points, neither of which modifies the
native per-row trash icon:

1. **Delete selected** — a native mass action that appears in Xperience's own
   selection toolbar once the administrator checks at least one row.
2. **Advanced delete** — a single header action button that opens the Advanced
   delete dialog directly; it needs no dropdown, since it is the only
   header-level action this feature adds.

**Styling.** `Kentico.Xperience.Admin.Base.ButtonColor` exposes only two
values, `Primary` and `Secondary` (confirmed by reflecting on
`Kentico.Xperience.Admin.Base.dll`). **Advanced delete** is registered with
`ButtonColor.Secondary` — the same color as **Advanced export** — so the two
header actions read as equal-weight siblings rather than one looking more
important than the other, now that neither is a one-click primary path (see
`form-submission-export.md`'s Entry point and navigation for the full
reasoning). **Advanced delete** additionally sets `Destructive = true` (a
separate flag from `ButtonColor`), which is what actually gives it its
cautionary/danger treatment — `ButtonColor.Secondary` controls weight, not
color intent.

**Visibility.** Register **Advanced delete** only when the selected form has
at least one submission, using the same `HasAnySubmissionsAsync` existence
check `form-submission-export.md` defines for **Advanced export** (both page
extenders call the same method, so the two header actions apply identical
criteria and appear/disappear together). A form nobody has ever submitted has
nothing to delete. This check runs once when the page configures its actions
and is independent of the listing's current search or filter state — a search
that temporarily matches zero rows does not hide the button, since Advanced
delete's date range and record limit operate over all of the form's
submissions, not the current view. **Delete selected** needs no such gate: it
is already self-gated by the platform's native selection UI, which cannot
present a checkable row when the listing has none.

### Quick delete (mass action)

**Label.** The registered button's visible text is **Delete**, not
**Delete selected** — the mass-action toolbar already displays a "Selected N
item(s)" count next to it, so appending "selected" to the button restates
what the count already says, and it matches **Export selected**'s label
change for the same reason. This specification keeps calling the feature
**Delete selected** in prose for clarity against **Advanced delete**; only
the on-screen label is shortened.

- Registered independently of **Export selected**, using the same native
  mechanism: both can coexist in the same selection toolbar, the way Content
  Hub's own Publish/Translate/Move/Delete mass actions coexist today. Neither
  feature owns the selection UI; both register their own action against it.
- Uses the platform's built-in confirmation support
  (`AddCommandWithConfirmation`) with `Destructive = true`, so the button and
  its confirmation prompt render with Xperience's own destructive-action
  styling rather than anything custom-built. Confirm during implementation
  whether the native confirmation template includes the selected count
  automatically; if not, use the richer confirmation-content overload to state
  it explicitly. Either way, the confirmation states that the action is
  permanent before anything is deleted.
- The checked rows' identifiers arrive directly as the command's
  `IEnumerable<int> identifiers` parameter. The client never captures or
  transmits them itself.
- The server independently re-validates every identifier as positive,
  distinct, and belonging to the selected form before deleting anything — the
  platform's selection is a UI convenience, not a trust boundary.
- On success, the listing refreshes (via the mass-action result's reload
  behavior) and a toast confirms the number removed.

**Compatibility.** Confirm during implementation whether the native selection
UI is bounded to the current listing page, or whether the platform offers a
"select all matching" affordance that can span pages (some Xperience listing
templates offer this for large result sets) — the same open question noted in
`form-submission-export.md` for **Export selected**. If it can span pages,
Quick delete simply deletes whatever the platform reports as selected; this
feature does not independently constrain or extend the platform's own
selection scope, and the permission check and confirmation step apply
regardless of how large the selection is.

### Advanced delete

```text
Advanced delete                                            [Close]

From                                      To
[ date picker ]                          [ date picker ]

Number of records
[                                                    ]
Maximum submissions removed. Leave empty to match all submissions in range.

Order by
[ Submitted - oldest first                         v ]

[ Preview matching count ]

12 submissions match these criteria and would be permanently deleted.

Consider exporting these submissions first -> [Export these]

Type DELETE to confirm
[                                                    ]

[validation or safe server error, when present]

                                         [Cancel] [Delete]
```

- Filters mirror Advanced export's date range, record limit, and ordering
  options and validation exactly (From/To inclusive-day semantics, a positive
  whole-number record limit, deterministic Submitted oldest/newest-first plus
  the primary key as a tie-breaker), so administrators reuse a filter model
  they already know from Export.
- **Preview matching count** is required before **Delete** becomes available.
  It returns only a count, never submission data — deletion is higher risk than
  export, so this dialog does not render a data grid the way Preview does in
  Advanced export.
- Changing any filter after a preview invalidates it; **Delete** is disabled
  until a current preview has been run against the exact filters submitted.
- **Export these** opens Advanced export prefilled with the same date range, so
  an administrator can capture a copy before removing it. This is a manual
  convenience link, not an automatic or required export.
- **Delete** additionally requires typing an exact confirmation phrase before
  it is enabled. Advanced delete uses this custom confirmation, rather than the
  native mass-action confirmation, because it needs to gate on a current
  preview and is not itself a mass action — it operates on a filter, not a
  checked selection.
- While Preview or Delete is being prepared, all actions and dismissal controls
  are disabled and the selected action shows progress.
- Validation and safe server errors appear within the dialog.

## Deletion contract

- Deletion is permanent. There is no soft delete, recycle bin, or undo,
  matching the native per-row delete's own behavior.
- Deleting a submission also deletes its uploaded files, verified empirically
  end to end (submitted a file, confirmed the physical file on disk, deleted
  the submission, confirmed both the database row and the physical file were
  gone). `BizFormItem.Delete()` alone does **not** do this — confirmed by the
  same testing, contrary to this specification's original assumption — so the
  toolkit deletes the physical file itself before deleting the row:
  - The only public API found for resolving an uploaded file's physical path
    (`IBizFormFilePathProvider`) was introduced in Xperience by Kentico
    `31.6.0`, far above this toolkit's `30.11.0` floor, so it is not used.
    Instead the physical path is built directly from
    `SystemContext.WebApplicationPhysicalPath` combined with the documented
    default storage location, `assets/BizFormFiles`, using `CMS.IO.Path`/
    `CMS.IO.File` (stable back to `30.11.0`, and itself an abstraction over
    local disk, Azure Blob, or Amazon S3 storage).
  - The **Kentico.FileUploader** form component (the current file-upload
    component; an older, differently-named component identifier does not
    match it — see the Export specification's `IsUploadedFileField` fix)
    stores its field value as a single raw string in the form
    `"{systemFileName}/{originalFileName}"`, confirmed directly against the
    underlying database column, rather than as a structured `BizFormUploadFile`
    object. Parsing this format correctly (system file name, not original
    file name) is required to locate the physical file; see
    `UploadedFileName.ExtractSystemFileName`.
  - This deletion order and mechanism apply identically to quick delete and
    Advanced delete, since both go through the shared deletion service.
- Deletion never removes the form definition, its data class, or its table.
- Deletion does not touch contacts, activities, consents, or other data covered
  by Xperience's GDPR "right to be forgotten" flow.
- Both delete paths operate only on the selected form; there is no
  cross-form or bulk-across-forms deletion.

## Shared deletion engine

Quick delete, Advanced delete, and the scheduled retention purge must all
resolve to the same underlying primitive: delete a bounded, ordered batch of a
form's `BizFormItem` rows (and their uploaded files), batched with keyset
pagination. Build this as one shared internal service so the toolkit has a
single, well-tested code path that deletes submission data, instead of three
independent ones:

- Quick delete supplies the platform-provided, server-validated list of
  submission IDs;
- Advanced delete and the scheduled retention task supply date-range/record-
  limit/ordering criteria that the service itself resolves to matching IDs.

When both this feature and the Retention specification are implemented,
generalize the Retention specification's suggested "Retention purge service"
component into this shared service. Doing so does not change the Retention
specification's external behavior, permission, or acceptance criteria.

## Administration integration

- Register this feature's own `PageExtender<FormSubmissionsTab>`, separate
  from the existing Export extender, adding the **Advanced delete** header
  action and the **Delete selected** mass action to the same
  `Page.PageConfiguration`. Multiple independent extenders on this page have
  been confirmed to compose correctly (see Context).
- Quick-delete command: the mass-action command signature
  (`IEnumerable<int> identifiers`) required by the platform, permission-gated,
  validating IDs before invoking the shared deletion service.
- Advanced-delete flow: a page command validates the requested filters and,
  only after confirmation, invokes the shared deletion service. A separate
  preview/count command returns only a count for the given filters.
- Keep production server and client code in the distributable toolkit package.
  Register services through the existing `AddFormsToolkit()` startup
  extension.

## Security and privacy

- Define and enforce a dedicated permission (for example
  `XperienceCommunity.FormsToolkit.DeleteSubmissions`), independent of the
  export permission and the Retention specification's permission. Being able
  to view/export submissions, or to configure a retention policy, must not by
  itself grant the ability to immediately mass-delete them.
- Show both the **Delete selected** mass action and the **Advanced delete**
  header action only to users who can access the form's submissions and hold
  this permission; both commands independently re-check it.
- Apply normal Xperience antiforgery validation to the Advanced-delete
  commands (the platform applies its own request validation to mass-action
  commands).
- Treat quick-delete submission identifiers as untrusted even though the
  platform supplies them: accept only positive, distinct IDs belonging to the
  selected form, re-validated at request time, exactly as **Export selected**
  already treats them.
- Treat Advanced-delete filters as untrusted; validate range, format, and
  bounds server-side independent of client-side validation.
- Never log submission values or uploaded-file contents. Safe operational logs
  may include the form ID, user ID, operation (quick or advanced), effective
  filters or ID count, deleted count, duration, and success/failure state.
- The preview/count command returns only a number; it must never return
  submission data.
- Respect Xperience's read-only deployment mode and surface its standard safe
  error for both delete paths.

## Compatibility and API gate

Before implementation, inspect the public API metadata and runtime behavior for
both the minimum supported Xperience version and the latest build-verified
version. Confirm:

- the exact `MassActions` registration overload to use
  (`AddCommandWithConfirmation` and its confirmation-content options), and
  whether its confirmation template includes the selected count automatically;
- whether the native selection UI is page-bounded or can span pages via a
  "select all matching" affordance (see Quick delete's Compatibility note);
- that the shared deletion service's explicit file-cleanup step (physical
  path plus the `Kentico.FileUploader` raw-value parsing) continues to match
  reality on the target version, since `BizFormItem.Delete()` alone does not
  remove uploaded files and `IBizFormFilePathProvider` remains unavailable
  before Xperience by Kentico `31.6.0` (see Deletion contract); and
- that no required behavior depends on `.Internal` namespaces or reflection.

Registering a second, independent `PageExtender<FormSubmissionsTab>` alongside
the existing Export extender, and adding to `PageConfiguration.MassActions`,
are both already confirmed working as described in Context; they do not need
re-verification, only reproduction in the toolkit's own code and tests.

## Error behavior

- Unknown or deleted form: safe not-found administration error; nothing is
  deleted.
- Invalid Advanced-delete filters, or the Advanced delete dialog's **Delete**
  button requested without a current matching preview: validation error;
  nothing is deleted.
- Quick delete with an empty or invalid identifier list: safe validation
  response; nothing is deleted. Never fall back to deleting an unscoped or
  full set.
- Unauthenticated request: normal platform authentication behavior.
- Authenticated but unauthorized request: forbidden response; nothing is
  deleted.
- Partial failure mid-batch (for example, a database error partway through):
  stop cleanly, log safe diagnostics, and report the actual deleted count
  rather than the originally requested count. Each row's `Delete()` call is a
  discrete operation, so a mid-batch failure does not leave a row half
  deleted.

## Performance and consistency

- Delete in bounded batches (the same ~1,000-row internal default used by
  export and retention) using keyset pagination; avoid offset pagination.
- Advanced delete applies its record limit using the same deterministic
  ordering shown in the dialog, plus the primary key as a tie-breaker.
- Quick delete's scope is bounded by whatever the platform's native selection
  UI allows (see Compatibility); the shared deletion service still processes
  it in bounded batches rather than assuming a small set.
- Concurrent operations are safe: deleting an already-deleted submission (for
  example, a race with the native per-row delete, another Advanced delete, or
  a scheduled retention run on the same form) is a no-op for that row rather
  than an error.
- Propagate cancellation tokens through the shared deletion service.

## Suggested component boundaries

- `FormSubmissionRemovalPageExtender`: this feature's own extender, separate
  from the Export extender, permission-gates and registers the **Advanced
  delete** header action — only when `HasAnySubmissionsAsync` reports at
  least one submission — and the **Delete selected** mass action
  unconditionally, since it is already self-gated by the platform's native
  selection UI.
- Quick-delete command: the mass-action command handler, validates
  platform-supplied IDs, invokes the shared deletion service.
- Advanced-delete dialog and its page commands: filter validation,
  preview/count, and confirmed delete.
- Shared submission deletion service (generalized from the Retention
  specification's purge service): resolves matching IDs or accepts explicit
  IDs, explicitly deletes each row's uploaded files (see Deletion contract)
  before calling `BizFormItem.Delete()`, in bounded batches, and is used by
  quick delete, Advanced delete, and the scheduled retention task.

## Test strategy

### Unit tests

- quick-delete ID validation (positive, distinct, belonging to the selected
  form), independent of the platform having already selected them;
- Advanced-delete filter validation (date range, record limit, ordering),
  reusing the export specification's equivalent test cases;
- preview/count accuracy against seeded data;
- **Delete** (Advanced delete's confirm button) disabled until a current
  preview exists, and invalidated when a filter changes after a preview;
- shared deletion service batching and uploaded-file cleanup, exercised from
  both call shapes (explicit IDs and filter-resolved IDs);
- permission-gated action and command behavior; and
- safe handling of an empty or malformed identifier list from the mass-action
  command.

### Integration and runtime tests

- **Delete selected** mass action and **Advanced delete** header action
  visibility for authorized and unauthorized users, independent of the export
  and retention permissions;
- **Advanced delete** hidden for a form with zero submissions, and shown once
  a submission exists, independent of the listing's current search/filter
  view — the same behavior verified for **Advanced export**;
- **Delete selected** appearing only once at least one row is checked, and
  deleting exactly the checked rows;
- **Delete selected** and **Export selected** coexisting in the same selection
  toolbar without interfering with each other;
- Advanced delete across open and closed date ranges and record limits,
  including zero matches;
- confirmation-phrase gating and preview invalidation on filter change;
- **Export these** opening Advanced export prefilled with the same date range;
- uploaded-file cleanup after both quick and Advanced delete;
- concurrent quick delete, Advanced delete, the native per-row delete, and a
  scheduled retention run against the same form;
- a large seeded Advanced delete: bounded batches, no timeout, and an accurate
  reported count; and
- read-only deployment behavior for both delete paths.

## Acceptance criteria

- A **Delete selected** mass action appears in the Submissions listing's
  native selection toolbar once at least one row is checked, and removes
  exactly the checked submissions after the platform's confirmation step.
- An **Advanced delete** header action, styled `ButtonColor.Secondary` and
  `Destructive` to pair visually with **Advanced export** while still reading
  as the cautionary action of the two, opens the Advanced delete dialog. It is
  shown only when the form has at least one submission.
- The native single-row delete action is unchanged, and the existing
  **Advanced export**/**Export selected** actions are unaffected by this
  feature.
- **Advanced delete** removes only submissions matching an explicit date range
  and/or record limit, only after a current preview count has been shown and a
  confirmation phrase typed.
- Both delete paths remove each deleted submission's uploaded files
  (verified empirically per Deletion contract) and never remove the form, its
  data class, or another form's data.
- Quick delete, Advanced delete, the native per-row delete, and the scheduled
  retention purge all rely on one shared deletion engine rather than
  duplicated logic.
- Deletion activity is safely logged without submission values.
- UI visibility and both commands enforce a dedicated delete permission,
  independent of the export and retention permissions.
- Production code remains in the toolkit package.

## Out of scope

- Building any custom row-selection or checkbox UI. Quick delete relies
  entirely on Xperience's native mass-action selection mechanism, the same one
  **Export selected** uses.
- Undo, recycle bin, or restoring deleted submissions.
- Automatically exporting submissions before deleting them; **Export these** is
  a manual convenience link only.
- Filtering Advanced delete by a specific field's value rather than the
  submitted date. Reconsider only alongside a validated need and a safe way to
  accept such filters as metadata-backed identifiers rather than arbitrary
  expressions, consistent with how Advanced export already treats column
  selection.
- Deleting across multiple forms in one operation.
- A public headless/REST delete endpoint.

## References

- [Manage form submissions](https://docs.kentico.com/documentation/business-users/digital-marketing/forms/manage-form-submissions.html)
- [Listing UI page template](https://docs.kentico.com/documentation/developers-and-admins/customization/extend-the-administration-interface/ui-pages/reference-ui-page-templates/listing-ui-page-template.html)
- [UI page extenders](https://docs.kentico.com/documentation/developers-and-admins/customization/extend-the-administration-interface/ui-pages/ui-page-extenders.html)
- [UI page permission checks](https://docs.kentico.com/documentation/developers-and-admins/customization/extend-the-administration-interface/ui-pages/ui-page-permission-checks.html)
- [Form data API](https://docs.kentico.com/api/digital-marketing/form-data.html)
- [Secure custom endpoints](https://docs.kentico.com/documentation/developers-and-admins/customization/secure-custom-endpoints.html)
- This repository's `form-submission-export.md` and `form-submission-retention.md`
  specifications, which this feature shares the native mass-action mechanism,
  filter semantics, batching conventions, and deletion mechanics with.

Before implementation, inspect the public API metadata for the exact installed
Xperience package version instead of copying signatures from an older Xperience
generation or assuming continuity with a different Xperience by Kentico
version.
