// Deliberately permissive (not full RFC 5322) — this is UX guidance to catch an obvious typo
// before a signing invitation goes to the wrong address, not the source of truth on validity; the
// backend's own EnvelopeRecipient.Create still rejects a blank/whitespace-only email regardless.
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function isValidEmail(value: string): boolean {
  return EMAIL_PATTERN.test(value.trim());
}
