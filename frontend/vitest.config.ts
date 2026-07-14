import { defineConfig } from 'vitest/config';

/**
 * Base Vitest configuration.
 * Angular CLI merges its own settings on top (include/exclude/environment/plugins).
 * `pool: 'forks'` prevents the worker_threads hang that occurs on Windows with Vitest 4.x.
 */
export default defineConfig({
  test: {
    pool: 'forks',
    deps: {
      optimizer: {
        // Keep guacamole-common-js out of Vite's dependency pre-bundling.
        // It is only reached through a lazy route, so the optimizer discovers
        // it MID-RUN, triggering an "optimized dependencies changed" reload
        // that races with the vi.mock('guacamole-common-js') registration in
        // guacamole.service.spec.ts — when the reload wins, the real module
        // loads and 25 tests die with "client.connect is not a function"
        // (flaky locally, near-deterministic on CircleCI's always-cold cache).
        // Excluding it makes the module go through the inline transform, where
        // mock interception does not depend on optimizer timing.
        web: { exclude: ['guacamole-common-js'] },
      },
    },
    coverage: {
      provider: 'v8',
      enabled: !!process.env['CI'],
      reporter: ['text', 'lcov', 'clover', 'json-summary'],
      exclude: [
        'src/main.ts',
        'src/environments/**',
        '**/*.spec.ts',
        '**/*.routes.ts',
        '**/*.config.ts',
        'src/app/app.config.ts',
        '**/*.html',
        '**/*.css',
        '**/*.scss',
      ],
      excludeAfterRemap: true,
      // Thresholds are enforced post-run by scripts/filter-coverage.js after
      // HTML templates and build artifacts are removed from the coverage map.
      // (Angular's vitest plugin auto-instruments component templates and
      // does not honor `exclude` for them, so we cannot enforce thresholds
      // here against an accurate denominator.)
    },
  },
});
