# Feature specification: Form submission export

Status: Implemented; prerelease validation in progress. **Export selected**
(mass action) is implemented as a purely additive extension that does not
modify the originally implemented current-view/Advanced export behavior.
Repository: `Simplea/xperience-community-forms-toolkit`  
Target baseline: Xperience by Kentico `31.2.1`, .NET 8

## Context

Xperience Community Forms Toolkit is an independently owned integration maintained
by Simplea for Xperience by Kentico. It is a reusable library, not an Xperience
website and not a Kentico-owned product. The Dancing Goat project under `examples/`
is the integration host used to run and verify the library.

The feature adds data export to the existing administration page at:

`Forms -> <selected form> -> Submissions`

Kentico Xperience 13 provides quick Excel, CSV, and XML exports plus an Advanced
export dialog. This feature adapts that workflow to Xperience by Kentico's React
administration UI and supported extension APIs. It does not copy the Kentico 13
visual design or expose its global-administrator SQL options.

## Goal

Let an authorized administrator:

- quickly export the submissions and columns shown on the current listing page to
  Excel, CSV, or XML, preserving the listing's displayed order; or
- open Advanced export to select the format, dates, maximum number of records,
  ordering, headers, delimiter, and columns before previewing or exporting.

All formats must be safe for untrusted form values, suitable for large exports, and
downloaded without navigating away from the submissions page.

## Entry point and navigation

The page header contains one action named **Export** with a dropdown indicator. Do
not label it **Export submissions**, because the surrounding Submissions page
already establishes what is being exported.

Selecting **Export** opens a compact action menu anchored to the unchanged header
action. Use Xperience by Kentico's dropdown and action-menu components and visual
conventions; do not copy Kentico 13's black menu styling.

Xperience by Kentico 31.2.1 renders page-extender custom header actions as standard
buttons and loads their custom React component only after the button is activated.
Do not overlay or replace that button with a split button after activation, because
doing so causes a visual jump and can overlap adjacent listing controls.

```text
Export  v
┌──────────────────────────┐
│ Export Page to CSV      │
│ Export Page to Excel    │
│ Export Page to XML      │
│ Advanced export         │
└──────────────────────────┘
```

- CSV appears first as the default/recommended quick format.
- The **Page** qualifier is unconditional — it appears in the label whether or
  not any rows are currently checked in the mass-action selection described
  below. This header component never needs to know whether a selection
  exists; the label always tells the truth about its own scope on its own,
  without any cross-component awareness. It exists specifically so this
  action cannot be confused with **Export selected**, which sits in a
  separate part of the page whenever a selection is active.
- Selecting CSV, Excel, or XML immediately exports the current listing page.
  This header action and its behavior are unchanged by **Export selected**
  below (beyond the label); it always covers the current page, exactly as
  originally implemented.
- While a quick export is being prepared, menu actions are disabled and the selected
  action indicates progress.
- A successful download leaves the submissions page open and closes the menu.
- **Advanced export** closes the menu and opens the advanced dialog. It does not
  navigate to another administration page.
- Do not provide **Reset view**. Xperience by Kentico does not expose the equivalent
  persisted listing-view configuration used by Kentico 13, so the action would add
  little value and imply capabilities this integration does not provide.
- Clicking outside the menu or pressing Escape dismisses it without changing the
  listing.

### Export selected (mass action)

Xperience by Kentico's listing UI page template has a native, publicly
documented **mass actions** mechanism: `PageConfiguration.MassActions` renders
a checkbox column and a contextual action toolbar immediately below the search
bar once the administrator checks at least one row. `FormSubmissionsTab` uses
the same `InfoObjectListingConfiguration`/listing-template base as Kentico's
own Recycle Bin and Content Hub pages, which already ship mass actions built
the same way. Implementing this feature confirmed both that the checkbox
column and toolbar render correctly on this exact listing, and an important
distinction the public documentation does not spell out:

