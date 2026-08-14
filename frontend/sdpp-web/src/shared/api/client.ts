import type { ProblemDetails } from "./types";

// In production this points at the Gateway (see docs/01-architecture/c4-diagrams.md) inside the
// corporate intranet — never a public endpoint. Configured per-environment via Vite env vars.
const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5080";

export class ApiError extends Error {
  readonly status: number;
  readonly problem?: ProblemDetails;

  constructor(message: string, status: number, problem?: ProblemDetails) {
    super(message);
    this.status = status;
    this.problem = problem;
  }
}

const MUTATING_METHODS = new Set(["POST", "PUT", "PATCH", "DELETE"]);

/** Called on every 401 response — wired from app/auth.ts so a session that expired mid-use (or
 * got revoked by an admin) sends the user back to /login instead of failing silently. */
let onUnauthorized: () => void = () => {};
export function registerUnauthorizedHandler(handler: () => void): void {
  onUnauthorized = handler;
}

/** Reads the non-HttpOnly sdpp_csrf cookie Identity.Api sets on login — the frontend's half of
 * the double-submit-cookie CSRF check (see BuildingBlocks.Infrastructure.Security.CsrfProtectionMiddleware). */
function readCsrfCookie(): string | null {
  const match = document.cookie.match(/(?:^|; )sdpp_csrf=([^;]*)/);
  return match ? decodeURIComponent(match[1]) : null;
}

// The access cookie (sdpp_at) lives only 15 minutes; the refresh cookie (sdpp_rt) lives 8 hours.
// These 4 pre-session endpoints must never trigger a silent refresh-and-retry on a 401: /refresh
// itself would recurse, and a 401 from /google or /mfa/* means "wrong credentials/code", not
// "session expired" — retrying those would resubmit a bad request instead of surfacing the error.
const NO_REFRESH_RETRY_PATHS = new Set([
  "/api/v1/auth/refresh",
  "/api/v1/auth/google",
  "/api/v1/auth/mfa/enroll/confirm",
  "/api/v1/auth/mfa/verify",
]);

// Dedupes concurrent refresh attempts — if several requests 401 around the same moment (e.g. a
// page that fires 3 queries on mount, as Admin does), they all await the same in-flight refresh
// instead of racing 3 separate /refresh calls against the one-time-use refresh token.
let refreshPromise: Promise<boolean> | null = null;

function attemptRefresh(): Promise<boolean> {
  refreshPromise ??= fetch(`${API_BASE_URL}/api/v1/auth/refresh`, {
    method: "POST",
    credentials: "include",
    headers: { ...(readCsrfCookie() ? { "X-CSRF-Token": readCsrfCookie()! } : {}) },
  })
    .then((r) => r.ok)
    .catch(() => false)
    .finally(() => {
      refreshPromise = null;
    });

  return refreshPromise;
}

/**
 * Thin fetch wrapper. Auth is entirely cookie-based (HttpOnly sdpp_at/sdpp_rt set by
 * Identity.Api) — `credentials: "include"` is what makes the browser attach them on every
 * request; there is no token for this code to read or attach manually anymore (see app/auth.ts).
 * A 401 on any endpoint outside NO_REFRESH_RETRY_PATHS silently tries /refresh once (the 8h
 * sdpp_rt cookie usually still being valid when the 15min sdpp_at one has expired) and retries
 * the original call before giving up and sending the user back to /login — without this, every
 * session would visibly break every 15 minutes even though a valid refresh token exists.
 */
