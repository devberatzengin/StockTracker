import type { AppNotification } from '@/api/types'

/** Where a notification leads: the product, or the assignment lists. */
export function notificationLink(n: AppNotification, canMoveStock: boolean): string {
  if (n.entityType === 'Product') return `/products/${n.entityId}`
  if (n.entityType === 'Assignment') {
    return canMoveStock && n.type === 'AssignmentOverdue' ? '/assignments' : '/my-assignments'
  }
  return '/notifications'
}

export const NOTIFICATION_COLORS: Record<AppNotification['type'], string> = {
  LowStock: 'orange',
  AssignmentDueSoon: 'blue',
  AssignmentOverdue: 'red',
  LedgerMismatch: 'grape',
}
