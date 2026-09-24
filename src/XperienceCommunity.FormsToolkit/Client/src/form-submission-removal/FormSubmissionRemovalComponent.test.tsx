import { ComponentProps } from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { commandData, mockPageCommand, pageCommandCalls } from "../../test/kentico/adminBase";
import { snackbarMessages } from "../../test/kentico/adminComponents";
import { FormSubmissionRemovalComponent } from "./FormSubmissionRemovalComponent";

const PREVIEW = "PreviewAdvancedDelete";
const DELETE = "AdvancedDelete";
const EXPORT_TOKEN = "CreateExportToken";

type Props = ComponentProps<typeof FormSubmissionRemovalComponent>;

const renderDialog = () => {
  const unloadComponent = vi.fn();
  const props = {
    previewCommandName: PREVIEW,
    deleteCommandName: DELETE,
    exportTokenCommandName: EXPORT_TOKEN,
    unloadComponent,
  } as unknown as Props;

  render(<FormSubmissionRemovalComponent {...props} />);
  return { unloadComponent };
};

const setFilters = () => {
  fireEvent.change(screen.getByLabelText("From:"), { target: { value: "2026-09-01" } });
  fireEvent.change(screen.getByLabelText("To:"), { target: { value: "2026-09-14" } });
  fireEvent.change(screen.getByLabelText("Number of records"), { target: { value: "3" } });
  fireEvent.change(screen.getByLabelText("Order by"), { target: { value: "descending" } });
};

const preview = async (matchingCount = 3, upperSubmissionId = 1022) => {
  mockPageCommand(PREVIEW, () => ({ matchingCount, upperSubmissionId }));
  fireEvent.click(screen.getByRole("button", { name: "Preview matching count" }));
  await screen.findByText(new RegExp(`${matchingCount} submissions? match these criteria`));
};

const typeConfirmation = (value: string) =>
  fireEvent.change(screen.getByLabelText("Type DELETE to confirm"), { target: { value } });

const deleteButton = () => screen.getByRole("button", { name: "Delete" });

const expectedFilters = {
  from: "2026-09-01",
  to: "2026-09-14",
  timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone,
  numberOfRecords: "3",
  order: "descending",
};

describe("FormSubmissionRemovalComponent", () => {
  let startedDownloads: string[];

  beforeEach(() => {
    startedDownloads = [];
    vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(function (this: HTMLAnchorElement) {
      startedDownloads.push(this.getAttribute("href") ?? "");
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("previews without a boundary and uses the returned boundary for export and delete", async () => {
    const { unloadComponent } = renderDialog();
    setFilters();

    await preview(3, 1022);
    expect(commandData(PREVIEW)).toEqual([{ ...expectedFilters, upperSubmissionId: null }]);

    mockPageCommand(EXPORT_TOKEN, () => ({ downloadUrl: "/download?token=abc" }));
    fireEvent.click(screen.getByRole("button", { name: "Export these first" }));
    await waitFor(() => expect(startedDownloads).toEqual(["/download?token=abc"]));

    // Regression for #22 (the export dropped the record limit) and #24 (the export and delete
    // ignored the preview's boundary): both must act on exactly what the preview counted.
    expect(commandData(EXPORT_TOKEN)).toEqual([
      {
        ...expectedFilters,
        upperSubmissionId: 1022,
        format: "excel",
        operation: "export",
        includeHeader: true,
        delimiter: "comma",
        columns: null,
      },
    ]);

    mockPageCommand(DELETE, () => ({ deletedCount: 3 }));
    typeConfirmation("DELETE");
    fireEvent.click(deleteButton());

    await waitFor(() => expect(unloadComponent).toHaveBeenCalled());
    expect(commandData(DELETE)).toEqual([{ ...expectedFilters, upperSubmissionId: 1022 }]);
    expect(snackbarMessages.map((message) => message.message)).toEqual(["Deleted 3 submission(s)."]);
  });

  it("requires a current preview before Delete is enabled", async () => {
    renderDialog();
    typeConfirmation("DELETE");
    expect(deleteButton()).toBeDisabled();

    await preview();
    expect(deleteButton()).toBeEnabled();
  });

  it.each([
    ["From", () => fireEvent.change(screen.getByLabelText("From:"), { target: { value: "2026-08-01" } })],
    ["To", () => fireEvent.change(screen.getByLabelText("To:"), { target: { value: "2026-09-30" } })],
    ["Number of records", () => fireEvent.change(screen.getByLabelText("Number of records"), { target: { value: "5" } })],
    ["Order by", () => fireEvent.change(screen.getByLabelText("Order by"), { target: { value: "ascending" } })],
  ])("invalidates the preview when %s changes", async (_filter, changeFilter) => {
    renderDialog();
    setFilters();
    await preview();
    typeConfirmation("DELETE");
    expect(deleteButton()).toBeEnabled();

    changeFilter();

    expect(screen.queryByText(/match these criteria/)).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Export these first" })).not.toBeInTheDocument();
    expect(deleteButton()).toBeDisabled();
  });

  it.each(["", "delete", "DELETE ", "DEL"])("keeps Delete disabled for the confirmation %j", async (value) => {
    renderDialog();
    await preview();

    typeConfirmation(value);

    expect(deleteButton()).toBeDisabled();
  });

  it("rejects an invalid record limit without calling the server", () => {
    renderDialog();
    fireEvent.change(screen.getByLabelText("Number of records"), { target: { value: "0" } });

    fireEvent.click(screen.getByRole("button", { name: "Preview matching count" }));

    expect(screen.getByRole("alert")).toHaveTextContent("Number of records must be a positive whole number.");
    expect(pageCommandCalls).toEqual([]);
  });

  it("shows a server error from the preview and keeps Delete disabled", async () => {
    renderDialog();
    mockPageCommand(PREVIEW, () => ({ error: "The administration time zone is invalid." }));

    fireEvent.click(screen.getByRole("button", { name: "Preview matching count" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("The administration time zone is invalid.");
    typeConfirmation("DELETE");
    expect(deleteButton()).toBeDisabled();
  });
});
