import { apiClient, isApiError } from '@/api/client'
import type { BarcodeFormat, ScanResult } from '@/api/types'

export const lookupScan = (input: string) =>
  apiClient.get<ScanResult>(`/scan/${encodeURIComponent(input.trim())}`)

/** Detail page for a scan result. */
export function scanResultPath(result: ScanResult) {
  switch (result.type) {
    case 'product':
      return `/products/${result.id}`
    case 'location':
      return `/locations/${result.id}`
    default:
      return `/${result.type}s/${result.id}`
  }
}

export interface CreateProductSuggestion {
  barcode: string
  format: BarcodeFormat
}

/** For a 404 on a valid EAN/UPC: the barcode a new product could be created with. */
export function createProductSuggestion(error: unknown): CreateProductSuggestion | null {
  if (!isApiError(error, 404)) return null
  const problem = error.problem as Record<string, unknown>
  return problem.suggestCreateProduct === true && typeof problem.barcode === 'string'
    ? {
        barcode: problem.barcode,
        format: (problem.barcodeFormat as BarcodeFormat | undefined) ?? 'Ean13',
      }
    : null
}

export function newProductWithBarcodePath({ barcode, format }: CreateProductSuggestion) {
  return `/products?${new URLSearchParams({ newBarcode: barcode, barcodeFormat: format })}`
}
