import { useState } from "react";
import { ActionComponentProps, usePageCommand } from "@kentico/xperience-admin-base";
import {
  Button,
  ButtonColor,
  DateTimeInput,
  Dialog,
  Input,
  MenuItem,
  NotificationBarAlert,
  Select,
  SnackbarItemVariant,
  useSnackbar,
} from "@kentico/xperience-admin-components";

import { startDownload, toDateOnly } from "../form-submission-export/FormSubmissionExportComponent";

type RemovalOrder = "ascending" | "descending";
type PendingOperation = "preview" | "delete" | "export" | null;

interface ComponentData {
  readonly previewCommandName: string;
  readonly deleteCommandName: string;
  readonly exportTokenCommandName: string;
}

type FormSubmissionRemovalComponentProps = Omit<
  ActionComponentProps,
  "componentData"
> & ComponentData;

interface RangeRequest {
  readonly from: string | null;
  readonly to: string | null;
  readonly timeZone: string;
  readonly numberOfRecords: string | null;
  readonly order: RemovalOrder;
}

interface ExportTokenRequest {
  readonly format: string;
  readonly operation: string;
  readonly from: string | null;
  readonly to: string | null;
  readonly timeZone: string;
  readonly numberOfRecords: string | null;
  readonly includeHeader: boolean;
  readonly delimiter: string;
  readonly order: RemovalOrder;
  readonly columns: readonly string[] | null;
}

interface PreviewResponse {
  readonly matchingCount?: number;
  readonly error?: string;
}

interface DeleteResponse {
  readonly deletedCount?: number;
  readonly error?: string;
}

interface ExportTokenResponse {
  readonly downloadUrl?: string;
  readonly error?: string;
}

const CONFIRMATION_PHRASE = "DELETE";

