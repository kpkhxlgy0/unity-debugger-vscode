import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    include: [
      "tests/extension/**/*.test.ts",
      "tests/integration/**/*.test.ts",
    ],
    passWithNoTests: true,
    restoreMocks: true,
    testTimeout: 10_000,
    hookTimeout: 30_000,
  },
});
