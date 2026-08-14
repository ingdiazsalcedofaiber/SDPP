import { create } from "zustand";
import { apiClient, ApiError, registerUnauthorizedHandler } from "../shared/api/client";
import { useConversionWizardStore } from "../features/conversion/wizardStore";

export interface AuthUser {
  id: string;
  fullName: string;
  email: string;
  photoUrl: string | null;
  domain: string;
  roles: string[];
}

type AuthStatus = "idle" | "loading" | "authenticated" | "unauthenticated";

interface AuthState {
  user: AuthUser | null;
  status: AuthStatus;
  setUser: (user: AuthUser) => void;
  clear: () => void;
  hasRole: (role: string) => boolean;
}

/**
 * Holds the authenticated identity — populated exclusively from the backend (GET /api/v1/auth/me,
 * called right after login and once on app boot to restore a session from its HttpOnly cookie).
 * There is no client-side token to decode: the sdpp_at cookie is HttpOnly by design, so the
 * frontend never sees or stores it, only the user profile it resolves to.
 */
export const useAuthStore = create<AuthState>((set, get) => ({
  user: null,
  status: "idle",
  setUser: (user) => set({ user, status: "authenticated" }),
  clear: () => set({ user: null, status: "unauthenticated" }),
  hasRole: (role) => get().user?.roles.includes(role) ?? false,
}));

registerUnauthorizedHandler(() => useAuthStore.getState().clear());

/** Called once on app mount — if a valid session cookie already exists (page refresh, reopened
 * tab), this silently restores it instead of forcing the user through Google's login screen again. */
export async function bootstrapSession(): Promise<void> {
  try {
    const user = await apiClient.get<AuthUser>("/api/v1/auth/me");
    useAuthStore.getState().setUser(user);
  } catch {
    useAuthStore.getState().clear();
  }
}

export type GoogleLoginResult =
  | { status: "MFA_ENROLLMENT_REQUIRED"; qrCodeDataUri: string; manualEntryKey: string }
  | { status: "MFA_CHALLENGE_REQUIRED" };

/** POSTs the Google ID token to Identity.Api — the backend does 100% of the validation (signature,
 * issuer, audience, expiration, email_verified); the frontend never inspects the token itself.
 * MFA is mandatory platform-wide, so this never resolves into a session directly — it always
 * returns one of the two challenge states above (or throws ApiError on failure). The real session
 * is only established by confirmMfaEnrollment/verifyMfa below. */
export async function loginWithGoogle(idToken: string): Promise<GoogleLoginResult> {
  return apiClient.post<GoogleLoginResult>("/api/v1/auth/google", { idToken });
}

/** Completes first-time MFA setup: submits the code from the freshly-scanned QR. On success,
 * returns the 10 backup codes (shown to the user exactly once — never retrievable again) and
 * populates the session, same single entry point into useAuthStore as verifyMfa below. */
export async function confirmMfaEnrollment(code: string): Promise<{ user: AuthUser; backupCodes: string[] }> {
  const result = await apiClient.post<{ user: AuthUser; backupCodes: string[] }>("/api/v1/auth/mfa/enroll/confirm", { code });
  useAuthStore.getState().setUser(result.user);
  return result;
}

/** Verifies the second factor (a live TOTP code or a backup code) on every login after the
 * first — the only other way a real session gets established. */
export async function verifyMfa(code: string): Promise<void> {
  const { user } = await apiClient.post<{ user: AuthUser }>("/api/v1/auth/mfa/verify", { code });
  useAuthStore.getState().setUser(user);
}

export async function logout(): Promise<void> {
  try {
    await apiClient.post("/api/v1/auth/logout");
  } finally {
    useAuthStore.getState().clear();
    // A different user could sign in next on the same browser — don't leave a stranger's
    // in-progress conversion (file, form fields) sitting in the wizard store.
    useConversionWizardStore.getState().reset();
  }
}

export function isDomainNotAllowedError(error: unknown): error is ApiError {
  return error instanceof ApiError && error.problem?.errorCode === "DOMAIN_NOT_ALLOWED";
}
