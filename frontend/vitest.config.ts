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
