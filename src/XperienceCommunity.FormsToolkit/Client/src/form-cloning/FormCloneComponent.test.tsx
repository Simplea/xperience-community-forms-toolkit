import { ComponentProps } from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import { commandData, mockPageCommand, pageCommandCalls } from "../../test/kentico/adminBase";
import { snackbarMessages } from "../../test/kentico/adminComponents";
import { FormCloneComponent } from "./FormCloneComponent";

const GET_DEFAULTS = "GetFormCloneDefaults";
const CLONE = "CloneForm";

type Props = ComponentProps<typeof FormCloneComponent>;

const renderDialog = ({ parameter = "7", maximumDisplayNameLength = 10 } = {}) => {
  const onActionExecuted = vi.fn();
  const unloadComponent = vi.fn();
  const props = {
    action: { parameter },
    getDefaultsCommandName: GET_DEFAULTS,
    cloneCommandName: CLONE,
    maximumDisplayNameLength,
    onActionExecuted,
    unloadComponent,
  } as unknown as Props;

  render(<FormCloneComponent {...props} />);
  return { onActionExecuted, unloadComponent };
};

const nameInput = () => screen.getByLabelText("Form name");
const cloneButton = () => screen.getByRole("button", { name: "Clone" });
const setName = (value: string) => fireEvent.change(nameInput(), { target: { value } });

describe("FormCloneComponent", () => {
  it("loads the default name for the row's form and clones with the trimmed name", async () => {
    mockPageCommand(GET_DEFAULTS, () => ({ displayName: "Contact (copy)" }));
    const { onActionExecuted, unloadComponent } = renderDialog({ maximumDisplayNameLength: 200 });

    await waitFor(() => expect(nameInput()).toHaveValue("Contact (copy)"));
    expect(commandData(GET_DEFAULTS)).toEqual([7]);

    mockPageCommand(CLONE, () => ({ clonedFormId: 8 }));
    setName("  Contact v2  ");
    fireEvent.click(cloneButton());

    await waitFor(() => expect(unloadComponent).toHaveBeenCalled());
    expect(commandData(CLONE)).toEqual([{ sourceFormId: 7, displayName: "Contact v2" }]);
    expect(onActionExecuted).toHaveBeenCalled();
    expect(snackbarMessages.map((message) => message.message)).toEqual(["Form cloned successfully."]);
  });

  // Regression for #26: the limit was passed as an unsupported maxLength prop and never applied.
  it("blocks names longer than the server's limit, measured after trimming", async () => {
    mockPageCommand(GET_DEFAULTS, () => ({ displayName: "Contact" }));
    renderDialog({ maximumDisplayNameLength: 10 });
    await waitFor(() => expect(nameInput()).toHaveValue("Contact"));

    setName("a".repeat(11));
    expect(cloneButton()).toBeDisabled();
    expect(nameInput()).toHaveAttribute("aria-invalid", "true");
    expect(screen.getByText("Form name must be 10 characters or fewer.")).toBeInTheDocument();

    setName(`${"a".repeat(10)}   `);
    expect(cloneButton()).toBeEnabled();
    expect(nameInput()).not.toHaveAttribute("aria-invalid");
    expect(screen.queryByText("Form name must be 10 characters or fewer.")).not.toBeInTheDocument();
  });

  it("disables Clone for a blank name", async () => {
    mockPageCommand(GET_DEFAULTS, () => ({ displayName: "Contact" }));
    renderDialog();
    await waitFor(() => expect(nameInput()).toHaveValue("Contact"));

    setName("   ");

    expect(cloneButton()).toBeDisabled();
  });

  it("shows an error without calling the server when the row has no valid form ID", () => {
    renderDialog({ parameter: "not-a-number" });

    expect(screen.getByRole("alert")).toHaveTextContent("The requested form could not be identified.");
    expect(pageCommandCalls).toEqual([]);
  });

  it("shows a server error from the clone", async () => {
    mockPageCommand(GET_DEFAULTS, () => ({ displayName: "Contact (copy)" }));
    const { unloadComponent } = renderDialog({ maximumDisplayNameLength: 200 });
    await waitFor(() => expect(nameInput()).toHaveValue("Contact (copy)"));

    mockPageCommand(CLONE, () => ({ error: "A form with this name already exists." }));
    fireEvent.click(cloneButton());

    expect(await screen.findByRole("alert")).toHaveTextContent("A form with this name already exists.");
    expect(unloadComponent).not.toHaveBeenCalled();
  });
});
