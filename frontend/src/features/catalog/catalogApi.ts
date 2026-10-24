import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/api/client'
import type {
  BarcodeInput,
  CategoryPage,
  CreateProductRequest,
  CreatedResponse,
  ProductDto,
  ProductPage,
  TrackingType,
  UpdateProductRequest,
} from '@/api/types'

const categoriesKey = ['categories'] as const
const productsKey = ['products'] as const

// Form mutations show their errors inside the form instead of a global toast.
const formMutationMeta = { skipGlobalError: true }

function toQueryString(params: Record<string, string | number | boolean | undefined | null>) {
  const query = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== '') {
      query.set(key, String(value))
    }
  }
  return query.toString()
}

export interface CategoryListParams {
  page: number
  pageSize: number
  search?: string
  includeInactive?: boolean
}

export function useCategories(params: CategoryListParams) {
  return useQuery({
    queryKey: [...categoriesKey, params],
    queryFn: () => apiClient.get<CategoryPage>(`/categories?${toQueryString({ ...params })}`),
    placeholderData: keepPreviousData,
  })
}

/** Active categories for select inputs (the API caps pages at 100). */
export function useCategoryOptions() {
  return useCategories({ page: 1, pageSize: 100 })
}

function useInvalidate(key: readonly string[]) {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: key })
}

export function useSaveCategory() {
  const invalidate = useInvalidate(categoriesKey)
  return useMutation({
    mutationFn: async ({ id, name }: { id?: string; name: string }): Promise<void> => {
      if (id) {
        await apiClient.put<void>(`/categories/${id}`, { name })
      } else {
        await apiClient.post<CreatedResponse>('/categories', { name })
      }
    },
    onSuccess: invalidate,
    meta: formMutationMeta,
  })
}

export function useDeactivateCategory() {
  const invalidate = useInvalidate(categoriesKey)
  return useMutation({
    mutationFn: (id: string) => apiClient.post<void>(`/categories/${id}/deactivate`),
    onSuccess: invalidate,
  })
}

export interface ProductListParams {
  page: number
  pageSize: number
  search?: string
  categoryId?: string | null
  trackingType?: TrackingType | null
  includeInactive?: boolean
}

export function useProducts(params: ProductListParams) {
  return useQuery({
    queryKey: [...productsKey, 'list', params],
    queryFn: () => apiClient.get<ProductPage>(`/products?${toQueryString({ ...params })}`),
    placeholderData: keepPreviousData,
  })
}

export function useProduct(id: string) {
  return useQuery({
    queryKey: [...productsKey, 'detail', id],
    queryFn: () => apiClient.get<ProductDto>(`/products/${id}`),
  })
}

function useInvalidateCatalog() {
  const queryClient = useQueryClient()
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: productsKey }),
      // Category product counts change with products.
      queryClient.invalidateQueries({ queryKey: categoriesKey }),
    ])
}

export function useCreateProduct() {
  const invalidate = useInvalidateCatalog()
  return useMutation({
    mutationFn: (body: CreateProductRequest) => apiClient.post<CreatedResponse>('/products', body),
    onSuccess: invalidate,
    meta: formMutationMeta,
  })
}

export function useUpdateProduct() {
  const invalidate = useInvalidateCatalog()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateProductRequest }) =>
      apiClient.put<void>(`/products/${id}`, body),
    onSuccess: invalidate,
    meta: formMutationMeta,
  })
}

export function useDeactivateProduct() {
  const invalidate = useInvalidateCatalog()
  return useMutation({
    mutationFn: (id: string) => apiClient.post<void>(`/products/${id}/deactivate`),
    onSuccess: invalidate,
  })
}

export function useAddBarcode(productId: string) {
  const invalidate = useInvalidateCatalog()
  return useMutation({
    mutationFn: (barcode: BarcodeInput) =>
      apiClient.post<CreatedResponse>(`/products/${productId}/barcodes`, barcode),
    onSuccess: invalidate,
    meta: formMutationMeta,
  })
}

export function useRemoveBarcode(productId: string) {
  const invalidate = useInvalidateCatalog()
  return useMutation({
    mutationFn: (barcodeId: string) =>
      apiClient.delete<void>(`/products/${productId}/barcodes/${barcodeId}`),
    onSuccess: invalidate,
    meta: formMutationMeta,
  })
}
