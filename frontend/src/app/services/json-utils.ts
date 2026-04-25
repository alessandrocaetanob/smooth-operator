// Lightweight helpers used by feature services to read both PascalCase
// (default ASP.NET Core JSON output) and camelCase keys.
export function pick<T = any>(raw: any, ...keys: string[]): T | undefined {
  if (!raw) return undefined;
  for (const k of keys) {
    if (raw[k] !== undefined && raw[k] !== null) return raw[k] as T;
  }
  return undefined;
}

export function pickOr<T>(raw: any, fallback: T, ...keys: string[]): T {
  const v = pick<T>(raw, ...keys);
  return v === undefined ? fallback : v;
}