async function request<T>(path: string, init?: RequestInit, isRetryAfterRefresh = false): Promise<T> {
  const method = (init?.method ?? "GET").toUpperCase();
  const csrfToken = MUTATING_METHODS.has(method) ? readCsrfCookie() : null;

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    credentials: "include",
    headers: {
      ...(init?.body instanceof FormData ? {} : { "Content-Type": "application/json" }),
      ...(csrfToken ? { "X-CSRF-Token": csrfToken } : {}),
      ...init?.headers,
    },
  });

  if (response.status === 401 && !isRetryAfterRefresh && !NO_REFRESH_RETRY_PATHS.has(path)) {
    const refreshed = await attemptRefresh();
    if (refreshed) {
      return request<T>(path, init, true);
    }
  }

  if (response.status === 401) {
    onUnauthorized();
  }

  if (!response.ok) {
    let problem: ProblemDetails | undefined;
    try {
      problem = await response.json();
    } catch {
      // no JSON body — leave problem undefined
    }
    throw new ApiError(problem?.title ?? response.statusText, response.status, problem);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

async function requestBlob(path: string, isRetryAfterRefresh = false): Promise<{ blob: Blob; fileName: string | null }> {
  const response = await fetch(`${API_BASE_URL}${path}`, { credentials: "include" });

  if (response.status === 401 && !isRetryAfterRefresh) {
    const refreshed = await attemptRefresh();
    if (refreshed) {
      return requestBlob(path, true);
    }
  }

  if (response.status === 401) {
    onUnauthorized();
  }
  if (!response.ok) {
    throw new ApiError(response.statusText, response.status);
  }

  // Files download through fetch (not a plain <a href>) so the sdpp_at cookie actually gets sent —
  // the backend replies with Content-Disposition instead of us guessing a filename.
  const disposition = response.headers.get("Content-Disposition");
  const fileName = disposition?.match(/filename[^;=\n]*=(?:(['"]).*?\1|[^;\n]*)/)?.[0]?.split("=")[1]?.replace(/['"]/g, "") ?? null;

  return { blob: await response.blob(), fileName };
}

export const apiClient = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: "POST", body: body instanceof FormData ? body : JSON.stringify(body) }),
  patch: <T>(path: string, body?: unknown) => request<T>(path, { method: "PATCH", body: JSON.stringify(body) }),
  delete: <T>(path: string) => request<T>(path, { method: "DELETE" }),
  postForm: <T>(path: string, form: FormData) => request<T>(path, { method: "POST", body: form }),
  getBlob: requestBlob,
};

/**
 * Fetch for the public signer-access endpoints (/api/v1/signature/access/*) — reachable both by an
 * external recipient with no SDPP session at all (auth is either nothing, for view/request-otp, or
 * a per-recipient bearer session token minted by /otp/verify) AND by an internal recipient signing
 * while already logged into SDPP (auth is their normal sdpp_at cookie). credentials:"include" is
 * required for that second case — without it the browser never sends the cookie cross-origin, and
 * the backend has no session to check RecipientAccessAuthorization against. Because a real session
 * cookie can be present, CSRF still applies exactly like apiClient's main request() (the backend's
 * CsrfProtectionMiddleware only activates when it sees sdpp_at/sdpp_rt) — same double-submit-cookie
 * header, no refresh-and-retry (an external recipient's bearer token can't be refreshed this way,
 * and an internal recipient's own session refresh is handled by the authenticated app shell already
 * being mounted elsewhere, not this standalone signing page).
 */
async function publicRequest<T>(path: string, init?: RequestInit & { bearer?: string }): Promise<T> {
  const { bearer, ...rest } = init ?? {};
  const method = (rest.method ?? "GET").toUpperCase();
  const csrfToken = MUTATING_METHODS.has(method) ? readCsrfCookie() : null;

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...rest,
    credentials: "include",
    headers: {
      ...(rest.body && !(rest.body instanceof FormData) ? { "Content-Type": "application/json" } : {}),
      ...(csrfToken ? { "X-CSRF-Token": csrfToken } : {}),
      ...(bearer ? { Authorization: `Bearer ${bearer}` } : {}),
      ...rest.headers,
    },
  });

  if (!response.ok) {
    let problem: ProblemDetails | undefined;
    try {
      problem = await response.json();
    } catch {
      // no JSON body
    }
    throw new ApiError(problem?.title ?? response.statusText, response.status, problem);
  }

  if (response.status === 204) {
    return undefined as T;
  }
  return (await response.json()) as T;
}

async function publicRequestBlob(path: string): Promise<Blob> {
  const response = await fetch(`${API_BASE_URL}${path}`, { credentials: "include" });
  if (!response.ok) {
    throw new ApiError(response.statusText, response.status);
  }
  return response.blob();
}

export const publicApiClient = {
  get: <T>(path: string) => publicRequest<T>(path),
  post: <T>(path: string, body?: unknown, bearer?: string) =>
    publicRequest<T>(path, { method: "POST", body: JSON.stringify(body), bearer }),
  getBlob: publicRequestBlob,
};
