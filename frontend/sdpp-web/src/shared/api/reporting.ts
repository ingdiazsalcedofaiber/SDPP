import { apiClient } from "./client";
import type { PagedAccessLog } from "./admin";

export type ReportingBucket = "day" | "week" | "month";

// The index signatures below are what @mui/x-charts' `dataset` prop requires structurally
// (DatasetElementType<unknown>) — these DTOs are consumed directly as chart datasets, not just displayed.
export interface CountByLabel {
  [key: string]: string | number;
  label: string;
  count: number;
}

export interface TimeSeriesPoint {
  [key: string]: string | number;
  periodStart: string;
  count: number;
}

export interface StatusBreakdown {
  completed: number;
  inProgress: number;
  failed: number;
}

export interface RecentConversion {
  jobId: string;
  originalFileName: string;
  operationType: string;
  status: string;
  createdAtUtc: string;
  completedAtUtc: string | null;
}

export interface DocumentsOverview {
  totalDocuments: number;
  totalConversions: number;
  conversionsByStatus: StatusBreakdown;
  conversionsByType: CountByLabel[];
  documentsByContentType: CountByLabel[];
  timeSeries: TimeSeriesPoint[];
  storageUsedBytes: number;
  storageQuotaBytes: number | null;
  recentConversions: RecentConversion[];
}

export interface UsersOverview {
  totalUsers: number;
  activeUsers: number;
  newUsersTimeSeries: TimeSeriesPoint[];
}

export interface ReportingRangeParams {
  from?: Date;
  to?: Date;
  bucket?: ReportingBucket;
}

function toQuery(params: ReportingRangeParams): string {
  const query = new URLSearchParams();
  if (params.from) query.set("from", params.from.toISOString());
  if (params.to) query.set("to", params.to.toISOString());
  if (params.bucket) query.set("bucket", params.bucket);
  return query.toString();
}

export function getPersonalDocumentsReport(params: ReportingRangeParams) {
  return apiClient.get<DocumentsOverview>(`/api/v1/documents/reporting/personal?${toQuery(params)}`);
}

export function getAdminDocumentsReport(params: ReportingRangeParams) {
  return apiClient.get<DocumentsOverview>(`/api/v1/documents/reporting/admin?${toQuery(params)}`);
}

export function getAdminUsersReport(params: ReportingRangeParams) {
  return apiClient.get<UsersOverview>(`/api/v1/admin/reporting/users?${toQuery(params)}`);
}

export function getMyAccessHistory(limit = 10) {
  return apiClient.get<PagedAccessLog>(`/api/v1/auth/me/access-history?limit=${limit}`);
}
