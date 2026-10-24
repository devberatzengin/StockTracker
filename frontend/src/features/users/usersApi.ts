import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/api/client'
import type { CreateUserRequest, CreatedResponse, UpdateUserRequest, UserPage } from '@/api/types'

export interface UserListParams {
  page: number
  pageSize: number
  search?: string
}

const usersKey = ['users'] as const

export function useUsers(params: UserListParams) {
  const query = new URLSearchParams({
    page: String(params.page),
    pageSize: String(params.pageSize),
  })
  if (params.search) {
    query.set('search', params.search)
  }

  return useQuery({
    queryKey: [...usersKey, params],
    queryFn: () => apiClient.get<UserPage>(`/users?${query}`),
    placeholderData: keepPreviousData,
  })
}

function useInvalidateUsers() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: usersKey })
}

// Form mutations show their errors inside the form instead of a global toast.
const formMutationMeta = { skipGlobalError: true }

export function useCreateUser() {
  const invalidate = useInvalidateUsers()
  return useMutation({
    mutationFn: (body: CreateUserRequest) => apiClient.post<CreatedResponse>('/users', body),
    onSuccess: invalidate,
    meta: formMutationMeta,
  })
}

export function useUpdateUser() {
  const invalidate = useInvalidateUsers()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateUserRequest }) =>
      apiClient.put<void>(`/users/${id}`, body),
    onSuccess: invalidate,
    meta: formMutationMeta,
  })
}

export function useResetPassword() {
  return useMutation({
    mutationFn: ({ id, newPassword }: { id: string; newPassword: string }) =>
      apiClient.post<void>(`/users/${id}/reset-password`, { newPassword }),
    meta: formMutationMeta,
  })
}

export function useDeactivateUser() {
  const invalidate = useInvalidateUsers()
  return useMutation({
    mutationFn: (id: string) => apiClient.post<void>(`/users/${id}/deactivate`),
    onSuccess: invalidate,
  })
}
