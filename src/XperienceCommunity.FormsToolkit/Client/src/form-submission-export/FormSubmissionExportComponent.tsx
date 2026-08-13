import React, { useMemo, useRef, useState } from "react";
import * as XperienceAdminBase from "@kentico/xperience-admin-base";
import { ActionComponentProps, usePageCommand } from "@kentico/xperience-admin-base";
import {
  Button,
  ButtonColor,
  Checkbox,
  DateTimeInput,
  Dialog,
  DropDownActionMenu,
  DropDownPlacement,
  Input,
  MenuItem,
  NotificationBarAlert,
  Select,
  SnackbarItemVariant,
  useSnackbar,
} from "@kentico/xperience-admin-components";

export type ExportFormat = "excel" | "csv" | "xml";
type ExportOperation = "export" | "preview";
type ExportOrder = "ascending" | "descending";

interface ExportField {
  readonly identifier: string;
  readonly sourceName: string;
  readonly caption: string;
  readonly visibleInListing: boolean;
}

interface ComponentData {
  readonly commandName: string;
  readonly currentViewDownloadUrl: string;
  readonly fields: readonly ExportField[];
}

type FormSubmissionExportComponentProps = Omit<
  ActionComponentProps,
  "componentData"
> & ComponentData;

interface ExportRequest {
  readonly format: ExportFormat;
  readonly operation: ExportOperation;
  readonly from: string | null;
  readonly to: string | null;
  readonly timeZone: string;
  readonly numberOfRecords: string | null;
  readonly includeHeader: boolean;
  readonly delimiter: "comma" | "semicolon";
  readonly order: ExportOrder;
  readonly columns: readonly string[] | null;
}

interface CurrentViewRequest {
  readonly format: ExportFormat;
  readonly submissionIds: readonly number[];
  readonly columns: readonly string[];
}

interface ExportResponse {
  readonly downloadUrl?: string;
  readonly error?: string;
}

interface AntiForgeryContextValue {
  readonly getXsrfHeader: () => Record<string, string>;
  readonly refreshToken: () => Promise<void>;
}

export const useXperienceAntiForgery = (
  XperienceAdminBase as typeof XperienceAdminBase & {
    useAntiForgery: () => AntiForgeryContextValue;
  }
).useAntiForgery;

const formatLabels: Record<ExportFormat, string> = {
  excel: "Excel",
  csv: "CSV",
  xml: "XML",
};

const formatExtensions: Record<ExportFormat, string> = {
  excel: "xlsx",
  csv: "csv",
  xml: "xml",
};

const toDateOnly = (value: Date | null): string | null => {
  if (!value) {
    return null;
  }

  const year = value.getFullYear();
  const month = String(value.getMonth() + 1).padStart(2, "0");
  const day = String(value.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
};

const startDownload = (url: string) => {
  const link = document.createElement("a");
  link.href = url;
  link.style.display = "none";
  document.body.appendChild(link);
  link.click();
  link.remove();
};

export const getAntiforgeryHeaders = (xsrfHeaders: Record<string, string>): Record<string, string> => {
  if (!Object.entries(xsrfHeaders).some(([name, value]) => name.length > 0 && value.length > 0)) {
    throw new Error("The administration security token is unavailable. Refresh the page and try again.");
  }

  return { "Content-Type": "application/json", ...xsrfHeaders };
};

const getResponseFileName = (response: Response, format: ExportFormat): string => {
  const disposition = response.headers.get("Content-Disposition") ?? "";
  const encoded = /filename\*=UTF-8''([^;]+)/i.exec(disposition)?.[1];
  if (encoded) {
    return decodeURIComponent(encoded.replace(/^"|"$/g, ""));
  }

  const regular = /filename="?([^";]+)"?/i.exec(disposition)?.[1];
  return regular ?? `form-submissions.${formatExtensions[format]}`;
};

export const downloadResponse = async (response: Response, format: ExportFormat) => {
  const blob = await response.blob();
  const objectUrl = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = objectUrl;
  link.download = getResponseFileName(response, format);
  link.style.display = "none";
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.setTimeout(() => URL.revokeObjectURL(objectUrl), 0);
};

