# Usage Guide

## Requirements

- Xperience by Kentico 31.2.1. Later 31.x releases may work but are not yet part
  of the verified compatibility baseline.
- ASP.NET Core 8.0.
- The administration application must be included in the deployment that hosts
  the export endpoint.

## Installation

Install the toolkit package in the Xperience application:

```powershell
dotnet add package XperienceCommunity.FormsToolkit --version 1.0.0-beta.1
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
});
```

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

## Assign the export permission

The toolkit adds the **Export form submissions** permission to the existing Forms
administration application. In **Role management**, edit the relevant role and
grant this permission under the Forms application.

The stable permission identifier is:

```text
XperienceCommunity.FormsToolkit.ExportSubmissions
```

Users also need the normal Xperience permissions required to open the Forms
application and its Submissions tab. The export action is absent when the toolkit
permission is not granted. The download endpoint additionally requires an
authenticated Xperience administration session. Advanced exports use a short-lived,
user-bound token issued by the permission-checked page command. Quick current-view
exports use an antiforgery-protected POST request and independently recheck the
toolkit permission before reading form data.

## Export form submissions

1. Open **Forms** in the Xperience administration.
2. Select a form and open **Submissions**.
3. Select **Export** in the page header.
4. Select **Export to CSV**, **Export to Excel**, or **Export to XML** from the
   dropdown. CSV appears first as the recommended quick format. Use **Advanced
   export** for additional options.

Quick exports contain the records and data columns shown on the current listing
page, in their current displayed order. They honor the current search, column
sorting, paging, and page size.

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
Quick exports include only the current grid page. Uploaded files are represented by
filename only.

Exports are generated incrementally from bounded database batches and downloaded
through the authenticated response. The toolkit does not create completed export
files in application or web-accessible storage.

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
