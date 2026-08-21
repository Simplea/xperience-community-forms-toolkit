# Usage Guide

## Requirements

- Xperience by Kentico 30.11.0 or newer. The package is build-verified through
  31.7.2 and runtime smoke-tested on 31.1.2. See the
  [compatibility policy](./Compatibility.md).
- ASP.NET Core 8.0.
- The administration application must be included in the deployment that hosts
  the export endpoint.

## Installation

Install the toolkit package in the Xperience application:

```powershell
dotnet add package XperienceCommunity.FormsToolkit --version 1.0.0-beta.4
```

Register the toolkit after registering Xperience services:

```csharp
using XperienceCommunity.FormsToolkit;

builder.Services.AddKentico(...);
builder.Services.AddFormsToolkit();
```

By default, each application instance permits two simultaneous exports and one
active export per administrator. The application-wide limit can be adjusted:

```csharp
builder.Services.AddFormsToolkit(options =>
{
    options.MaximumConcurrentExports = 3;
    options.EnableFormCloning = true;
});
```

Form cloning is enabled by default. Set `EnableFormCloning` to `false` to remove
the row action without disabling submission exports.

Map attribute-routed controllers in the request pipeline:

```csharp
app.UseAuthentication();
app.UseKentico();
app.UseAuthorization();

app.MapControllers();
```

The package embeds and registers its administration client module. Consuming
applications do not need to copy JavaScript files or run a separate client build.
If the application explicitly configures `CMSAdminClientModuleSettings`, keep the
`xperience-community-forms-toolkit` module in `Embedded` mode.

## Assign the export and delete permissions

The toolkit adds the **Export form submissions** and **Delete form submissions**
permissions to the existing Forms administration application. In **Role
management**, edit the relevant role and grant the permissions it needs under the
Forms application. They are independent: a role can export without being able to
delete, or the reverse.

The stable permission identifiers are:

```text
XperienceCommunity.FormsToolkit.ExportSubmissions
XperienceCommunity.FormsToolkit.DeleteSubmissions
```

Users also need the normal Xperience permissions required to open the Forms
application and its Submissions tab. Each toolkit action is absent when its
permission is not granted, and is hidden entirely for a form with no submissions
regardless of permission. The export download endpoint additionally requires an
authenticated Xperience administration session. Advanced exports and Advanced
deletes use a short-lived, user-bound token or a permission-checked page command;
**Export selected** uses an antiforgery-protected POST request and independently
rechecks the export permission before reading form data.

## Export form submissions

1. Open **Forms** in the Xperience administration.
2. Select a form and open **Submissions**.
3. Select one or more rows using the listing's native selection checkboxes
   (including "select all" in the table header to cover the whole page), then
   select **Export** in the toolbar that appears below the search bar; or
4. Select **Advanced export** in the page header for filter-based options instead
   of a row selection.

**Export** opens a small dialog to choose **CSV**, **Excel**, or **XML**, then
exports exactly the checked rows and their currently displayed columns, in their
displayed order.

Select **Advanced export** to configure:

- Excel, CSV, or XML output;
- inclusive **From** and **To** dates, interpreted in the administration user's
  detected time zone;
- an optional maximum number of records;
- oldest-first or newest-first submission ordering;
- CSV comma or semicolon delimiter;
- Excel/CSV column headers; and
- exported columns.

**Preview** downloads a real file in the selected format containing at most 100
matching records. A smaller configured record maximum is honored.

Advanced exports with empty dates and no record maximum include all submissions.
**Export** only includes the rows you checked. Uploaded files are represented by
filename only.

Exports are generated incrementally from bounded database batches and downloaded
through the authenticated response. The toolkit does not create completed export
files in application or web-accessible storage.

## Delete form submissions

1. Open **Forms** in the Xperience administration.
2. Select a form and open **Submissions**.
3. Select one or more rows using the listing's native selection checkboxes, then
   select **Delete** in the toolbar that appears below the search bar; or
4. Select **Advanced delete** in the page header to remove submissions by date
   range and/or record limit instead of a row selection.

**Delete** removes exactly the checked rows after a confirmation prompt. This is
permanent; there is no undo or recycle bin.

Select **Advanced delete** to configure the same date range, record limit, and
ordering options as Advanced export, then:

1. Select **Preview matching count** to see how many submissions would be removed.
   This is required before deletion is allowed, and is invalidated by any further
   filter change.
2. Optionally select **Export these first** to capture a copy via Advanced export,
   prefilled with the same date range, before deleting.
3. Type `DELETE` to confirm, then select **Delete**.

Both delete paths also remove each deleted submission's uploaded files. Deleting a
submission never removes the form definition, its data class, or another form's
data.

**Advanced export** and **Advanced delete** are both hidden for a form with no
submissions; there is nothing to act on until the form receives its first one.

## Clone a form

1. Open **Forms** in the Xperience administration.
2. Open a form row's actions and select **Clone form**.
3. Review or change the prefilled form name, then select **Clone**.

The user must have Xperience's Forms Create permission. The clone receives its own
code name, data class, database table, and primary key. It copies the supported
form definition, Form Builder layout, contact mapping, general form settings, and
authorized-role access.

The clone starts with zero submissions. Autoresponders, automation processes,
notifications, page/widget usage references, and analytics relationships remain
attached only to the source form. The toolkit intentionally uses only public
Xperience APIs; autoresponder cloning will remain unavailable until Kentico exposes
a supported public API for reading that configuration.

## Client development

Only contributors modifying the administration dialog need to build the embedded
client module:

```powershell
cd src/XperienceCommunity.FormsToolkit/Client
npm install
npm run build
```

Commit the generated `Client/dist` files so normal .NET builds and package consumers
do not require Node.js.
