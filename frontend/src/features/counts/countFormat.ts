import type { CountLine, Unit } from '@/api/types'
import { formatQuantity, UNIT_LABELS } from '@/features/catalog/labels'

/** "ITM-7F3K9Q · Laptop" for items, the product name for quantity lines. */
export const lineTitle = (line: Pick<CountLine, 'itemCode' | 'productName'>) =>
  line.itemCode ? `${line.itemCode} · ${line.productName}` : line.productName

/** "185 adet"; dash when there is nothing. */
export const quantityText = (value: number | null | undefined, unit: Unit) =>
  value === null || value === undefined
    ? '—'
    : `${formatQuantity(value)} ${UNIT_LABELS[unit].toLocaleLowerCase('tr-TR')}`

/** "+5" / "−15" with a real minus sign. */
export const differenceText = (value: number) =>
  value > 0 ? `+${formatQuantity(value)}` : value < 0 ? `−${formatQuantity(-value)}` : '0'
