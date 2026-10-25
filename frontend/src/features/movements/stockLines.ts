import type { StockLine } from '@/api/types'
import type { LineErrors } from '@/features/movements/lineErrors'
import type { PickedProduct } from '@/features/movements/pickedProduct'

export const MAX_STOCK_LINES = 100

export interface ItemLine {
  key: string
  kind: 'item'
  itemId: string
  code: string
  title: string
  subtitle: string | null
}

export interface QuantityLine {
  key: string
  kind: 'quantity'
  product: PickedProduct | null
  fromLocationId: string | null
  quantity: number | ''
  /** Display-only source path, set when the source was picked outside the editor (scan-and-move). */
  sourcePath?: string
}

export type EditorLine = ItemLine | QuantityLine

export const newQuantityLine = (product: PickedProduct | null = null): QuantityLine => ({
  key: crypto.randomUUID(),
  kind: 'quantity',
  product,
  fromLocationId: null,
  quantity: '',
})

export function toStockLines(lines: EditorLine[]): StockLine[] {
  return lines.map((l) =>
    l.kind === 'item'
      ? { itemId: l.itemId }
      : {
          productId: l.product!.id,
          fromLocationId: l.fromLocationId,
          quantity: Number(l.quantity),
        },
  )
}

/** Client-side checks for out/transfer lines; the backend repeats them and checks balances. */
export function validateStockLines(
  lines: EditorLine[],
  targetLocationId?: string | null,
): LineErrors {
  const errors: LineErrors = { lines: {}, general: [], fields: {} }
  if (lines.length === 0) errors.general.push('En az bir satır ekleyin.')
  if (lines.length > MAX_STOCK_LINES)
    errors.general.push(`Tek işlemde en fazla ${MAX_STOCK_LINES} satır olabilir.`)

  lines.forEach((line, i) => {
    const set = (field: string, message: string) => {
      errors.lines[i] = { ...errors.lines[i], [field]: message }
    }
    if (line.kind === 'item') return
    if (!line.product) set('productId', 'Ürün seçin.')
    else if (line.product.trackingType === 'Serialized')
      set('productId', 'Tekil takipli ürünlerde "Adet ekle" ile adetin kodunu okutun.')
    if (!line.fromLocationId) set('fromLocationId', 'Kaynak lokasyon seçin.')
    else if (targetLocationId && line.fromLocationId === targetLocationId)
      set('fromLocationId', 'Kaynak ve hedef lokasyon aynı olamaz.')
    if (line.quantity === '' || line.quantity <= 0)
      set('quantity', 'Miktar sıfırdan büyük olmalıdır.')
  })

  if (Object.keys(errors.lines).length > 0 && errors.general.length === 0) {
    errors.general.push('İşaretli satırları düzeltin.')
  }
  return errors
}
