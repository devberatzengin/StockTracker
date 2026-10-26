import type { ItemDetail } from '@/api/types'

/** Why an item cannot be assigned, or null. Mirrors the server: state actions + assignable product. */
export function canAssign(item: ItemDetail): string | null {
  if (!item.productIsAssignable) return `${item.productName} zimmetlenebilir bir ürün değil.`
  if (!item.availableActions.includes('Assign')) {
    return item.openAssignment
      ? `${item.code} zaten ${item.openAssignment.assigneeName} adına zimmetli. Önce iade alın.`
      : `${item.code} şu anki durumunda zimmetlenemez.`
  }
  return null
}

/** Why an item cannot be returned, or null. */
export function canReturn(item: ItemDetail): string | null {
  if (!item.availableActions.includes('Return') || !item.openAssignment) {
    return `${item.code} zimmetli değil; iade alınacak bir zimmet yok.`
  }
  return null
}
