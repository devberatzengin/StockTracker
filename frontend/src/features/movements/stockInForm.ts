import type { LineErrors } from '@/features/movements/lineErrors'
import type { PickedProduct } from '@/features/movements/pickedProduct'

export const MAX_LINES = 100
export const MAX_ITEMS_PER_LINE = 100

export interface InLine {
  key: string
  product: PickedProduct | null
  quantity: number | ''
  serialNumbers: string[]
}

export const newInLine = (): InLine => ({
  key: crypto.randomUUID(),
  product: null,
  quantity: '',
  serialNumbers: [],
})

export const emptyErrors: LineErrors = { lines: {}, general: [], fields: {} }

/** Client-side checks mirroring the backend, so most mistakes show before submitting. */
export function validateInLines(target: string | null, lines: InLine[]): LineErrors {
  const errors: LineErrors = { lines: {}, general: [], fields: {} }
  if (!target) errors.fields.toLocationId = 'Hedef lokasyon seçin.'
  if (lines.length === 0) errors.general.push('En az bir satır girin.')
  if (lines.length > MAX_LINES)
    errors.general.push(`Tek işlemde en fazla ${MAX_LINES} satır olabilir.`)

  lines.forEach((line, i) => {
    const set = (field: string, message: string) => {
      errors.lines[i] = { ...errors.lines[i], [field]: message }
    }
    if (!line.product) {
      set('productId', 'Ürün seçin.')
      return
    }
    if (line.quantity === '' || line.quantity <= 0) {
      set('quantity', 'Miktar sıfırdan büyük olmalıdır.')
    } else if (line.product.trackingType === 'Serialized') {
      if (!Number.isInteger(line.quantity) || line.quantity > MAX_ITEMS_PER_LINE) {
        set('quantity', `Adet 1 ile ${MAX_ITEMS_PER_LINE} arasında tam sayı olmalıdır.`)
      } else if (line.serialNumbers.length > line.quantity) {
        set('serialNumbers', 'Seri numarası sayısı adet sayısından fazla olamaz.')
      }
    }
  })

  if (Object.keys(errors.lines).length > 0 && errors.general.length === 0) {
    errors.general.push('İşaretli satırları düzeltin.')
  }
  return errors
}

export const hasErrors = (e: LineErrors) =>
  e.general.length > 0 || Object.keys(e.fields).length > 0 || Object.keys(e.lines).length > 0

/** Pasted lists ("SN-1, SN-2 SN-3") become separate serial numbers; duplicates are dropped. */
export function splitSerialNumbers(values: string[]): string[] {
  return [
    ...new Set(
      values
        .flatMap((v) => v.split(/[,;\s]+/))
        .map((v) => v.trim())
        .filter(Boolean),
    ),
  ]
}
