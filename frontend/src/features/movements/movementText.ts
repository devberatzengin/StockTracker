import type { Movement } from '@/api/types'
import { formatQuantity, UNIT_LABELS } from '@/features/catalog/labels'
import { ITEM_STATUS_LABELS, MOVEMENT_REASON_LABELS } from '@/features/movements/labels'
import { ablative, accusative, dative } from '@/lib/turkishSuffix'

type MovementText = Pick<
  Movement,
  | 'type'
  | 'userName'
  | 'productName'
  | 'unit'
  | 'quantity'
  | 'itemCode'
  | 'fromLocationName'
  | 'toLocationName'
  | 'reason'
  | 'fromStatus'
  | 'toStatus'
>

/** The moved thing, ready for its accusative suffix: "200 adet Vida M4", "ITM-7F3K9Q kodlu Laptop". */
function object(m: MovementText) {
  return m.itemCode
    ? `${m.itemCode} kodlu ${accusative(m.productName)}`
    : `${formatQuantity(m.quantity)} ${UNIT_LABELS[m.unit].toLocaleLowerCase('tr-TR')} ${accusative(m.productName)}`
}

const place = (name: string | null | undefined) => name ?? 'bilinmeyen lokasyon'

/**
 * One history line in plain Turkish, e.g.
 * "Berat, 200 adet Vida M4'ü Raf A1'den Raf B2'ye taşıdı."
 */
export function describeMovement(m: MovementText): string {
  const who = m.userName
  switch (m.type) {
    case 'In':
      return `${who}, ${object(m)} ${dative(place(m.toLocationName))} ekledi.`
    case 'Out': {
      const reason = m.reason
        ? ` (${MOVEMENT_REASON_LABELS[m.reason].toLocaleLowerCase('tr-TR')})`
        : ''
      return `${who}, ${object(m)} ${ablative(place(m.fromLocationName))} çıkardı${reason}.`
    }
    case 'Transfer':
      return `${who}, ${object(m)} ${ablative(place(m.fromLocationName))} ${dative(place(m.toLocationName))} taşıdı.`
    case 'Adjustment':
      return m.toLocationName
        ? `${who}, sayım farkı olarak ${object(m)} ${dative(m.toLocationName)} ekledi.`
        : `${who}, sayım farkı olarak ${object(m)} ${ablative(place(m.fromLocationName))} düştü.`
    case 'Assign':
      return `${who}, ${object(m)} zimmetledi.`
    case 'Return':
      return `${who}, ${object(m)} iade alıp ${dative(place(m.toLocationName))} koydu.`
    case 'StatusChange':
      return m.fromStatus && m.toStatus
        ? `${who}, ${object(m)} "${ITEM_STATUS_LABELS[m.fromStatus]}" durumundan "${ITEM_STATUS_LABELS[m.toStatus]}" durumuna aldı.`
        : `${who}, ${object(m)} durumunu değiştirdi.`
    default:
      return `${who}, ${object(m)} için işlem yaptı.`
  }
}
