import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/api/client'
import type {
  Assignment,
  AssignmentPage,
  AssignmentResult,
  AssignmentStatus,
  ItemDetail,
  MovementResult,
  ReturnCondition,
  UserDto,
  UserLookup,
} from '@/api/types'

const query = (params: Record<string, string | number | null | undefined>) => {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== '') search.set(key, String(value))
  }
  return search.toString()
}

// Form mutations show their errors inside the form instead of a global toast.
const formMutationMeta = { skipGlobalError: true }

/** Assigning, returning and status changes move items: refresh stock views and assignment lists. */
function useInvalidateAssignments() {
  const queryClient = useQueryClient()
  return () =>
    Promise.all(
      [['assignments'], ['movements'], ['items'], ['products'], ['locations']].map((queryKey) =>
        queryClient.invalidateQueries({ queryKey }),
      ),
    )
}

export interface AssignmentFilters {
  page?: number
  pageSize?: number
  status?: AssignmentStatus | null
  userId?: string | null
  productId?: string | null
  itemId?: string | null
  search?: string
  sort?: string
}

export function useAssignments(filters: AssignmentFilters, enabled = true) {
  return useQuery({
    queryKey: ['assignments', 'list', filters],
    queryFn: () =>
      apiClient.get<AssignmentPage>(`/assignments?${query({ pageSize: 25, ...filters })}`),
    placeholderData: keepPreviousData,
    enabled,
  })
}

export function useMyAssignments() {
  return useQuery({
    queryKey: ['assignments', 'mine'],
    queryFn: () => apiClient.get<Assignment[]>('/assignments/mine'),
  })
}

export const fetchItem = (id: string) => apiClient.get<ItemDetail>(`/items/${id}`)

export function useAssignItem() {
  const invalidate = useInvalidateAssignments()
  return useMutation({
    mutationFn: (body: {
      itemId: string
      assigneeUserId: string
      dueDate: string | null
      note: string | null
    }) => apiClient.post<AssignmentResult>('/assignments', body),
    onSuccess: invalidate,
    meta: formMutationMeta,
  })
}

export function useReturnAssignment() {
  const invalidate = useInvalidateAssignments()
  return useMutation({
    mutationFn: ({
      id,
      ...body
    }: {
      id: string
      returnLocationId: string
      condition: ReturnCondition
      note: string | null
    }) => apiClient.post<AssignmentResult>(`/assignments/${id}/return`, body),
    onSuccess: invalidate,
    meta: formMutationMeta,
  })
}

export type StatusChangeAction = 'send-to-repair' | 'return-from-repair'

export function useItemStatusChange(itemId: string) {
  const invalidate = useInvalidateAssignments()
  return useMutation({
    mutationFn: ({ action, note }: { action: StatusChangeAction; note: string }) =>
      apiClient.post<MovementResult>(`/items/${itemId}/${action}`, { note }),
    onSuccess: invalidate,
    meta: formMutationMeta,
  })
}

export function useUserLookup(search: string) {
  return useQuery({
    queryKey: ['users', 'lookup', search],
    queryFn: () => apiClient.get<UserLookup[]>(`/users/lookup?${query({ search, limit: 20 })}`),
    placeholderData: keepPreviousData,
  })
}

export function useUser(id: string) {
  return useQuery({
    queryKey: ['users', 'detail', id],
    queryFn: () => apiClient.get<UserDto>(`/users/${id}`),
  })
}
