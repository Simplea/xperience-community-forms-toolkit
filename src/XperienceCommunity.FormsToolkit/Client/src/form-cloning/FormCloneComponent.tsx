import React, { useEffect, useState } from "react";
import { ActionComponentProps, usePageCommand } from "@kentico/xperience-admin-base";
import {
  Dialog,
  Input,
  NotificationBarAlert,
  SnackbarItemVariant,
  useSnackbar,
} from "@kentico/xperience-admin-components";

interface ComponentData {
  readonly getDefaultsCommandName: string;
  readonly cloneCommandName: string;
}

interface DefaultsResponse {
  readonly displayName?: string;
  readonly error?: string;
}

interface CloneRequest {
  readonly sourceFormId: number;
  readonly displayName: string;
}

interface CloneResponse {
  readonly clonedFormId?: number;
  readonly error?: string;
}

type FormCloneComponentProps = Omit<ActionComponentProps, "componentData"> & ComponentData;

const getSourceFormId = (parameter: string | undefined): number | null => {
  const sourceFormId = Number(parameter);
  return Number.isSafeInteger(sourceFormId) && sourceFormId > 0 ? sourceFormId : null;
};

export const FormCloneComponent = ({
  action,
  getDefaultsCommandName,
  cloneCommandName,
  onActionExecuted,
  unloadComponent,
}: FormCloneComponentProps) => {
  const sourceFormId = getSourceFormId(action?.parameter);
  const [displayName, setDisplayName] = useState("");
  const [loading, setLoading] = useState(true);
  const [inProgress, setInProgress] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const { addMessage } = useSnackbar();

  const { execute: getDefaults } = usePageCommand<DefaultsResponse, number>(
    getDefaultsCommandName,
    {
      after: (response) => {
        setLoading(false);
        if (response?.error) {
          setError(response.error);
          return;
        }

        setDisplayName(response?.displayName ?? "");
      },
    },
  );

  const { execute: cloneForm } = usePageCommand<CloneResponse, CloneRequest>(
    cloneCommandName,
    {
      after: (response) => {
        setInProgress(false);
        if (response?.error) {
          setError(response.error);
          return;
        }

        addMessage({
          message: "Form cloned successfully.",
          variant: SnackbarItemVariant.Success,
          autoHide: true,
        });
        onActionExecuted();
        unloadComponent();
      },
    },
  );

  useEffect(() => {
    if (sourceFormId === null) {
      setLoading(false);
      setError("The requested form could not be identified.");
      return;
    }

    void getDefaults(sourceFormId).catch(() => {
      setLoading(false);
      setError("The form could not be loaded.");
    });
  }, [getDefaults, sourceFormId]);

  const submit = async () => {
    const normalizedDisplayName = displayName.trim();
    if (!normalizedDisplayName) {
      setError("Form name is required.");
      return;
    }

    if (sourceFormId === null) {
      setError("The requested form could not be identified.");
      return;
    }

    setError(null);
    setInProgress(true);
    try {
      await cloneForm({ sourceFormId, displayName: normalizedDisplayName });
    } catch {
      setInProgress(false);
      setError("The form could not be cloned.");
    }
  };

  return (
    <Dialog
      isOpen
      headline="Clone form"
      onClose={unloadComponent}
      headerCloseButton={{ tooltipText: "Close" }}
      isDismissable={!inProgress}
      actionInProgress={inProgress}
      confirmAction={{
        label: "Clone",
        onClick: submit,
        inProgress,
        disabled: loading || inProgress || !displayName.trim(),
      }}
      cancelAction={{ label: "Cancel", onClick: unloadComponent, disabled: inProgress }}
      notificationBar={error ? <NotificationBarAlert>{error}</NotificationBarAlert> : undefined}
    >
      <div style={{ display: "grid", gap: "16px", padding: "4px 0" }}>
        <Input
          label="Form name"
          markAsRequired
          value={displayName}
          maxLength={200}
          disabled={loading || inProgress}
          onChange={(event) => setDisplayName(event.currentTarget.value)}
        />
      </div>
    </Dialog>
  );
};