- A mass action registered with `AddCommand`/`AddCommandWithConfirmation`
  (a framework-rendered button with no custom component, the same pattern
  `RecycleBinList.MassPermanentlyDelete` uses) genuinely does get the checked
  rows' primary keys auto-bound to an `IEnumerable<int> identifiers` command
  parameter. This path is real and low-risk for actions whose entire effect is
  server-side plus a grid refresh.
- A mass action registered with `AddActionWithCustomComponent` does **not**
  get this for free. The framework's auto-binding is tied to its own built-in
  button/click handling; once a custom component owns the click, nothing
  supplies the selection to it automatically — confirmed by triggering a
  `System.Text.Json` deserialization failure trying to parse an empty request
  body directly as `IEnumerable<int> identifiers`.

**Export selected** needs a custom component regardless, because delivering a
file requires client-side code to react to a response and start a download —
something a framework-driven button's response handling (grid reload only)
cannot do, and something no `[PageCommand]` can do directly since it only
returns JSON. Given that, **Export selected** resolves the selection the same
way quick export's header action already does: it reads the checked rows
directly from the rendered listing.

Add one native mass action, **Export selected**, registered independently of
the existing **Export** header action:

- It appears only when the administrator has checked at least one row, using
  Xperience's native mass-action checkbox column and toolbar. This toolkit
  does not build, own, or inject that checkbox column.
- Selecting it opens a small dialog offering a format choice — **CSV**,
  **Excel**, or **XML** — with **Cancel** and **Export** actions. Reviewing
  the implemented feature against a running administration instance showed
  that a one-click, CSV-only mass action left no way to get Excel or XML for
  a hand-picked selection, since Advanced export can only target a date/limit
  filter, not an arbitrary set of checked rows. The dialog closes this gap
  without adding a new server endpoint: the existing current-view download
  endpoint already supports all three formats, because the header action's
  own CSV/Excel/XML quick exports already use it.

  ```text
  Export selected                                            [Close]

  Export to
  [ CSV                                                v ]

  [validation or safe server error, when present]

                                          [Cancel] [Export]
  ```

