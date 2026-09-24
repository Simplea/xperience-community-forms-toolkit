import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach, beforeEach } from "vitest";

import { resetPageCommands } from "./kentico/adminBase";
import { snackbarMessages } from "./kentico/adminComponents";

beforeEach(() => {
  resetPageCommands();
  snackbarMessages.length = 0;
});

afterEach(() => {
  cleanup();
});
