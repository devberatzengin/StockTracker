import { apiClient } from '@/api/client'
import type { ProductDto, TrackingType, Unit } from '@/api/types'

/** The product facts the movement forms need. */
export interface PickedProduct {
  id: string
  code: string
  name: string
  trackingType: TrackingType
  unit: Unit
  /** Primary photo thumbnail (signed, expires); shown in pickers. */
  thumbnailUrl?: string | null
}

export const fetchPickedProduct = async (id: string): Promise<PickedProduct> => {
  const p = await apiClient.get<ProductDto>(`/products/${id}`)
  return {
    id: p.id,
    code: p.code,
    name: p.name,
    trackingType: p.trackingType,
    unit: p.unit,
    thumbnailUrl: p.photos.find((photo) => photo.isPrimary)?.thumbnailUrl,
  }
}
