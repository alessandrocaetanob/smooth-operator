import { defineConfig } from 'vitest/config';

/**
 * Base Vitest configuration.
 * Angular CLI merges its own settings on top (include/exclude/environment/plugins).
 * `pool: 'forks'` prevents the worker_threads hang that occurs on Windows with Vitest 4.x.
 */
export default defineConfig({
  test: {
    pool: 'forks',
    coverage: {
      provider: 'v8',
      enabled: process.env.CI === 'true',
      reporter: ['text', 'lcov'],
      exclude: [
        'src/main.ts',
        'src/environments/**',
        '**/*.spec.ts',
        '**/*.routes.ts',
        '**/*.config.ts',
        'src/app/app.config.ts',
      ],
      // Conservative floor — prevents regression; 70% target is Phase 8 CI gate.
      thresholds: {
        statements: 40,
        branches: 46,
        functions: 33,
        lines: 42,
      },
    },
  },
});
