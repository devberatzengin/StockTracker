import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/api/client'
import { useIdempotencyKey } from '@/lib/idempotency'
import type {
  ItemDetail,
  ItemPage,
  ItemStatus,
  LocationContents,
  MovementPage,
  MovementReason,
  MovementResult,
  MovementType,
  ProductStock,
  StockInLine,
  StockLine,
} from '@/api/types'

const query = (params: Record<string, string | number | boolean | null | undefined>) => {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== '') search.set(key, String(value))
  }
  return search.toString()
}

// Form mutations show their errors inside the form (line by line) instead of a global toast.
const formMutationMeta = { skipGlobalError: true }

/** A movement changes balances, item locations, product stock and location contents. */
function useInvalidateStock() {
  const queryClient = useQueryClient()
  return () =>
    Promise.all(
      [['movements'], ['items'], ['products'], ['locations']].map((queryKey) =>
        queryClient.invalidateQueries({ queryKey }),
      ),
    )
}

export function useStockIn() {
  const invalidate = useInvalidateStock()
  const key = useIdempotencyKey()
  return useMutation({
    mutationFn: (body: { toLocationId: string; lines: StockInLine[] }) =>
      apiClient.postOnce<MovementResult>('/movements/in', body, key.get()),
    onSuccess: () => {
      key.renew()
      return invalidate()
    },
    meta: formMutationMeta,
  })
}

export function useStockOut() {
  const invalidate = useInvalidateStock()
  const key = useIdempotencyKey()
  return useMutation({
    mutationFn: (body: { reason: MovementReason; note: string | null; lines: StockLine[] }) =>
      apiClient.postOnce<MovementResult>('/movements/out', body, key.get()),
    onSuccess: () => {
      key.renew()
      return invalidate()
    },
    meta: formMutationMeta,
  })
}

export function useStockTransfer() {
  const invalidate = useInvalidateStock()
  const key = useIdempotencyKey()
  return useMutation({
    mutationFn: (body: { toLocationId: string; lines: StockLine[] }) =>
      apiClient.postOnce<MovementResult>('/movements/transfer', body, key.get()),
    onSuccess: () => {
      key.renew()
      return invalidate()
    },
    meta: formMutationMeta,
  })
}

export function useReverseMovement() {
  const invalidate = useInvalidateStock()
  const key = useIdempotencyKey()
  return useMutation({
    mutationFn: ({ id, note }: { id: string; note: string }) =>
      apiClient.postOnce<MovementResult>(`/movements/${id}/reverse`, { note }, key.get()),
    onSuccess: () => {
      key.renew()
      return invalidate()
    },
    meta: formMutationMeta,
  })
}

export interface MovementFilters {
  page?: number
  pageSize?: number
  from?: string
  to?: string
  type?: MovementType | null
  productId?: string | null
  itemId?: string | null
  locationId?: string | null
  userId?: string | null
  referenceId?: string | null
  /** Movements in a count's scope after it started. */
  countId?: string | null
}

export function useMovements(filters: MovementFilters, enabled = true) {
  return useQuery({
    queryKey: ['movements', filters],
    queryFn: () => apiClient.get<MovementPage>(`/movements?${query({ pageSize: 25, ...filters })}`),
    placeholderData: keepPreviousData,
    enabled,
  })
}

export interface ItemFilters {
  page?: number
  pageSize?: number
  search?: string
  productId?: string
  status?: ItemStatus | null
}

export function useItems(filters: ItemFilters) {
  return useQuery({
    queryKey: ['items', 'list', filters],
    queryFn: () => apiClient.get<ItemPage>(`/items?${query({ pageSize: 25, ...filters })}`),
    placeholderData: keepPreviousData,
  })
}

export function useItem(id: string) {
  return useQuery({
    queryKey: ['items', 'detail', id],
    queryFn: () => apiClient.get<ItemDetail>(`/items/${id}`),
  })
}

export function useUpdateItem(id: string) {
  const invalidate = useInvalidateStock()
  return useMutation({
    mutationFn: (serialNumber: string | null) =>
      apiClient.put<void>(`/items/${id}`, { serialNumber }),
    onSuccess: invalidate,
    meta: formMutationMeta,
  })
}

export function useProductStock(productId: string | null | undefined) {
  return useQuery({
    queryKey: ['products', 'stock', productId],
    queryFn: () => apiClient.get<ProductStock>(`/products/${productId}/stock`),
    enabled: !!productId,
  })
}

export const fetchProductStock = (productId: string) =>
  apiClient.get<ProductStock>(`/products/${productId}/stock`)

export function useLocationContents(locationId: string, includeChildren: boolean) {
  return useQuery({
    queryKey: ['locations', 'contents', locationId, includeChildren],
    queryFn: () =>
      apiClient.get<LocationContents>(
        `/locations/${locationId}/contents?${query({ includeChildren })}`,
      ),
  })
}
