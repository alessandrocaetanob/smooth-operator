## 2026-05-09 - Improving floating item contrast

**Learning:** Floating items and tooltips over remote desktop connections (which can be any color, including pure white) need explicit high-contrast backgrounds instead of relying on transparent or low-opacity glass panels that blend into the canvas in light themes.

**Action:** Updated `.tool-btn` and tooltips to use explicitly high contrast inverse-surface colors and visible translucent backgrounds in both light and dark themes to ensure constant visibility.
