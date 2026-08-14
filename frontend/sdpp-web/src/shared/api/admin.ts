import { apiClient } from "./client";

export interface UserSummary {
  id: string;
  fullName: string;
  email: string;
  photoUrl: string | null;
  domain: string;
  active: boolean;
  createdAtUtc: string;
  lastLoginAtUtc: string | null;
  roles: string[];
}
export interface PagedUsers {
  items: UserSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface RoleDto {
  id: string;
  name: string;
  description: string | null;
  isSystemRole: boolean;
}

export interface SessionDto {
  id: string;
  ipAddress: string | null;
  userAgent: string | null;
  operatingSystem: string | null;
  createdAtUtc: string;
  expiresAtUtc: string;
  revokedAtUtc: string | null;
  lastUsedAtUtc: string;
  isActive: boolean;
}

export interface AccessLogEntry {
  id: number;
  userId: string | null;
  fullNameSnapshot: string;
  email: string;
  occurredAtUtc: string;
  ipAddress: string | null;
  userAgent: string | null;
  result: string;
  provider: string;
  durationMs: number;
}
export interface PagedAccessLog {
  items: AccessLogEntry[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AllowedDomainDto {
  id: string;
  domain: string;
  isActive: boolean;
  isDevOnly: boolean;
  createdAtUtc: string;
  notes: string | null;
}

export function listUsers(params: { search?: string; domain?: string; active?: boolean; page?: number; pageSize?: number }) {
  const query = new URLSearchParams();
  if (params.search) query.set("search", params.search);
  if (params.domain) query.set("domain", params.domain);
  if (params.active !== undefined) query.set("active", String(params.active));
  query.set("page", String(params.page ?? 1));
  query.set("pageSize", String(params.pageSize ?? 25));
  return apiClient.get<PagedUsers>(`/api/v1/admin/users?${query.toString()}`);
}

export function listRoles() {
  return apiClient.get<RoleDto[]>("/api/v1/admin/roles");
}

export function updateUserRoles(userId: string, roleIds: string[]) {
  return apiClient.patch<void>(`/api/v1/admin/users/${userId}/roles`, { roleIds });
}

export function updateUserStatus(userId: string, active: boolean) {
  return apiClient.patch<void>(`/api/v1/admin/users/${userId}/status`, { active });
}

export function listUserSessions(userId: string) {
  return apiClient.get<SessionDto[]>(`/api/v1/admin/users/${userId}/sessions`);
}

export function listAccessHistory(params: { userId?: string; email?: string; page?: number; pageSize?: number }) {
  const query = new URLSearchParams();
  if (params.userId) query.set("userId", params.userId);
  if (params.email) query.set("email", params.email);
  query.set("page", String(params.page ?? 1));
  query.set("pageSize", String(params.pageSize ?? 25));
  return apiClient.get<PagedAccessLog>(`/api/v1/admin/access-history?${query.toString()}`);
}

export function listAllowedDomains() {
  return apiClient.get<AllowedDomainDto[]>("/api/v1/admin/allowed-domains");
}

export function createAllowedDomain(domain: string, isDevOnly: boolean, notes?: string) {
  return apiClient.post<AllowedDomainDto>("/api/v1/admin/allowed-domains", { domain, isDevOnly, notes });
}

export function deleteAllowedDomain(id: string) {
  return apiClient.delete<void>(`/api/v1/admin/allowed-domains/${id}`);
}
