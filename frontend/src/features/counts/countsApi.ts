import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/api/client'
import { useIdempotencyKey } from '@/lib/idempotency'
import type {
  CountApprovalResult,
  CountDecision,
  CountDetail,
  CountPage,
  CountReview,
  CountScanResult,
  CountStatus,
  CreatedResponse,
} from '@/api/types'

// Form mutations show their errors inside the screen instead of a global toast.
const formMutationMeta = { skipGlobalError: true }

export interface CountFilters {
  page?: number
  status?: CountStatus | null
}

export function useCounts(filters: CountFilters) {
  const search = new URLSearchParams({ page: String(filters.page ?? 1), pageSize: '25' })
  if (filters.status) search.set('status', filters.status)
  return useQuery({
    queryKey: ['counts', 'list', filters],
    queryFn: () => apiClient.get<CountPage>(`/counts?${search}`),
    placeholderData: keepPreviousData,
  })
}

export function useCount(id: string) {
  return useQuery({
    queryKey: ['counts', 'detail', id],
    queryFn: () => apiClient.get<CountDetail>(`/counts/${id}`),
  })
}

export function useCountReview(id: string, enabled: boolean) {
  return useQuery({
    queryKey: ['counts', 'review', id],
    queryFn: () => apiClient.get<CountReview>(`/counts/${id}/review`),
    enabled,
  })
}

function useInvalidateCount(id?: string) {
  const queryClient = useQueryClient()
  return () =>
    queryClient.invalidateQueries({ queryKey: id ? ['counts', 'detail', id] : ['counts'] })
}

export function useStartCount() {
  const invalidate = useInvalidateCount()
  return useMutation({
    mutationFn: (body: { locationId: string; includeChildren: boolean; note: string | null }) =>
      apiClient.post<CreatedResponse>('/counts', body),
    onSuccess: invalidate,
    meta: formMutationMeta,
  })
}

export function useScanCount(id: string) {
  const invalidate = useInvalidateCount(id)
  return useMutation({
    mutationFn: (body: { locationId: string | null; code: string; quantity?: number }) =>
      apiClient.post<CountScanResult>(`/counts/${id}/scan`, body),
    onSuccess: invalidate,
    meta: formMutationMeta,
  })
}

export function useUndoCountScan(id: string) {
  const invalidate = useInvalidateCount(id)
  return useMutation({
    mutationFn: (lineId: string) =>
      apiClient.delete<void>(`/counts/${id}/lines/${lineId}/last-scan`),
    onSuccess: invalidate,
    meta: formMutationMeta,
  })
}

/** Submit and cancel change the status: refresh the list too. */
export function useCountAction(id: string) {
  const invalidate = useInvalidateCount()
  return useMutation({
    mutationFn: (action: 'submit' | 'cancel') => apiClient.post<void>(`/counts/${id}/${action}`),
    onSuccess: invalidate,
    meta: formMutationMeta,
  })
}

export function useApproveCount(id: string) {
  const queryClient = useQueryClient()
  const key = useIdempotencyKey()
  return useMutation({
    mutationFn: (decisions: CountDecision[]) =>
      apiClient.postOnce<CountApprovalResult>(`/counts/${id}/approve`, { decisions }, key.get()),
    onSuccess: () => {
      key.renew()
      return Promise.all(
        [['counts'], ['movements'], ['items'], ['products'], ['locations']].map((queryKey) =>
          queryClient.invalidateQueries({ queryKey }),
        ),
      )
    },
    meta: formMutationMeta,
  })
}
