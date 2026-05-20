import { Injectable } from '@angular/core';

export interface RuntimeConfig {
  helpUrl: string;
  docsUrl: string;
  featureFlags: Record<string, boolean>;
}

const DEFAULTS: RuntimeConfig = {
  helpUrl: '',
  docsUrl: '',
  featureFlags: {},
};

/**
 * Loads `/config/config.json` before route activation so that runtime values
 * (help URL, docs URL, feature flags) can be overridden per deployment without
 * rebuilding the image.  Falls back to compile-time defaults on any error.
 */
@Injectable({ providedIn: 'root' })
export class RuntimeConfigService {
  private _config: RuntimeConfig = { ...DEFAULTS };

  async load(): Promise<void> {
    try {
      const res = await fetch('/config/config.json');
      if (res.ok) {
        const data: Partial<RuntimeConfig> = await res.json();
        this._config = { ...DEFAULTS, ...data };
      }
    } catch {
      // Silently fall back to defaults — network errors must not block bootstrap.
    }
  }

  get config(): Readonly<RuntimeConfig> {
    return this._config;
  }
}
