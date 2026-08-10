# Feature specification: Form cloning

Status: Implemented and runtime smoke-tested on Xperience by Kentico `31.7.2`
Repository: `Simplea/xperience-community-forms-toolkit`  
Supported baseline: Xperience by Kentico `30.11.0` or newer; build-verified
through `31.7.2`; .NET 8
Research date: 2026-08-07

## Context

Xperience Community Forms Toolkit is an independently owned integration maintained
by Simplea for Xperience by Kentico. It is a reusable library, not an Xperience
website and not a Kentico-owned product. The Dancing Goat project under `examples/`
is the integration host used to run and verify the library.

The feature adds a **Clone form** row action to the existing Forms listing at:

`Digital marketing -> Forms`

Editors currently need to create a new form and reproduce its layout, fields,
validation, visibility, contact mappings, and settings manually. Kentico Xperience
13 exposed **Clone** from each form's row action menu, as shown in the supplied K13
screenshot. This feature adapts that small workflow to Xperience by Kentico's React
administration UI and supported extension points.

## Research findings

As of the research date, the current Xperience by Kentico documentation describes
the following form capabilities:

- create a new global form and edit it in Form Builder;
- compose sections and fields, including component properties, ordering, required
  state, validation rules, default values, and conditional visibility;
- map fields to contact attributes and configure smart fields;
- configure general form behavior, submission activity logging, submit-button text,
  and a basic autoresponder;
- associate separate automation processes and form-submission notifications;
- inspect submissions and where a form is used; and
- synchronize form definitions between Xperience instances, with documented
  exclusions.

The official create-and-edit workflow does not document a form clone or duplicate
action. The Xperience changelog documents cloning for pages, reusable content items,
and headless items, but not forms. This specification therefore treats form cloning
as a toolkit extension rather than a wrapper around an existing product feature.

The proposed minimal experience is:

- a **Clone form** row action on the Forms listing;
- a dialog containing one required **Form name** field, prefilled with
  `<original name> (copy)`;
- enforcement of the Forms application's Create permission;
- automatic generation of a unique code name and database table;
- copying of the data-class field definition, contact mapping, Form Builder layout,
  and supported `BizFormInfo` settings; and
- no copying of form submissions.

Xperience exposes a public API for creating an autoresponder process, but does not
expose a supported public API for reading an existing form's autoresponder
configuration. Reading that configuration requires types from `.Internal`
namespaces. Autoresponder cloning is therefore deliberately excluded. The toolkit
must not take a runtime dependency on undocumented internal types to add it.

## Goal

Let an authorized administrator create an independent form from an existing form
without manually rebuilding it.

The clone must preserve the source form's reusable design and form-level behavior,
receive new system identity and storage, contain no submitted data, and leave all
existing uses and integrations of the source form unchanged.

## Entry point and interaction

Add one row action named **Clone form** to each item in the existing Forms listing.
Use Xperience by Kentico's native listing action, dialog, icon, validation, toast,
and loading conventions. A copy/document-copy icon is appropriate. Do not reproduce
Kentico 13's black action-menu styling.

Selecting **Clone form** opens a small dialog without navigating away from the
listing:

```text
Clone form                                             [Close]

Form name
[ Original form name (copy)                         ]

[validation or safe server error, when present]

                                      [Cancel] [Clone]
```

- **Form name** is prefilled with `<source display name> (copy)` and selected for
  convenient replacement.
- The administrator may edit the name before cloning.
- **Cancel**, the close control, and Escape dismiss the dialog before a request is
  submitted.
- **Clone** is the primary action.
- While cloning is in progress, all dialog actions and dismissal controls are
  disabled and the primary action indicates progress.
- Prevent duplicate submissions caused by repeated clicks.
- On success, close the dialog, refresh the listing, and show **Form cloned
  successfully.** The administrator remains on the Forms listing.
- On validation or cloning failure, keep the dialog open and show a safe message.

The action is unavailable when the toolkit feature is disabled. For a user who can
view Forms but lacks the Forms application's Create permission, render the action
disabled when that matches the native listing convention; otherwise omit it. The
server command independently enforces the permission in every case.

## Name and identity

