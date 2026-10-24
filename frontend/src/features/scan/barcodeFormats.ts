import { BarcodeFormat as ZxingFormat } from '@zxing/library'
import type { BarcodeFormat } from '@/api/types'

/** Formats the camera reader looks for. */
export const SCANNED_FORMATS = [
  ZxingFormat.QR_CODE,
  ZxingFormat.EAN_13,
  ZxingFormat.EAN_8,
  ZxingFormat.UPC_A,
  ZxingFormat.UPC_E,
  ZxingFormat.CODE_128,
]

/** Maps a camera result format to the product barcode format; null for QR codes and manual entry. */
export function toBarcodeFormat(format: ZxingFormat | null): BarcodeFormat | null {
  switch (format) {
    case ZxingFormat.EAN_13:
      return 'Ean13'
    case ZxingFormat.EAN_8:
      return 'Ean8'
    case ZxingFormat.UPC_A:
      return 'UpcA'
    case ZxingFormat.CODE_128:
      return 'Code128'
    case ZxingFormat.QR_CODE:
    case null:
      return null
    default:
      return 'Other'
  }
}
