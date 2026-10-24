import type { LocationType } from '@/api/types'

export const LOCATION_TYPE_LABELS: Record<LocationType, string> = {
  Building: 'Bina',
  Room: 'Oda',
  Cabinet: 'Dolap',
  Shelf: 'Raf',
  Box: 'Kutu',
  Other: 'Diğer',
}

export const LOCATION_TYPE_OPTIONS = (
  Object.entries(LOCATION_TYPE_LABELS) as [LocationType, string][]
).map(([value, label]) => ({ value, label }))
