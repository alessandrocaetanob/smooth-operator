import { defineConfig } from 'vitest/config';

/**
 * Base Vitest configuration.
 * Angular CLI merges its own settings on top (include/exclude/environment/plugins).
 * `pool: 'forks'` prevents the worker_threads hang that occurs on Windows with Vitest 4.x.
 */
export default defineConfig({
  test: {
    pool: 'forks',
  },
});