- On **Export**, the client component reads which rows are checked directly
  from the rendered listing (the checked `input[type="checkbox"]` within each
  `data-testid="table-row"`, reusing the same accessible-role/`data-testid`
  capture already specified for quick export's current-page rows) and submits
  those submission IDs, together with the chosen format, to the existing
  current-view download endpoint. No new server endpoint or page command is
  needed.
- The exported columns use the same listing-visible field set quick export
  already uses (`VisibleInListing` fields only — not the full Advanced-export
  column list, since this reuses the current-view endpoint's existing
  validation, which enforces that constraint), with headers and a comma
  delimiter for CSV. Administrators who need a different column set for a
  selected set can use Advanced export instead, filtered appropriately.
- Because this is non-destructive, it requires no confirmation beyond the
  dialog's own **Cancel**/**Export** choice.
- The server independently re-validates every submitted ID, format, and
  column as positive/distinct/belonging to the selected form before reading
  anything — identical treatment to quick export's current-page request,
  since it is the same endpoint.
- The existing **Export** header action, its dropdown, and its current-page
  scope are completely unchanged by this addition beyond its **Page** label
  qualifier (see Entry point above). The two are independent entry points
  that happen to sit on the same page and share no client code path beyond
  the row-capture helpers.

**Compatibility.** The native selection UI observed during implementation was
bounded to the current listing page; no "select all matching" affordance
spanning pages was present. If a future version adds one, **Export selected**
would simply read whatever rows are rendered as checked — it does not
independently constrain the platform's selection UI.

### Advanced export dialog

The advanced view is a large, vertically scrollable dialog. On narrow supported
administration viewports, controls stack without horizontal scrolling.

```text
Advanced export                                          [Close]

Export to
[ Excel                                              v ]

From                                      To
[ date picker ]                          [ date picker ]

Number of records
[                                                    ]
Maximum submissions included. Leave empty to export all matching submissions.

Export column header
[x]

Order by
[ Submitted - oldest first                         v ]

Columns
[Select all] [Deselect all] [Default selection]

[x] Submission ID
[x] Submitted
[x] <configured form field caption>
[x] ...

[validation or server error, when present]

                               [Cancel] [Preview] [Export]
```

When **CSV** is selected, show an additional **Delimiter** selector with **Comma**
and **Semicolon**. When **XML** is selected, hide and ignore **Export column
header** because XML uses element names instead of a separate header row.

Advanced-dialog behavior:

- **Cancel** and the close control dismiss the dialog only while no request is in
  progress.
- **Preview** downloads a real file in the selected format containing at most 100
  matching data records. The dialog stays open afterward.
- **Export** downloads the requested file. The dialog may close after the download
  starts.
- While Preview or Export is being prepared, all actions and dismissal controls are
  disabled and the selected action shows progress.
- At least one column must be selected.
- Changing format preserves options that remain applicable to the new format.
- Validation and safe server errors appear within the dialog.

## Quick export defaults

The CSV, Excel, and XML quick actions mirror Kentico 13's current-view behavior:

- export only records displayed on the currently selected listing page;
- preserve their current displayed order, including user-selected column sorting;
- honor the current search/filter indirectly because only the rendered matching
  record identifiers are included;
- export only the data columns currently rendered by the submissions listing, in
  their displayed order; the Actions column is never exported;
- include column headers for Excel and CSV;
- use a comma delimiter for CSV; and
- do not apply Advanced export's date, record-limit, or manual-ordering options.

The public `FormSubmissionsTab` extension surface does not pass its React table
manager state to a custom header action. For the target Xperience version, the
client therefore captures the current table's ordered row identifiers and column
names from the rendered listing using its accessible roles and stable
`data-testid` values. It never copies submitted cell values into the request. The
server validates the identifiers against the selected form, re-reads those records,
selects validated metadata-backed fields, and emits records in the supplied order.

**Export selected** (see above) uses a related but distinct DOM read: instead
of every rendered row, it reads only the rows whose native mass-action
checkbox is checked, then reuses this same current-view request/response
contract. The row-identifier and column-identifier resolution, and the
server-side validation, are identical either way.

The row identifier is obtained from the listing's generated edit-row link. If the
expected listing structure cannot be read, fail safely with an administration error;
never fall back to exporting all submissions because that would silently change the
scope selected by the user. Empty current pages produce a valid empty file using the
listing's default visible columns.

## Advanced export options

### Export format

Supported formats:

- **Excel**: `.xlsx`
- **CSV**: `.csv`
- **XML**: `.xml`

The default format when Advanced export opens is Excel, matching the Kentico 13
workflow.

### Date range

- **From** is optional and inclusive from the beginning of the selected day.
- **To** is optional and inclusive through the end of the selected day. Implement
  the database predicate as an exclusive start of the following day to avoid
  precision errors.
- Dates are interpreted in the time zone used by the Xperience administration
  application and converted appropriately before filtering `FormInserted`.
- If **From** is after **To**, do not start the preview or export and show a
  validation message.

### Number of records

- **Number of records** is an optional maximum number of exported data records, not
  a page size.
- Empty means every submission matching the other Advanced export options.
- A supplied value must be a positive whole number representable by the server-side
  request model. Reject zero, negative, fractional, malformed, and overflowing
  values.
- Header rows do not count toward the limit.
- The limit is applied after date filtering and using the selected deterministic
  ordering.
- A value greater than the available matching records exports all matching records.
- Preview uses the smaller of **Number of records** and 100. If **Number of
  records** is empty or greater than 100, Preview uses 100.

### Export column header

- The option is available for Excel and CSV and is selected by default.
- When selected, the first output row contains the selected field captions.
- When cleared, Excel and CSV contain only data rows.
- XML does not show this option and always uses stable element names.

### Delimiter

- The option is available only for CSV.
- Supported values are comma and semicolon.
- Comma is the default.

### Ordering

Do not accept SQL expressions from the client. Provide these choices:

- **Submitted - oldest first** (default)
- **Submitted - newest first**

Always add the form submission primary key in the same direction as a deterministic
tie-breaker. Ordering is independent of which columns are selected for output.

### Columns

- Build the available list from supported Xperience form/data-class metadata APIs.
- Show field captions in the dialog.
- Include `Submission ID` and `Submitted`, followed by configured form fields in
  their form order.
- Default selection includes all available exportable columns.
- **Select all** selects every exportable column.
- **Deselect all** clears the selection; Preview and Export remain unavailable until
  at least one column is selected.
- **Default selection** restores the initial selection and order.
- Preserve the configured order. Arbitrary column reordering is not required.
- Never trust client-supplied column names. Send stable field identifiers and
  validate every selection against server-resolved metadata.
- Do not expose internal/system columns other than the explicitly supported
  `Submission ID` and `Submitted` columns.

## Form and submission discovery

- Use the selected `FormId` supplied by `FormSubmissionsTab`; do not ask the user to
  select the form again.
- Resolve `BizFormInfo`, then its `DataClassInfo`, and use the resulting class name
  with `BizFormItemProvider.GetItems(formClassName)`.
- Resolve the physical primary-key field from supported metadata. Do not assume that
  every generated form table uses the physical column name `ItemID`.
- Build the exportable field list from supported Xperience form/data-class metadata
  APIs. Do not parse the form definition XML unless no supported API provides the
  required information.
- Do not accept a form class name, table name, or arbitrary database column name
  from the client.
- If no records match, return a valid empty export: header-only for Excel or CSV
  when headers are enabled, and a valid root-only XML document.

## Value handling shared by all formats

- Format date/time values using ISO 8601 with an explicit offset or `Z`.
- Format numbers using invariant culture.
- Format booleans as `true` or `false`.
- Export null or missing values as empty cells/elements.
- Preserve multiline text where the selected format supports it.
- For uploaded-file fields, export the original/display filename only. Never export
  file bytes, server paths, temporary paths, or an unauthenticated download URL.
  Confirmed and fixed post-implementation: `IsUploadedFileField`'s component-
  identifier check used a stale value (`Kentico.Forms.Web.Mvc.FileUploaderComponent`,
  an old MVC-era type name) that never matches the actual registered identifier,
  `Kentico.FileUploader`, so upload fields were never recognized as such. Separately,
  that component's raw field value is a single string in the form
  `"{systemFileName}/{originalFileName}"`, not a structured `BizFormUploadFile`
  object — confirmed directly against the underlying database column. Both are
  fixed in `UploadedFileName`/`FormSubmissionExportService.IsUploadedFileField`;
  see the Removal specification's Deletion contract, where the same investigation
  originated.
- For values without a specialized conversion, use the supported Xperience value
  representation and an invariant string conversion.
- A value that cannot be converted must not fail the entire export. Emit an empty
  value or safe invariant representation and log only the form ID, field identifier,
  submission ID, and exception type—never the submitted value.

Protect textual values against spreadsheet formula injection. For CSV, if a value,
after leading whitespace, begins with `=`, `+`, `-`, or `@`, or begins with a tab or
carriage return, prefix the value with a single quote before CSV escaping. For
Excel, write untrusted textual values as text cells rather than formulas and apply
equivalent protection where required by the writer. XML values must be XML-escaped.

## Format contracts

### CSV

- Media type: `text/csv; charset=utf-8`.
- UTF-8 with a byte-order mark.
- Configured comma or semicolon delimiter.
- CRLF record separators.
- Standard CSV quoting and doubled embedded double quotes.
- Preserve multiline values through correct quoting.

### Excel

- Media type: `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`.
- Produce a valid `.xlsx` Open XML workbook, not an HTML or CSV file with an `.xlsx`
  extension.
- Use a worksheet named `Submissions`; if Excel's worksheet row limit would be
  exceeded, continue in deterministically numbered worksheets without silently
  truncating records.
- Write untrusted strings as text cells. Do not create formulas, macros, external
  links, or embedded submitted files.
- Respect Excel's per-cell text limit. If a value exceeds the supported cell length,
  truncate only the Excel representation and log safe metadata without logging the
  value. CSV and XML remain capable of carrying the complete value.

### XML

- Media type: `application/xml; charset=utf-8`.
- UTF-8 encoding with an XML declaration.
- Use one `FormSubmissions` root element and one `Submission` element per record.
- Use stable, valid XML element names derived from the selected system or form field
  identifiers. Use XML-safe name encoding rather than captions as element names.
- Emit selected fields in the configured export order.
- Do not emit a document type declaration, external entities, processing
  instructions, raw database metadata, or submitted file contents.

## Filenames and download behavior

Use safe filenames with the selected extension:

`<sanitized-form-code-name>-submissions-<yyyyMMdd-HHmmss>Z.<extension>`

Preview filenames include `-preview-` before the timestamp. Supply a
standards-compliant `Content-Disposition: attachment` header. Set `Cache-Control:
no-store` and equivalent headers where appropriate.

## Administration integration

Use a page extender registered for the existing submissions page. Add a single
custom-component header action named **Export**. The custom React administration
component presents a native Xperience dropdown menu, captures current-view row and
column identifiers for quick exports, and owns the Advanced export dialog.

Because a page command returns an administration command response rather than a
streamed browser file, use an authenticated server endpoint for the actual file.
The page command validates the requested options and returns a short-lived download
URL containing a protected token. The browser starts the download without replacing
the administration page.

The protected-token flow is used for Advanced export. Quick current-view export uses
an authenticated, antiforgery-protected POST download request containing only the
format, ordered submission IDs, and stable field identifiers. The endpoint validates
the export permission, selected form, row identifiers, and fields before streaming
the response. This avoids oversized URLs and avoids persisting a server-side view
snapshot.

Obtain quick-export antiforgery headers from Xperience's active administration
antiforgery context. Xperience consumes and clears the bootstrap `<meta>` values
during application startup, so custom components must not attempt to read token
values directly from the DOM.

Keep production server and client code in the distributable toolkit package.
Dancing Goat only consumes and hosts the packaged feature.

## Security and privacy

Form submissions can contain personal or sensitive information. These requirements
are mandatory:

- Define and enforce a toolkit-specific export permission.
- Show **Export** only to users who can access form submissions and have that
  permission.
- Independently authenticate and authorize the download endpoint.
- For standalone controller routes, evaluate the permission against the Forms
  application identifier and authenticated administrator using Xperience's
  application permission evaluator. Do not use `IUIPermissionEvaluator` there,
  because it requires an active UI-page tree context that controller routes do not
  have.
- Support both Xperience administration authentication schemes used by the target
  version.
- Bind the protected token to the current user, selected form, format, date range,
  effective record limit, ordering, header option, delimiter, selected columns, and
  Preview/Export operation.
- Require normal Xperience antiforgery validation and an independent export
  permission check on the quick-export POST endpoint.
- Treat quick-export row and column identifiers as untrusted. Accept only positive,
  distinct submission IDs and metadata-backed fields belonging to the selected form.
  Never accept submitted cell values from the browser.
- Apply the same untrusted-identifier validation to **Export selected**: it reuses
  the current-view download endpoint, so the same server-side re-validation
  (positive, distinct, belonging to the selected form; listing-visible columns
  only) and the same export-permission and antiforgery checks already apply
  without any new code.
- Give tokens a short lifetime. A Preview token must never produce more than 100
  records or be convertible into an unrestricted export.
- Re-resolve and validate the form and selected field identifiers at download time.
- Apply normal Xperience CSRF protection to the page command that prepares the
  download.
- Never persist completed exports in application or web-accessible storage.
- Never log submission values, output rows, uploaded-file paths, or generated files.
  Safe operational logs may include the form ID, user ID, format, range, effective
  limit, row count, duration, and success/failure state.
- Cancel database reads and output promptly when the HTTP request is aborted.

## Performance and consistency

- Do not impose a hidden record limit. Empty **Number of records** intentionally
  means every matching record.
- Retrieve records in bounded batches, with approximately 1,000 records as a
  reasonable internal default.
- Stream CSV and XML incrementally to the response.
- Generate Excel incrementally without materializing all submission records in a
  `DataTable`, giant string, or equivalent full in-memory representation.
- Quick export performs one bounded query for the identifiers shown on the current
  listing page and writes the result in the client-supplied display order. Its scope
  is naturally bounded by the configured listing page size.
- Avoid offset pagination. Use stable keyset pagination.
- Establish an upper submission-ID boundary when preparing the export so new
  submissions cannot make an active export unbounded.
- Apply the user limit by requesting no more than the smaller of the internal batch
  size and the number of records still required.
- Select only the columns required for ordering and output.
- Propagate cancellation tokens through supported query and writer operations.
- Prevent concurrent duplicate submissions from the dialog.
- Allow at most one active export per user and use a configurable, small
  application-instance concurrency limit for all exports. Return a safe retryable
  response when the limit is reached.

## Error behavior

- Unknown or deleted form: `404` with a generic administration error.
- Invalid options, date range, record limit, format, delimiter, ordering, or column
  selection: `400` with a safe validation message.
- Unauthenticated request: normal platform authentication behavior.
- Authenticated but unauthorized request: `403` without revealing submission data.
- Concurrency limit reached: `429` with a safe retry message before streaming starts.
- Failure after streaming begins: abort the download, log safe diagnostics, and
  never append an HTML error page to the output file.

## Suggested component boundaries

- `FormSubmissionsPageExtender`: permission-gates and places the **Export** action,
  and registers the **Export selected** mass action on the same page's
  `PageConfiguration.MassActions`.
- Administration export component: anchored dropdown menu, current-view identifier
  capture, antiforgery-aware quick requests, Advanced dialog, option validation
  feedback, and download initiation. Owns none of the mass-action selection UI,
  which is native to the platform.
- **Export selected** client component: reads checked rows from the rendered
  listing and posts to the existing current-view download endpoint. Adds no new
  server endpoint or page command.
- Export-options command: returns validated field metadata and creates protected
  download tokens for Advanced export.
- Export controller/endpoint: authenticates, authorizes, validates the token, sets
  headers, validates quick-export antiforgery requests, applies concurrency limits,
  and coordinates streaming.
- Export service: resolves the form, builds stable queries, applies filters,
  ordering and limits, and reads bounded batches.
- Format writers: separate CSV, Excel, and XML writers sharing value conversion and
  sensitive-file handling.
- Export options model: format, date range, optional record limit, ordering, header
  option, delimiter, selected columns, and Preview/Export operation.

Register all services through the repository's public Forms Toolkit startup
extension so consuming applications have one documented integration path.

## Test strategy

### Unit tests

- option validation for every format and conditional option;
- date conversion and invalid ranges;
- record-limit validation and Preview's effective 100-record cap;
- selected-column validation, default order, and empty selection;
- deterministic ascending and descending ordering;
- shared invariant value conversion and uploaded-file filename extraction;
- spreadsheet formula-injection protection;
- CSV encoding, delimiter, escaping, Unicode, multiline values, and headers;
- Excel workbook validity, text-cell behavior, headers, long-cell handling, and
  worksheet rollover;
- XML declaration, structure, name encoding, escaping, nulls, and prohibited DTDs;
- safe filenames for quick, advanced, and preview downloads; and
- protected-token binding for all export options.
- validation of current-view row identifiers and listing-backed column selections;
- preservation of the current-view row order;
- failure when the current listing structure cannot be captured; and
- **Export selected**'s checked-row capture and column restriction to
  listing-visible fields, reusing the current-view request/response contract
  and its existing validation; and
- **Export selected**'s format-choice dialog: default format, and validation
  that Cancel and Export behave correctly for each of CSV, Excel, and XML.

### Integration and runtime tests

- **Export** visibility for authorized and unauthorized users;
- dropdown quick exports for CSV, Excel, and XML;
- quick export after search, ascending/descending column sorting, paging, and page
  size changes;
- transition from the dropdown menu to Advanced export;
- quick-export POST requests using the active Xperience antiforgery header;
- **Export selected** appearing only once at least one row is checked via the
  platform's native selection UI, opening its format dialog, and exporting
  exactly the checked rows in the chosen format (CSV, Excel, and XML each);
- confirming the existing **Export** header action's behavior and current-page
  scope are unaffected by the presence of the new mass action, and that its
  dropdown items consistently show the **Page** label qualifier;
- format-dependent Advanced export controls;
- all combinations of open and closed date ranges;
- empty, smaller-than-result, and larger-than-result record limits;
- Preview's 100-record maximum and smaller user limit;
- column selection and output order;
- header inclusion for Excel and CSV;
- CSV comma and semicolon delimiters;
- both deterministic ordering directions;
- empty results for every format;
- direct endpoint authentication, authorization, and tamper resistance;
- concurrency rejection and retry behavior;
- large seeded exports with bounded memory and no silent truncation; and
- files opening successfully in representative Excel, CSV, and XML consumers.

## Acceptance criteria

- The selected form's Submissions page contains one **Export** header action.
- **Export** opens an Xperience-styled menu containing CSV first, followed by Excel,
  XML, and Advanced export actions, without replacing the header button.
- Each of **Export**'s quick items is labeled with an unconditional **Page**
  qualifier ("Export Page to CSV," and so on) so it cannot be confused with
  **Export selected**, regardless of whether a selection is active.
- The action does not include Reset view.
- Quick exports contain only the current listing page's records and displayed data
  columns, in the current displayed order.
- An **Export selected** mass action appears using Xperience's native selection UI
  once at least one row is checked. Selecting it opens a format-choice dialog
  (CSV, Excel, or XML); confirming it exports exactly the checked rows in that
  format, independently of the **Export** header action.
- Advanced export provides format, date range, record maximum, safe ordering,
  conditional header/delimiter controls, column selection, Preview, and Export.
- Preview produces a real file containing no more than 100 data records.
- All formats contain only the selected form's authorized submissions and selected
  columns in deterministic order.
- Large exports use bounded processing and are not silently truncated.
- Uploaded files are represented only by filename.
- UI visibility and direct downloads enforce the export permission.
- Exported data is neither persisted nor logged.
- Automated and Dancing Goat runtime tests cover the critical workflows and
  security boundary.
- Production code remains in the toolkit package.

## Out of scope

- Exporting several forms in one file.
- Exporting uploaded-file contents.
- Raw database export.
- User-entered SQL `WHERE` or `ORDER BY` expressions.
- Arbitrary column reordering.
- Background/scheduled exports, email delivery, or persisted export history.
- Importing, deleting, or modifying submissions.
- Building any custom row-selection UI. **Export selected** relies entirely on
  Xperience's native mass-action selection mechanism.
- Advanced-export-style options (date range, record limit, ordering, header
  toggle, delimiter choice) or column customization for **Export selected**.
  Its dialog offers a format choice only; administrators who need finer
  control can use Advanced export instead.

## References

- [Kentico Xperience 13 advanced export](https://docs.kentico.com/13/managing-website-content/exporting-data-from-the-user-interface/exporting-data-from-the-ui-advanced-export)
- [Kentico Xperience 13 UI data export](https://docs.kentico.com/13/managing-website-content/exporting-data-from-the-user-interface)
- [Manage form submissions](https://docs.kentico.com/documentation/business-users/digital-marketing/forms/manage-form-submissions.html)
- [UI page extenders](https://docs.kentico.com/documentation/developers-and-admins/customization/extend-the-administration-interface/ui-pages/ui-page-extenders.html)
- [Listing UI page template](https://docs.kentico.com/documentation/developers-and-admins/customization/extend-the-administration-interface/ui-pages/reference-ui-page-templates/listing-ui-page-template.html)
- [Form data API](https://docs.kentico.com/api/digital-marketing/form-data.html)
- [Secure custom endpoints](https://docs.kentico.com/documentation/developers-and-admins/customization/secure-custom-endpoints.html)
- [UI page permission checks](https://docs.kentico.com/documentation/developers-and-admins/customization/extend-the-administration-interface/ui-pages/ui-page-permission-checks.html)

Before implementation, inspect the public API metadata for the exact installed
Xperience package version instead of copying signatures from Kentico 13 or another
Xperience by Kentico version.