export const FormSubmissionRemovalComponent = ({
  previewCommandName,
  deleteCommandName,
  exportTokenCommandName,
  unloadComponent,
}: FormSubmissionRemovalComponentProps) => {
  const [from, setFrom] = useState<Date | null>(null);
  const [to, setTo] = useState<Date | null>(null);
  const [numberOfRecords, setNumberOfRecords] = useState("");
  const [order, setOrder] = useState<RemovalOrder>("ascending");
  const [confirmationPhrase, setConfirmationPhrase] = useState("");
  const [previewCount, setPreviewCount] = useState<number | null>(null);
  const [previewedFilters, setPreviewedFilters] = useState<string | null>(null);
  const [inProgress, setInProgress] = useState(false);
  const [pendingOperation, setPendingOperation] = useState<PendingOperation>(null);
  const [error, setError] = useState<string | null>(null);
  const { addMessage } = useSnackbar();

  const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
  const currentFiltersKey = JSON.stringify({ from: toDateOnly(from), to: toDateOnly(to), numberOfRecords, order });

  const buildRangeRequest = (): RangeRequest => ({
    from: toDateOnly(from),
    to: toDateOnly(to),
    timeZone,
    numberOfRecords: numberOfRecords || null,
    order,
  });

  const invalidatePreview = () => {
    setPreviewCount(null);
    setPreviewedFilters(null);
  };

  const { execute: executePreview } = usePageCommand<PreviewResponse, RangeRequest>(previewCommandName, {
    after: (response) => {
      setInProgress(false);
      setPendingOperation(null);
      if (response?.error) {
        setError(response.error);
        return;
      }

      if (typeof response?.matchingCount === "number") {
        setPreviewCount(response.matchingCount);
        setPreviewedFilters(currentFiltersKey);
      }
    },
  });

  const { execute: executeDelete } = usePageCommand<DeleteResponse, RangeRequest>(deleteCommandName, {
    after: (response) => {
      setInProgress(false);
      setPendingOperation(null);
      if (response?.error) {
        setError(response.error);
        return;
      }

      if (typeof response?.deletedCount === "number") {
        addMessage({
          message: `Deleted ${response.deletedCount} submission(s).`,
          variant: SnackbarItemVariant.Success,
          autoHide: true,
        });
        unloadComponent();
      }
    },
  });

  const { execute: executeExportToken } = usePageCommand<ExportTokenResponse, ExportTokenRequest>(
    exportTokenCommandName,
    {
      after: (response) => {
        setInProgress(false);
        setPendingOperation(null);
        if (response?.error) {
          setError(response.error);
          return;
        }

        if (response?.downloadUrl) {
          startDownload(response.downloadUrl);
        }
      },
    },
  );

  const validate = (): boolean => {
    if (from && to && from.getTime() > to.getTime()) {
      setError("From must be on or before To.");
      return false;
    }

    if (numberOfRecords.length > 0 && !/^[1-9]\d*$/.test(numberOfRecords)) {
      setError("Number of records must be a positive whole number.");
      return false;
    }

    return true;
  };

  const runPreview = async () => {
    setError(null);
    if (!validate()) {
      return;
    }

    setPendingOperation("preview");
    setInProgress(true);
    try {
      await executePreview(buildRangeRequest());
    } catch {
      setInProgress(false);
      setPendingOperation(null);
      setError("The matching count could not be calculated.");
    }
  };

  const runDelete = async () => {
    setError(null);
    if (!validate()) {
      return;
    }

    setPendingOperation("delete");
    setInProgress(true);
    try {
      await executeDelete(buildRangeRequest());
    } catch {
      setInProgress(false);
      setPendingOperation(null);
      setError("The submissions could not be deleted.");
    }
  };

  const runExportThese = async () => {
    setError(null);
    setPendingOperation("export");
    setInProgress(true);
    try {
      await executeExportToken({
        format: "excel",
        operation: "export",
        from: toDateOnly(from),
        to: toDateOnly(to),
        timeZone,
        numberOfRecords: null,
        includeHeader: true,
        delimiter: "comma",
        order,
        columns: null,
      });
    } catch {
      setInProgress(false);
      setPendingOperation(null);
      setError("The export could not be prepared.");
    }
  };

  const previewIsCurrent = previewCount !== null && previewedFilters === currentFiltersKey;
  const deleteDisabled = inProgress || !previewIsCurrent || confirmationPhrase !== CONFIRMATION_PHRASE;

  return (
    <Dialog
      isOpen
      headline="Advanced delete"
      onClose={unloadComponent}
      headerCloseButton={{ tooltipText: "Close" }}
      isDismissable={!inProgress}
      actionInProgress={inProgress}
      width="min(640px, calc(100vw - 48px))"
      maxHeight="calc(100vh - 48px)"
      confirmAction={{
        label: "Delete",
        onClick: runDelete,
        inProgress: inProgress && pendingOperation === "delete",
        disabled: deleteDisabled,
        destructive: true,
      }}
      cancelAction={{ label: "Cancel", onClick: unloadComponent, disabled: inProgress }}
      notificationBar={error ? <NotificationBarAlert>{error}</NotificationBarAlert> : undefined}
    >
      <div style={{ display: "grid", gap: "20px", padding: "4px 0" }}>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))", gap: "16px" }}>
          <DateTimeInput
            label={"From: "}
            value={from}
            onChange={(value) => {
              setFrom(value);
              invalidatePreview();
            }}
            showTime={false}
            allowClear
          />
          <DateTimeInput
            label={"To: "}
            value={to}
            onChange={(value) => {
              setTo(value);
              invalidatePreview();
            }}
            showTime={false}
            allowClear
          />
        </div>

        <Input
          label="Number of records"
          type="number"
          min={1}
          value={numberOfRecords}
          placeholder="All matching submissions"
          explanationText="Maximum submissions removed. Leave empty to match all submissions in range."
          onChange={(event) => {
            setNumberOfRecords(event.currentTarget.value);
            invalidatePreview();
          }}
        />

        <Select
          label="Order by"
          value={order}
          onChange={(value) => {
            if (value) {
              setOrder(value as RemovalOrder);
              invalidatePreview();
            }
          }}
        >
          <MenuItem primaryLabel="Submitted - oldest first" value="ascending" />
          <MenuItem primaryLabel="Submitted - newest first" value="descending" />
        </Select>

        <div>
          <Button
            label="Preview matching count"
            color={ButtonColor.Secondary}
            disabled={inProgress}
            inProgress={inProgress && pendingOperation === "preview"}
            onClick={runPreview}
          />
        </div>

        {previewIsCurrent && (
          <div style={{ display: "grid", gap: "12px" }}>
            <p style={{ margin: 0 }}>
              {previewCount} submission{previewCount === 1 ? "" : "s"} match these criteria and would be
              permanently deleted.
            </p>
            <div>
              <Button
                label="Export these first"
                color={ButtonColor.Secondary}
                disabled={inProgress}
                inProgress={inProgress && pendingOperation === "export"}
                onClick={runExportThese}
              />
            </div>
          </div>
        )}

        <Input
          label={`Type ${CONFIRMATION_PHRASE} to confirm`}
          value={confirmationPhrase}
          disabled={inProgress}
          onChange={(event) => setConfirmationPhrase(event.currentTarget.value)}
        />
      </div>
    </Dialog>
  );
};