- Trim leading and trailing whitespace from the submitted display name.
- Require a non-empty name and apply the same maximum length and character handling
  as Xperience's normal form-creation flow.
- Do not ask the administrator for a code name, class name, table name, GUID, or
  numeric identifier.
- Generate a new form GUID, form ID, data-class identity, code name, class name,
  database table name, and primary-key identity through Xperience's supported form
  creation behavior.
- The generated identifiers and table name must be unique under concurrent clone
  operations. Do not implement uniqueness with a client-side existence check.
- Display names do not need to be unique if Xperience's normal form-creation flow
  permits duplicates. System identifiers always remain unique.
- Preserve the source's business field names. Remap only system identity fields,
  including the physical primary key, when the newly created form requires a
  different name.
- Because forms are global objects in the supported Xperience versions, do not add
  a website-channel or site selector.

## Clone contract

The clone is a new, independent form. Later edits to either form must not affect the
other.

### Copied form definition

Copy the complete form design in its configured order:

- sections, zones, section types, and section property values;
- field placement and ordering;
- form component identifiers and component property values;
- field names, captions, data types, size and precision metadata, default values,
  required state, and other supported field settings;
- validation rules and their configured properties and messages;
- visible, hidden, and conditional visibility configuration, including references
  between cloned fields;
- smart-field configuration;
- contact-attribute mappings; and
- the submit-button text and supported submit-button presentation settings.

All cross-field references in validation, visibility, or component properties must
resolve to fields in the cloned form. A clone that silently drops or redirects such
references is a failure.

Custom Form Builder sections, components, data types, and validation rules are
copied by their registered identifiers and serialized configuration. Cloning does
not package or install their implementation. If a component used by the source is
no longer registered, either preserve its configuration exactly when Xperience can
do so safely or reject the clone with a safe message; never substitute a different
component.

### Copied form settings

Copy form-owned settings that affect how the same design behaves, including all
supported equivalents of:

- submission activity logging;
- form access or presentation settings;
- reporting/display field configuration;
- contact-data overwrite behavior and other current general form properties;
- the Form Builder layout.

Settings that reference a cloned field must be validated after the new field
definition exists.

### Deliberately not copied

Do not copy or reassign:

- form submissions or their system metadata;
- files uploaded as part of form submissions;
- the source form's database rows or primary-key values;
- form usage references from pages, widgets, or custom code;
- **Used in** results;
- basic autoresponder configuration, including selected emails and custom
  autoresponder settings;
- form-submission notification objects or their recipient lists;
- advanced automation processes, running automation instances, or statistics;
- contact activities, contact-group membership, campaign associations, analytics,
  or conversion history;
- content-sync history or staging state; or
- generated code files in the consuming application.

The clone therefore begins with zero submissions and no page usage. Its basic
autoresponder is disabled. Existing pages, autoresponders, notifications, automation
triggers, and integrations continue to reference only the source form. This avoids
making a newly created clone live or causing email and automation side effects
without an administrator's separate, intentional action.

## Administration integration

Use a page extender for the existing Xperience Forms listing. Add the row action
through the listing page's supported table-action extension API and invoke a page
command for validation and cloning.

Prefer a supported command-confirmation form for the one-field dialog. A packaged
custom React component is acceptable only if the public listing APIs in a supported
version cannot provide the required prefilled model and progress behavior. Do not
reuse an internal content-item clone component.

Keep production server and client code in the distributable toolkit package.
Dancing Goat only consumes and hosts the packaged feature. Register the cloning
services through the repository's existing public `AddFormsToolkit()` startup
extension. Provide an option that enables cloning by default and allows a consuming
application to disable only this feature without disabling submission export.

## Server workflow and consistency

For each accepted command:

1. Re-resolve the source form by the row's numeric form ID.
2. Recheck the authenticated administrator's Forms Create permission.
3. Validate and normalize the requested display name.
4. Read a consistent snapshot of the source definition and copied settings.
5. Create the new form and its independent data class and table through supported
   Xperience services.
6. Apply the copied schema, layout, mappings, and supported settings.
7. Re-read and validate the completed clone before returning success.