export const readProblemDetail = async (response: Response): Promise<string> => {
  try {
    const problem = await response.json() as { detail?: string };
    return problem.detail ?? "The export could not be prepared.";
  } catch {
    return "The export could not be prepared.";
  }
};

export const extractSubmissionId = (row: HTMLElement): number | null => {
  const links = Array.from(row.querySelectorAll<HTMLAnchorElement>("a[href]"));
  for (const link of links) {
    const segments = new URL(link.href, window.location.href).pathname.split("/").filter(Boolean);
    const lastNumericSegment = [...segments].reverse().find((segment) => /^\d+$/.test(segment));
    if (lastNumericSegment) {
      const identifier = Number(lastNumericSegment);
      if (Number.isSafeInteger(identifier) && identifier > 0) {
        return identifier;
      }
    }
  }

  return null;
};

const captureCurrentView = (fields: readonly ExportField[]): Omit<CurrentViewRequest, "format"> => {
  const table = document.querySelector<HTMLElement>('[data-testid="table"][role="table"]');
  if (!table) {
    throw new Error("The current submissions view could not be read. Refresh the page and try again.");
  }

  const rows = Array.from(table.querySelectorAll<HTMLElement>('[data-testid="table-row"][role="row"]'));
  const fieldsBySourceName = new Map(fields.map((field) => [field.sourceName.toLocaleLowerCase(), field]));

  const visibleSourceNames = rows.length > 0
    ? Array.from(rows[0].querySelectorAll<HTMLElement>('[role="cell"][data-testid^="table-cell-"]'))
      .map((cell) => cell.dataset.testid?.slice("table-cell-".length) ?? "")
    : fields.filter((field) => field.visibleInListing).map((field) => field.sourceName);

  const columns = visibleSourceNames
    .map((sourceName) => fieldsBySourceName.get(sourceName.toLocaleLowerCase())?.identifier)
    .filter((identifier): identifier is string => Boolean(identifier));
  if (columns.length === 0 || new Set(columns).size !== columns.length) {
    throw new Error("The current submissions columns could not be read. Refresh the page and try again.");
  }

  const submissionIds = rows.map(extractSubmissionId);
  if (submissionIds.some((identifier) => identifier === null)) {
    throw new Error("The current submissions could not be identified. Refresh the page and try again.");
  }

  const resolvedIds = submissionIds as number[];
  if (new Set(resolvedIds).size !== resolvedIds.length) {
    throw new Error("The current submissions view is invalid. Refresh the page and try again.");
  }

  return { submissionIds: resolvedIds, columns };
};

const getTriggerRect = (): DOMRect => {
  if (document.activeElement instanceof HTMLElement) {
    const rect = document.activeElement.getBoundingClientRect();
    if (rect.width > 0 && rect.height > 0) {
      return rect;
    }
  }

  return new DOMRect(window.innerWidth - 180, 72, 160, 1);
};

