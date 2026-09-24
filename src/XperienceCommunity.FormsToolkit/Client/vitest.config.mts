import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";

const fromRoot = (path: string) => fileURLToPath(new URL(path, import.meta.url));

// The Kentico admin packages only run inside the Xperience admin app, so component tests
// swap them for lightweight test doubles that render plain, accessible HTML.
export default defineConfig({
  resolve: {
    alias: {
      "@kentico/xperience-admin-base": fromRoot("./test/kentico/adminBase.tsx"),
      "@kentico/xperience-admin-components": fromRoot("./test/kentico/adminComponents.tsx"),
    },
  },
  test: {
    environment: "jsdom",
    include: ["src/**/*.test.tsx"],
    setupFiles: ["./test/setup.ts"],
  },
});
