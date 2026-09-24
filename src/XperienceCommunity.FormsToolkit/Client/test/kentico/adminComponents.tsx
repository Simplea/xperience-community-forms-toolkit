import { ChangeEvent, ReactNode, useId } from "react";

// Minimal stand-ins for the admin components the toolkit uses. They keep the props the toolkit
// passes and render plain, labelled HTML so tests can query by role and label.

export const ButtonColor = { Primary: "primary", Secondary: "secondary", Tertiary: "tertiary" } as const;

export const SnackbarItemVariant = { Success: "success", Error: "error", Warning: "warning", Info: "info" } as const;

interface SnackbarMessage {
  readonly message: string;
  readonly variant: string;
}

/** Every snackbar message a component showed, in order. */
export const snackbarMessages: SnackbarMessage[] = [];

export const useSnackbar = () => ({
  addMessage: (message: SnackbarMessage) => {
    snackbarMessages.push(message);
  },
});

interface DialogAction {
  readonly label: string;
  readonly onClick: () => void;
  readonly disabled?: boolean;
}

interface DialogProps {
  readonly isOpen: boolean;
  readonly headline: string;
  readonly children?: ReactNode;
  readonly notificationBar?: ReactNode;
  readonly confirmAction?: DialogAction;
  readonly cancelAction?: DialogAction;
}

export const Dialog = ({ isOpen, headline, children, notificationBar, confirmAction, cancelAction }: DialogProps) =>
  isOpen ? (
    <div role="dialog" aria-label={headline}>
      {notificationBar}
      {children}
      {cancelAction && (
        <button type="button" disabled={cancelAction.disabled} onClick={cancelAction.onClick}>
          {cancelAction.label}
        </button>
      )}
      {confirmAction && (
        <button type="button" disabled={confirmAction.disabled} onClick={confirmAction.onClick}>
          {confirmAction.label}
        </button>
      )}
    </div>
  ) : null;

export const NotificationBarAlert = ({ children }: { readonly children?: ReactNode }) => <div role="alert">{children}</div>;

interface ButtonProps {
  readonly label: string;
  readonly onClick?: () => void;
  readonly disabled?: boolean;
}

export const Button = ({ label, onClick, disabled }: ButtonProps) => (
  <button type="button" disabled={disabled} onClick={onClick}>
    {label}
  </button>
);

interface InputProps {
  readonly label?: string;
  readonly value?: string | number;
  readonly type?: string;
  readonly placeholder?: string;
  readonly disabled?: boolean;
  readonly invalid?: boolean;
  readonly validationMessage?: string;
  readonly onChange?: (event: ChangeEvent<HTMLInputElement>) => void;
}

export const Input = ({ label, value, type, placeholder, disabled, invalid, validationMessage, onChange }: InputProps) => {
  const id = useId();
  return (
    <div>
      <label htmlFor={id}>{label}</label>
      <input
        id={id}
        type={type ?? "text"}
        value={value ?? ""}
        placeholder={placeholder}
        disabled={disabled}
        aria-invalid={invalid || undefined}
        onChange={onChange}
      />
      {validationMessage && <p>{validationMessage}</p>}
    </div>
  );
};

interface DateTimeInputProps {
  readonly label?: string;
  readonly value?: Date | null;
  readonly onChange?: (value: Date | null) => void;
}

const toInputDate = (value: Date | null | undefined) =>
  value
    ? `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, "0")}-${String(value.getDate()).padStart(2, "0")}`
    : "";

/** Renders a native date input; the toolkit only uses the date part. */
export const DateTimeInput = ({ label, value, onChange }: DateTimeInputProps) => {
  const id = useId();
  return (
    <div>
      <label htmlFor={id}>{label}</label>
      <input
        id={id}
        type="date"
        value={toInputDate(value)}
        onChange={(event) => {
          const [year, month, day] = event.currentTarget.value.split("-").map(Number);
          onChange?.(event.currentTarget.value ? new Date(year, month - 1, day) : null);
        }}
      />
    </div>
  );
};

interface SelectProps {
  readonly label?: string;
  readonly value?: string;
  readonly onChange?: (value: string | null) => void;
  readonly children?: ReactNode;
}

export const Select = ({ label, value, onChange, children }: SelectProps) => {
  const id = useId();
  return (
    <div>
      <label htmlFor={id}>{label}</label>
      <select id={id} value={value} onChange={(event) => onChange?.(event.currentTarget.value)}>
        {children}
      </select>
    </div>
  );
};

export const MenuItem = ({ primaryLabel, value }: { readonly primaryLabel: string; readonly value: string }) => (
  <option value={value}>{primaryLabel}</option>
);

interface CheckboxProps {
  readonly label?: string;
  readonly checked?: boolean;
  readonly disabled?: boolean;
  readonly onChange?: (event: ChangeEvent<HTMLInputElement>, checked: boolean) => void;
}

export const Checkbox = ({ label, checked, disabled, onChange }: CheckboxProps) => (
  <label>
    <input
      type="checkbox"
      checked={checked}
      disabled={disabled}
      onChange={(event) => onChange?.(event, event.currentTarget.checked)}
    />
    {label}
  </label>
);