The operation must be atomic from an administrator's perspective. If any required
step fails, remove any newly created form, data class, table, or other clone-owned
artifact using supported cleanup APIs. If the platform offers a transaction that
safely covers its schema mutation, use it. Never leave a form that appears
successful but is missing fields or required settings.

Do not issue hand-written `CREATE TABLE`, `ALTER TABLE`, or `DROP TABLE` statements.
Do not clone by copying submission-table rows. Avoid mutating the source form's
in-memory field definition while remapping the primary key; work on a detached copy.

Concurrent clones of the same source are allowed. Each receives independent unique
identity. Serialize or retry only the platform operation that generates identifiers
when required; do not lock the source form for the duration of the request.

## Security and privacy

- Require an authenticated Xperience administration user.
- Require the Forms application's Create permission for both action availability
  and command execution.
- Require the normal permission that allows the user to view/access the source form.
- Apply Xperience's normal antiforgery validation to the page command.
- Treat the source form ID and submitted name as untrusted input.
- Resolve the source by ID; do not accept a class name, table name, field definition,
  serialized layout, or arbitrary database identifier from the browser.
- Do not return or log form submissions, default values that may contain sensitive
  information, custom component configuration values, or raw serialized form
  definitions.
- Safe operational logs may include the source and clone form IDs, authenticated
  user ID, duration, and success/failure state.
- Escape the display name wherever it is rendered in the administration UI.
- Respect Xperience read-only deployment mode and surface its standard safe error.

## Compatibility and API gate

Before implementation, inspect the public API metadata and runtime behavior for
both the minimum supported Xperience version and the latest build-verified version.
Record the exact supported APIs used for:

- creating a form, data class, table, and unique identifiers;
- reading and applying the complete Form Builder definition;
- copying contact mappings and form-owned settings;
- adding a prefilled row-action dialog; and
- cleaning up a partially created form.

Production code must not reference namespaces or types marked `.Internal`, use
reflection to access non-public members, or copy internal React components from
Xperience packages. The basic autoresponder is not part of the required clone
contract because Xperience does not expose a supported public API for reading its
source configuration. Do not silently ship a version-specific internal API branch
to copy it. If any remaining required clone behavior cannot be implemented using
supported APIs, revise the scope explicitly before implementation.

## Error behavior

- Unknown or deleted source form: safe not-found administration error; create
  nothing.
- Invalid or empty name: field-level validation error.
- Unauthenticated request: normal platform authentication behavior.
- Authenticated but unauthorized request: normal forbidden administration response;
  create nothing.
- Unsupported or invalid source definition: safe error naming the unsupported
  component or category when this does not expose sensitive configuration.
- Identifier collision: let the platform regenerate/retry within a small bound;
  otherwise fail safely and clean up.
- Failure after creation starts: clean up clone-owned artifacts, log safe diagnostic
  metadata, and do not report success.
- If cleanup itself fails, log the orphan identifiers at error severity so an
  administrator can repair them; do not include serialized definitions or data.

## Suggested component boundaries

- `FormListPageExtender`: permission-gates and adds the **Clone form** row action.
- Clone-dialog model: required display name and platform-compatible validation.
- Clone page command: authenticates, authorizes, validates input, prevents duplicate
  submission, and returns the administration response.
- Form clone service: snapshots the source, creates independent identity and
  storage, coordinates copy steps, validates the result, and cleans up on failure.
- Form definition copier: clones fields, sections, component configuration,
  cross-field references, validation, visibility, smart fields, and contact mapping.
- Form settings copier: clones supported `BizFormInfo` behavior while explicitly
  leaving autoresponder configuration disabled on the clone.

Keep Xperience-specific object mutation behind the clone service so compatibility
tests can detect API changes without coupling the page extender to persistence
details.

## Test strategy

### Unit and component tests

- default display name and submitted-name trimming/validation;
- clone option enabled and disabled behavior;
- permission-gated action and command behavior;
- source-not-found handling;
- field and section ordering;
- component and section property preservation;
- required, default, validation, visibility, and smart-field configuration;
- cross-field reference preservation;
- contact mappings and general form settings;
- explicit exclusion of basic autoresponder settings;
- new system identifiers and preserved business field names;
- explicit exclusion of submissions, usage references, notifications, and advanced
  automation;
