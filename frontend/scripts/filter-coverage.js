#!/usr/bin/env node
/**
 * Filters HTML templates and build artifacts out of the coverage report
 * Angular's `@angular/build:unit-test` vitest plugin auto-instruments
 * component HTML templates and merges them into the totals — its plugin
 * code does not honor `coverage.exclude` for those files. This script
 * runs after vitest, drops the unwanted entries from `coverage-final.json`,
 * regenerates `lcov.info` / `clover.xml` / the text summary, and then
 * enforces TS-only thresholds.
 *
 * Files removed from coverage:
 *   - **.html, **.css, **.scss   (Angular component templates / styles)
 *   - dist/**, .angular/**       (build outputs, cached compiler artifacts)
 *   - migrations / generated     (template for the backend; future-proof here)
 *
 * Thresholds enforced match `vitest.config.ts`. Override by exporting
 * MIN_STATEMENTS / MIN_BRANCHES / MIN_FUNCTIONS / MIN_LINES (percentages).
 */

const fs = require('node:fs');
const path = require('node:path');
const libCoverage = require('istanbul-lib-coverage');
const libReport = require('istanbul-lib-report');
const reports = require('istanbul-reports');

const COVERAGE_DIR = path.resolve(__dirname, '..', 'coverage', 'frontend');
const COVERAGE_JSON = path.join(COVERAGE_DIR, 'coverage-final.json');

const EXCLUDE_EXTENSIONS = ['.html', '.css', '.scss'];
const EXCLUDE_PATH_FRAGMENTS = [
  `${path.sep}dist${path.sep}`,
  `${path.sep}.angular${path.sep}`,
  `${path.sep}node_modules${path.sep}`,
  `${path.sep}migrations${path.sep}`,
  `${path.sep}generated${path.sep}`,
];

function shouldExclude(filePath) {
  const normalized = filePath.replace(/[/\\]/g, path.sep).toLowerCase();
  if (EXCLUDE_EXTENSIONS.some((ext) => normalized.endsWith(ext))) {
    return true;
  }
  return EXCLUDE_PATH_FRAGMENTS.some((frag) => normalized.includes(frag));
}

function pct(part, total) {
  if (total === 0) return 100;
  return (100 * part) / total;
}

function fail(message) {
  console.error(`[31m${message}[0m`);
  process.exit(1);
}

function main() {
  if (!fs.existsSync(COVERAGE_JSON)) {
    fail(`coverage-final.json not found at ${COVERAGE_JSON}; run with --coverage first.`);
  }

  const raw = JSON.parse(fs.readFileSync(COVERAGE_JSON, 'utf8'));
  const filtered = {};
  let droppedHtml = 0;
  let droppedOther = 0;
  for (const [filePath, fileCov] of Object.entries(raw)) {
    if (!shouldExclude(filePath)) {
      filtered[filePath] = fileCov;
      continue;
    }
    if (filePath.toLowerCase().endsWith('.html')) {
      droppedHtml += 1;
    } else {
      droppedOther += 1;
    }
  }
  if (Object.keys(filtered).length === 0) {
    fail('After filtering, no files remain in the coverage report.');
  }
  fs.writeFileSync(COVERAGE_JSON, JSON.stringify(filtered, null, 2));

  // Regenerate lcov.info and clover.xml + a fresh text summary against the
  // filtered map so SonarCloud / Codecov see the same numbers we enforce.
  const coverageMap = libCoverage.createCoverageMap(filtered);
  const context = libReport.createContext({
    dir: COVERAGE_DIR,
    coverageMap,
    defaultSummarizer: 'flat',
  });
  for (const r of ['lcov', 'clover', 'text-summary', 'json-summary']) {
    reports.create(r, { file: r === 'lcov' ? 'lcov.info' : undefined }).execute(context);
  }

  const summary = coverageMap.getCoverageSummary().toJSON();
  const measured = {
    statements: pct(summary.statements.covered, summary.statements.total),
    branches: pct(summary.branches.covered, summary.branches.total),
    functions: pct(summary.functions.covered, summary.functions.total),
    lines: pct(summary.lines.covered, summary.lines.total),
  };
  const thresholds = {
    statements: Number(process.env.MIN_STATEMENTS ?? 80),
    branches: Number(process.env.MIN_BRANCHES ?? 80),
    functions: Number(process.env.MIN_FUNCTIONS ?? 80),
    lines: Number(process.env.MIN_LINES ?? 80),
  };

  console.log('\n=== Filtered coverage (HTML / build artifacts removed) ===');
  console.log(
    `Dropped ${droppedHtml} HTML file(s) and ${droppedOther} build-artifact file(s); ${Object.keys(filtered).length} files retained.`,
  );
  console.log(`Statements: ${measured.statements.toFixed(2)}% (min ${thresholds.statements}%)`);
  console.log(`Branches:   ${measured.branches.toFixed(2)}% (min ${thresholds.branches}%)`);
  console.log(`Functions:  ${measured.functions.toFixed(2)}% (min ${thresholds.functions}%)`);
  console.log(`Lines:      ${measured.lines.toFixed(2)}% (min ${thresholds.lines}%)`);

  const failures = Object.entries(measured).filter(([k, v]) => v < thresholds[k]);
  if (failures.length > 0) {
    fail(`\nFAIL: coverage below threshold on: ${failures.map(([k]) => k).join(', ')}.`);
  }
  console.log('\nAll thresholds met.\n');
}

main();
