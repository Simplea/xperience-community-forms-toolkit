import { useState } from "react";
import { ActionComponentProps } from "@kentico/xperience-admin-base";
import {
  Dialog,
  MenuItem,
  NotificationBarAlert,
  Select,
  SnackbarItemVariant,
  useSnackbar,
} from "@kentico/xperience-admin-components";

import {
  downloadResponse,
  extractSubmissionId,
  getAntiforgeryHeaders,
  readProblemDetail,
  useXperienceAntiForgery,
} from "./FormSubmissionExportComponent";
import type { ExportFormat } from "./FormSubmissionExportComponent";

interface ComponentData {
  readonly currentViewDownloadUrl: string;
  readonly columns: readonly string[];
}

type FormSubmissionExportSelectedComponentProps = Omit<
  ActionComponentProps,
  "componentData"
> & ComponentData;

const formatLabels: Record<ExportFormat, string> = {
  csv: "CSV",
  excel: "Excel",
  xml: "XML",
};

const captureSelectedSubmissionIds = (): number[] => {
  const table = document.querySelector<HTMLElement>('[data-testid="table"][role="table"]');
  if (!table) {
    throw new Error("The current submissions view could not be read. Refresh the page and try again.");
  }

  const rows = Array.from(table.querySelectorAll<HTMLElement>('[data-testid="table-row"][role="row"]'));
  const checkedRows = rows.filter(
    (row) => row.querySelector<HTMLInputElement>('input[type="checkbox"]')?.checked,
  );

  const submissionIds = checkedRows.map(extractSubmissionId);
  if (submissionIds.length === 0 || submissionIds.some((identifier) => identifier === null)) {
    throw new Error("The selected submissions could not be identified. Refresh the page and try again.");
  }

  return submissionIds as number[];
};

export const FormSubmissionExportSelectedComponent = ({
  currentViewDownloadUrl,
  columns,
  unloadComponent,
}: FormSubmissionExportSelectedComponentProps) => {
  const [format, setFormat] = useState<ExportFormat>("csv");
  const [inProgress, setInProgress] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const { addMessage } = useSnackbar();
  const { getXsrfHeader } = useXperienceAntiForgery();

  const submit = async () => {
    setError(null);
    setInProgress(true);

    try {
      const submissionIds = captureSelectedSubmissionIds();
      const response = await fetch(currentViewDownloadUrl, {
        method: "POST",
        credentials: "same-origin",
        headers: getAntiforgeryHeaders(getXsrfHeader()),
        body: JSON.stringify({ format, submissionIds, columns }),
      });
      if (!response.ok) {
        throw new Error(await readProblemDetail(response));
      }

      await downloadResponse(response, format);
      unloadComponent();
    } catch (caught) {
      setInProgress(false);
      const message = caught instanceof Error ? caught.message : "The export could not be prepared.";
      setError(message);
      addMessage({ message, variant: SnackbarItemVariant.Error, autoHide: true });
    }
  };

  return (
    <Dialog
      isOpen
      headline="Export selected"
      onClose={unloadComponent}
      headerCloseButton={{ tooltipText: "Close" }}
      isDismissable={!inProgress}
      actionInProgress={inProgress}
      width="min(480px, calc(100vw - 48px))"
      confirmAction={{
        label: "Export",
        onClick: submit,
        inProgress,
        disabled: inProgress,
      }}
      cancelAction={{ label: "Cancel", onClick: unloadComponent, disabled: inProgress }}
      notificationBar={error ? <NotificationBarAlert>{error}</NotificationBarAlert> : undefined}
    >
      <Select
        label="Export to"
        value={format}
        onChange={(value) => value && setFormat(value as ExportFormat)}
      >
        {(["csv", "excel", "xml"] as const).map((option) => (
          <MenuItem key={option} primaryLabel={formatLabels[option]} value={option} />
        ))}
      </Select>
    </Dialog>
  );
};
