import type { ItemStatus, MovementReason, MovementType } from '@/api/types'

export const MOVEMENT_TYPE_LABELS: Record<MovementType, string> = {
  In: 'Giriş',
  Out: 'Çıkış',
  Transfer: 'Transfer',
  Adjustment: 'Sayım düzeltmesi',
  Assign: 'Zimmet',
  Return: 'İade',
  StatusChange: 'Durum değişikliği',
}

export const MOVEMENT_TYPE_COLORS: Record<MovementType, string> = {
  In: 'green',
  Out: 'red',
  Transfer: 'blue',
  Adjustment: 'orange',
  Assign: 'grape',
  Return: 'teal',
  StatusChange: 'gray',
}

export const MOVEMENT_REASON_LABELS: Record<MovementReason, string> = {
  Consumed: 'Tüketildi',
  Damaged: 'Hasarlı',
  Lost: 'Kayıp',
  Other: 'Diğer',
}

export const ITEM_STATUS_LABELS: Record<ItemStatus, string> = {
  InStock: 'Stokta',
  Assigned: 'Zimmetli',
  InRepair: 'Tamirde',
  Disposed: 'Hurdaya ayrıldı',
}

export const ITEM_STATUS_COLORS: Record<ItemStatus, string> = {
  InStock: 'green',
  Assigned: 'grape',
  InRepair: 'orange',
  Disposed: 'gray',
}

const toOptions = <T extends string>(labels: Record<T, string>) =>
  (Object.entries(labels) as [T, string][]).map(([value, label]) => ({ value, label }))

export const MOVEMENT_TYPE_OPTIONS = toOptions(MOVEMENT_TYPE_LABELS)
export const MOVEMENT_REASON_OPTIONS = toOptions(MOVEMENT_REASON_LABELS)
export const ITEM_STATUS_OPTIONS = toOptions(ITEM_STATUS_LABELS)
