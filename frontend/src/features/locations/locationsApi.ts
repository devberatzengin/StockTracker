import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/api/client'
import type { CreatedResponse, LocationDto, LocationTreeNode, LocationType } from '@/api/types'

const locationsKey = ['locations'] as const

// Form mutations show their errors inside the form instead of a global toast.
const formMutationMeta = { skipGlobalError: true }

export function useLocationTree() {
  return useQuery({
    queryKey: [...locationsKey, 'tree'],
    queryFn: () => apiClient.get<LocationTreeNode[]>('/locations/tree'),
  })
}

export function useLocation(id: string) {
  return useQuery({
    queryKey: [...locationsKey, 'detail', id],
    queryFn: () => apiClient.get<LocationDto>(`/locations/${id}`),
  })
}

function useInvalidateLocations() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: locationsKey })
}

export function useCreateLocation() {
  const invalidate = useInvalidateLocations()
  return useMutation({
    mutationFn: (body: { name: string; type: LocationType; parentId: string | null }) =>
      apiClient.post<CreatedResponse>('/locations', body),
    onSuccess: invalidate,
    meta: formMutationMeta,
  })
}

export function useUpdateLocation() {
  const invalidate = useInvalidateLocations()
  return useMutation({
    mutationFn: ({ id, ...body }: { id: string; name: string; type: LocationType }) =>
      apiClient.put<void>(`/locations/${id}`, body),
    onSuccess: invalidate,
    meta: formMutationMeta,
  })
}

export function useMoveLocation() {
  const invalidate = useInvalidateLocations()
  return useMutation({
    mutationFn: ({ id, newParentId }: { id: string; newParentId: string | null }) =>
      apiClient.post<void>(`/locations/${id}/move`, { newParentId }),
    onSuccess: invalidate,
    meta: formMutationMeta,
  })
}

export function useDeactivateLocation() {
  const invalidate = useInvalidateLocations()
  return useMutation({
    mutationFn: (id: string) => apiClient.post<void>(`/locations/${id}/deactivate`),
    onSuccess: invalidate,
  })
}
