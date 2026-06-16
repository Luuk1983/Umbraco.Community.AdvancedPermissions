/**
 * Maps a permission state class to its glyph (✓ / ✗ / — / N/A).
 * Centralised so the editor, viewer, and shared components all render the same icon set.
 *
 * Note that `na` is deliberately NOT a dash: the dash denotes inheritance, so a not-applicable
 * cell needs its own marker to avoid reading as "inherits".
 *
 * @param cls The state class to render.
 * @returns The glyph for that state.
 */
export function stateIcon(cls: 'allow' | 'deny' | 'inherit' | 'na' | string): string {
  if (cls === 'allow') return '✓'; // ✓
  if (cls === 'deny') return '✗'; // ✗
  if (cls === 'na') return 'N/A'; // not-applicable — distinct from the inherit dash
  return '—'; // — (inherit / unknown)
}
