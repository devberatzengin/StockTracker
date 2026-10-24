import type { BarcodeFormat, TrackingType, Unit } from '@/api/types'

export const UNIT_LABELS: Record<Unit, string> = {
  Piece: 'Adet',
  Kg: 'Kg',
  Meter: 'Metre',
  Liter: 'Litre',
  Package: 'Paket',
  Box: 'Kutu',
}

export const TRACKING_TYPE_LABELS: Record<TrackingType, string> = {
  Serialized: 'Tekil (seri numaralı)',
  Quantity: 'Miktar bazlı',
}

export const BARCODE_FORMAT_LABELS: Record<BarcodeFormat, string> = {
  Ean13: 'EAN-13',
  Ean8: 'EAN-8',
  UpcA: 'UPC-A',
  Code128: 'Code 128',
  Other: 'Diğer',
}

const toOptions = <T extends string>(labels: Record<T, string>) =>
  (Object.entries(labels) as [T, string][]).map(([value, label]) => ({ value, label }))

export const UNIT_OPTIONS = toOptions(UNIT_LABELS)
export const TRACKING_TYPE_OPTIONS = toOptions(TRACKING_TYPE_LABELS)
export const BARCODE_FORMAT_OPTIONS = toOptions(BARCODE_FORMAT_LABELS)

const quantityFormatter = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 3 })

export const formatQuantity = (value: number | null | undefined) =>
  value === null || value === undefined ? '—' : quantityFormatter.format(value)
