// The public "Pedir atuação" request form and its submit call.

/** The React request form's fields (names match POST /api/public/requests). Dates are yyyy-MM-dd. */
export type RequestForm = {
  name: string;
  email: string;
  phone: string;
  eventType: string;
  preferredDate: string;
  isDateRange: boolean;
  preferredEndDate: string;
  location: string;
  message: string;
  website: string; // honeypot - always empty for people
};

export type FieldErrors = Partial<Record<keyof RequestForm, string>>;

type SubmitOutcome =
  | { kind: 'submitted' }
  | { kind: 'invalid'; errors: FieldErrors }
  | { kind: 'expired' } // antiforgery token refused: reload and retry
  | { kind: 'throttled' }
  | { kind: 'failed' };

/**
 * GET /api/public/antiforgery-token, then POST /api/public/requests as a form (the server enforces
 * antiforgery on form posts). Every server answer maps to one outcome; nothing throws.
 */
export async function submitRequest(form: RequestForm): Promise<SubmitOutcome> {
  try {
    const tokenResponse = await fetch('/api/public/antiforgery-token', { credentials: 'same-origin' });
    if (!tokenResponse.ok) return { kind: 'failed' };
    const { fieldName, token } = (await tokenResponse.json()) as { fieldName: string; token: string };

    const body = new FormData();
    body.set(fieldName, token);
    for (const [key, value] of Object.entries(form)) {
      if (key === 'preferredEndDate' && !form.isDateRange) continue;
      body.set(key, String(value));
    }

    const response = await fetch('/api/public/requests', { method: 'POST', body, credentials: 'same-origin' });
    if (response.ok) return { kind: 'submitted' };
    if (response.status === 429) return { kind: 'throttled' };
    if (response.status === 400) {
      const problem = await response.json().catch(() => null);
      if (!problem?.errors) return { kind: 'expired' };
      const errors: FieldErrors = {};
      for (const [field, messages] of Object.entries(problem.errors as Record<string, string[]>)) {
        errors[field as keyof RequestForm] = messages[0];
      }
      return { kind: 'invalid', errors };
    }
    return { kind: 'failed' };
  } catch {
    return { kind: 'failed' };
  }
}
