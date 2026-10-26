import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { apiClient } from '@/api/client'
import type { AuditLogPage } from '@/api/types'

export interface AuditFilters {
  page?: number
  pageSize?: number
  entityType?: string | null
  entityId?: string | null
  userId?: string | null
  from?: string | null
  to?: string | null
}

export function useAuditLogs(filters: AuditFilters, enabled = true) {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries({ pageSize: 20, ...filters })) {
    if (value !== undefined && value !== null && value !== '') search.set(key, String(value))
  }
  return useQuery({
    queryKey: ['audit', filters],
    queryFn: () => apiClient.get<AuditLogPage>(`/audit?${search}`),
    placeholderData: keepPreviousData,
    enabled,
  })
}
