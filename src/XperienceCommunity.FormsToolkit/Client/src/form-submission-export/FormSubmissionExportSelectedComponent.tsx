import { useEffect, useRef } from "react";
import { ActionComponentProps } from "@kentico/xperience-admin-base";
import { SnackbarItemVariant, useSnackbar } from "@kentico/xperience-admin-components";

import {
  downloadResponse,
  extractSubmissionId,
  getAntiforgeryHeaders,
  readProblemDetail,
  useXperienceAntiForgery,
} from "./FormSubmissionExportComponent";

interface ComponentData {
  readonly currentViewDownloadUrl: string;
  readonly columns: readonly string[];
}

type FormSubmissionExportSelectedComponentProps = Omit<
  ActionComponentProps,
  "componentData"
> & ComponentData;

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
  const ran = useRef(false);
  const { addMessage } = useSnackbar();
  const { getXsrfHeader } = useXperienceAntiForgery();

  useEffect(() => {
    if (ran.current) {
      return;
    }
    ran.current = true;

    (async () => {
      try {
        const submissionIds = captureSelectedSubmissionIds();
        const response = await fetch(currentViewDownloadUrl, {
          method: "POST",
          credentials: "same-origin",
          headers: getAntiforgeryHeaders(getXsrfHeader()),
          body: JSON.stringify({ format: "csv", submissionIds, columns }),
        });
        if (!response.ok) {
          throw new Error(await readProblemDetail(response));
        }

        await downloadResponse(response, "csv");
      } catch (caught) {
        addMessage({
          message: caught instanceof Error ? caught.message : "The export could not be prepared.",
          variant: SnackbarItemVariant.Error,
          autoHide: true,
        });
      } finally {
        unloadComponent();
      }
    })();
  }, [currentViewDownloadUrl, columns, getXsrfHeader, addMessage, unloadComponent]);

  return null;
};