export const FormSubmissionExportComponent = ({
  commandName,
  currentViewDownloadUrl,
  fields,
  unloadComponent,
}: FormSubmissionExportComponentProps) => {
  const [advanced, setAdvanced] = useState(false);
  const [format, setFormat] = useState<ExportFormat>("excel");
  const [from, setFrom] = useState<Date | null>(null);
  const [to, setTo] = useState<Date | null>(null);
  const [numberOfRecords, setNumberOfRecords] = useState("");
  const [includeHeader, setIncludeHeader] = useState(true);
  const [delimiter, setDelimiter] = useState<"comma" | "semicolon">("comma");
  const [order, setOrder] = useState<ExportOrder>("ascending");
  const [selectedColumns, setSelectedColumns] = useState<Set<string>>(
    () => new Set(fields.map((field) => field.identifier)),
  );
  const [inProgress, setInProgress] = useState(false);
  const [pendingLabel, setPendingLabel] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const pendingOperation = useRef<ExportOperation>("export");
  const triggerRect = useRef(getTriggerRect());
  const menuHasOpened = useRef(false);
  const { addMessage } = useSnackbar();
  const { getXsrfHeader } = useXperienceAntiForgery();

  const orderedSelectedColumns = useMemo(
    () => fields.filter((field) => selectedColumns.has(field.identifier)).map((field) => field.identifier),
    [fields, selectedColumns],
  );

  const { execute } = usePageCommand<ExportResponse, ExportRequest>(
    commandName,
    {
      after: (response) => {
        setInProgress(false);
        setPendingLabel(null);
        if (response?.error) {
          setError(response.error);
          return;
        }

        if (response?.downloadUrl) {
          startDownload(response.downloadUrl);
          if (pendingOperation.current === "export") {
            unloadComponent();
          }
        }
      },
    },
  );

  const notifyQuickExportError = (message: string) => {
    addMessage({ message, variant: SnackbarItemVariant.Error, autoHide: true });
  };

  const quickExport = async (requestedFormat: ExportFormat) => {
    const label = `Export Page to ${formatLabels[requestedFormat]}`;
    setPendingLabel(label);
    setInProgress(true);

    try {
      const currentView = captureCurrentView(fields);
      const response = await fetch(currentViewDownloadUrl, {
        method: "POST",
        credentials: "same-origin",
        headers: getAntiforgeryHeaders(getXsrfHeader()),
        body: JSON.stringify({ format: requestedFormat, ...currentView } satisfies CurrentViewRequest),
      });
      if (!response.ok) {
        throw new Error(await readProblemDetail(response));
      }

      await downloadResponse(response, requestedFormat);
      unloadComponent();
    } catch (caught) {
      setInProgress(false);
      setPendingLabel(null);
      notifyQuickExportError(caught instanceof Error ? caught.message : "The export could not be prepared.");
    }
  };

  const validateAdvanced = (): boolean => {
    if (from && to && from.getTime() > to.getTime()) {
      setError("From must be on or before To.");
      return false;
    }

    if (numberOfRecords.length > 0 && !/^[1-9]\d*$/.test(numberOfRecords)) {
      setError("Number of records must be a positive whole number.");
      return false;
    }

    if (selectedColumns.size === 0) {
      setError("Select at least one column to export.");
      return false;
    }

    return true;
  };

  const submitAdvanced = async (operation: ExportOperation) => {
    setError(null);
    if (!validateAdvanced()) {
      return;
    }

    pendingOperation.current = operation;
    setPendingLabel(operation === "preview" ? "Preview" : `Export to ${formatLabels[format]}`);
    setInProgress(true);

    try {
      await execute({
        format,
        operation,
        from: toDateOnly(from),
        to: toDateOnly(to),
        timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone,
        numberOfRecords: numberOfRecords || null,
        includeHeader,
        delimiter,
        order,
        columns: orderedSelectedColumns,
      });
    } catch {
      setInProgress(false);
      setPendingLabel(null);
      setError("The export could not be prepared.");
    }
  };

  const toggleColumn = (identifier: string, selected: boolean) => {
    setSelectedColumns((current) => {
      const next = new Set(current);
      if (selected) {
        next.add(identifier);
      } else {
        next.delete(identifier);
      }
      return next;
    });
  };

  if (!advanced) {
    const rect = triggerRect.current;
    return (
      <DropDownActionMenu
        open
        placement={DropDownPlacement.BottomStart}
        onToggle={(isOpen) => {
          if (isOpen) {
            menuHasOpened.current = true;
          } else if (menuHasOpened.current && !inProgress) {
            unloadComponent();
          }
        }}
        renderTrigger={(ref) => (
          <span
            ref={ref as React.RefObject<HTMLSpanElement>}
            aria-hidden
            style={{
              position: "fixed",
              left: `${rect.left}px`,
              top: `${rect.bottom}px`,
              width: `${Math.max(rect.width, 1)}px`,
              height: "1px",
              pointerEvents: "none",
            }}
          />
        )}
      >
        {(["csv", "excel", "xml"] as const).map((quickFormat) => {
          const label = `Export Page to ${formatLabels[quickFormat]}`;
          return (
            <MenuItem
              key={quickFormat}
              primaryLabel={label}
              secondaryLabel={inProgress && pendingLabel === label ? "Preparing..." : undefined}
              disabled={inProgress}
              onClick={() => quickExport(quickFormat)}
            />
          );
        })}
        <MenuItem
          primaryLabel="Advanced export"
          disabled={inProgress}
          onClick={() => setAdvanced(true)}
        />
      </DropDownActionMenu>
    );
  }

  const noColumnsSelected = selectedColumns.size === 0;
  return (
    <Dialog
      isOpen
      headline="Advanced export"
      onClose={unloadComponent}
      headerCloseButton={{ tooltipText: "Close" }}
      isDismissable={!inProgress}
      actionInProgress={inProgress}
      width="min(880px, calc(100vw - 48px))"
      maxHeight="calc(100vh - 48px)"
      confirmAction={{
        label: "Export",
        onClick: () => submitAdvanced("export"),
        inProgress: inProgress && pendingOperation.current === "export",
        disabled: inProgress || noColumnsSelected,
      }}
      secondaryAction={{
        label: "Preview",
        onClick: () => submitAdvanced("preview"),
        inProgress: inProgress && pendingOperation.current === "preview",
        disabled: inProgress || noColumnsSelected,
      }}
      cancelAction={{ label: "Cancel", onClick: unloadComponent, disabled: inProgress }}
      notificationBar={error ? <NotificationBarAlert>{error}</NotificationBarAlert> : undefined}
    >
      <div style={{ display: "grid", gap: "20px", padding: "4px 0" }}>
        <Select label="Export to" value={format} onChange={(value) => value && setFormat(value as ExportFormat)}>
          <MenuItem primaryLabel="Excel" value="excel" />
          <MenuItem primaryLabel="CSV" value="csv" />
          <MenuItem primaryLabel="XML" value="xml" />
        </Select>

        {format === "csv" && (
          <Select label="Delimiter" value={delimiter} onChange={(value) => value && setDelimiter(value as "comma" | "semicolon")}>
            <MenuItem primaryLabel="Comma" value="comma" />
            <MenuItem primaryLabel="Semicolon" value="semicolon" />
          </Select>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))", gap: "16px" }}>
          <DateTimeInput label={"From:\u00a0"} value={from} onChange={setFrom} showTime={false} allowClear />
          <DateTimeInput label={"To:\u00a0"} value={to} onChange={setTo} showTime={false} allowClear />
        </div>

        <Input
          label="Number of records"
          type="number"
          min={1}
          value={numberOfRecords}
          placeholder="All matching submissions"
          explanationText="Maximum submissions included. Leave empty to export all matching submissions."
          onChange={(event) => setNumberOfRecords(event.currentTarget.value)}
        />

        {format !== "xml" && (
          <Checkbox
            label="Export column header"
            checked={includeHeader}
            disabled={inProgress}
            onChange={(_, checked) => setIncludeHeader(checked)}
          />
        )}

        <Select label="Order by" value={order} onChange={(value) => value && setOrder(value as ExportOrder)}>
          <MenuItem primaryLabel="Submitted - oldest first" value="ascending" />
          <MenuItem primaryLabel="Submitted - newest first" value="descending" />
        </Select>

        <section aria-labelledby="export-columns-heading" style={{ display: "grid", gap: "12px" }}>
          <h3 id="export-columns-heading" style={{ margin: 0 }}>Columns</h3>
          <div style={{ display: "flex", flexWrap: "wrap", gap: "8px" }}>
            <Button
              label="Select all"
              color={ButtonColor.Secondary}
              disabled={inProgress}
              onClick={() => setSelectedColumns(new Set(fields.map((field) => field.identifier)))}
            />
            <Button
              label="Deselect all"
              color={ButtonColor.Secondary}
              disabled={inProgress}
              onClick={() => setSelectedColumns(new Set())}
            />
            <Button
              label="Default selection"
              color={ButtonColor.Secondary}
              disabled={inProgress}
              onClick={() => setSelectedColumns(new Set(fields.map((field) => field.identifier)))}
            />
          </div>
          <div style={{ display: "grid", gap: "8px", maxHeight: "320px", overflowY: "auto", padding: "4px" }}>
            {fields.map((field) => (
              <Checkbox
                key={field.identifier}
                label={field.caption}
                checked={selectedColumns.has(field.identifier)}
                disabled={inProgress}
                onChange={(_, checked) => toggleColumn(field.identifier, checked)}
              />
            ))}
          </div>
        </section>
      </div>
    </Dialog>
  );
};