- duplicate-click protection and concurrent clone identity uniqueness; and
- cleanup of failures injected after every persistent creation step.

### Integration and runtime tests

Run against both the minimum supported Xperience version and latest verified
version:

- action placement and native administration styling on the Forms listing;
- authorized, view-only, and unauthenticated users;
- prefilling, cancellation, validation, progress, success toast, and list refresh;
- cloning an empty form;
- cloning every built-in field and section type used by Dancing Goat;
- cloning custom components, configurable sections, validation rules, conditional
  visibility, smart fields, and contact mappings;
- cloning a form with a configured basic autoresponder and verifying that the
  source remains configured while the clone's autoresponder is disabled;
- verifying that advanced automation and notification relationships are unchanged;
- verifying zero submissions and zero usage references on the clone;
- submitting the source and clone independently and verifying separate tables;
- editing and deleting fields on one form without changing the other;
- repeated and concurrent clone operations;
- missing registered custom component behavior;
- read-only deployment behavior;
- failure rollback without orphan form/data-class/table records; and
- normal Continuous Integration/Content sync tracking of the newly created form,
  where supported by Xperience.

## Acceptance criteria

- Each form in the Forms listing has a native **Clone form** row action when the
  feature is enabled and the listing convention permits the current user to use it.
- The action opens a dialog with **Form name** prefilled as
  `<source display name> (copy)`.
- A user with the Forms Create permission can clone the form without leaving the
  listing; an unauthorized user cannot invoke the operation directly.
- The clone has new unique system identity, data class, primary key, and database
  table.
- The clone preserves the complete supported form layout, fields, component
  settings, validation, visibility, smart-field behavior, contact mappings,
  and supported form-owned settings.
- The clone contains no submissions and does not inherit page usage, notifications,
  autoresponders, advanced automation, analytics, or other runtime relationships.
- Source and clone can be edited, submitted, and deleted independently.
- A partial failure does not leave a visible incomplete clone or untracked storage
  artifacts.
- The implementation passes compatibility validation on the minimum and latest
  verified Xperience versions without depending on internal Xperience APIs.
- Production code remains in the toolkit package.

## Out of scope

- Copying submissions or uploaded submission files.
- Repointing existing pages or form widgets to the clone.
- Copying basic autoresponder configuration. Reconsider only when Xperience exposes
  a supported public API for reading the source configuration.
- Cloning or editing notification recipient lists.
- Cloning advanced automation processes or active automation state.
- Cloning referenced email content, consents, contact attributes, or custom
  component implementations.
- Cloning several forms in one operation or bulk cloning.
- Cross-instance transfer; use Xperience Content sync for that scenario.
- Supplying custom code names, class names, table names, or raw database schema.
- A public headless/REST clone endpoint.

## References

- [Xperience by Kentico Forms overview](https://docs.kentico.com/documentation/business-users/digital-marketing/forms)
- [Create and edit forms](https://docs.kentico.com/documentation/business-users/digital-marketing/forms/create-and-edit-forms)
- [Form Builder](https://docs.kentico.com/documentation/developers-and-admins/development/builders/form-builder)
- [Manage form submissions](https://docs.kentico.com/documentation/business-users/digital-marketing/forms/manage-form-submissions.html)
- [Form submission notifications](https://docs.kentico.com/documentation/developers-and-admins/configuration/notifications)
- [Automation](https://docs.kentico.com/documentation/business-users/digital-marketing/automation)
- [Listing UI page template](https://docs.kentico.com/documentation/developers-and-admins/customization/extend-the-administration-interface/ui-pages/reference-ui-page-templates/listing-ui-page-template.html)
- [UI page extenders](https://docs.kentico.com/documentation/developers-and-admins/customization/extend-the-administration-interface/ui-pages/ui-page-extenders.html)
- [Xperience by Kentico changelog](https://docs.kentico.com/changelog)
- [Kentico Xperience 13 cloning objects](https://docs.kentico.com/13/custom-development/cloning-objects-through-the-api)
- [.NET API compatibility rules](https://learn.microsoft.com/en-us/dotnet/core/compatibility/library-change-rules)
