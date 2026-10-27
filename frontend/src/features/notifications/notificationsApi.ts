import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/api/client'
import type { NotificationPage } from '@/api/types'

/** The bell polls; there is no live connection (PLAN 3.4). */
export const POLL_INTERVAL_MS = 60_000

export function useUnreadCount() {
  return useQuery({
    queryKey: ['notifications', 'unread-count'],
    queryFn: () => apiClient.get<{ count: number }>('/notifications/unread-count'),
    refetchInterval: POLL_INTERVAL_MS,
    refetchIntervalInBackground: false,
  })
}

export function useNotifications(
  page: number,
  pageSize: number,
  unreadOnly = false,
  enabled = true,
) {
  return useQuery({
    queryKey: ['notifications', 'list', page, pageSize, unreadOnly],
    queryFn: () =>
      apiClient.get<NotificationPage>(
        `/notifications?page=${page}&pageSize=${pageSize}&unreadOnly=${unreadOnly}`,
      ),
    placeholderData: keepPreviousData,
    enabled,
  })
}

function useInvalidate() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: ['notifications'] })
}

export function useMarkRead() {
  const invalidate = useInvalidate()
  return useMutation({
    mutationFn: (id: string) => apiClient.post<void>(`/notifications/${id}/read`),
    onSuccess: invalidate,
  })
}

export function useMarkAllRead() {
  const invalidate = useInvalidate()
  return useMutation({
    mutationFn: () => apiClient.post<void>('/notifications/read-all'),
    onSuccess: invalidate,
  })
}
