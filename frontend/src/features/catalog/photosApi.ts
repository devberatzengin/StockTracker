import { useMutation, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/api/client'
import type { ProductPhoto } from '@/api/types'

/** Photos are part of the product (detail, list thumbnails, scan results). */
function useInvalidateProduct() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: ['products'] })
}

// Errors are shown next to the gallery instead of a global toast.
const meta = { skipGlobalError: true }

export function useUploadPhoto(productId: string) {
  const invalidate = useInvalidateProduct()
  return useMutation({
    mutationFn: (file: File) => {
      const form = new FormData()
      form.append('file', file)
      return apiClient.post<ProductPhoto>(`/products/${productId}/photos`, form)
    },
    onSuccess: invalidate,
    meta,
  })
}

export function usePhotoActions(productId: string) {
  const invalidate = useInvalidateProduct()
  const base = `/products/${productId}/photos`
  return {
    setPrimary: useMutation({
      mutationFn: (photoId: string) => apiClient.post<void>(`${base}/${photoId}/primary`),
      onSuccess: invalidate,
      meta,
    }),
    reorder: useMutation({
      mutationFn: (photoIds: string[]) => apiClient.put<void>(`${base}/order`, { photoIds }),
      onSuccess: invalidate,
      meta,
    }),
    remove: useMutation({
      mutationFn: (photoId: string) => apiClient.delete(`${base}/${photoId}`),
      onSuccess: invalidate,
      meta,
    }),
  }
}
