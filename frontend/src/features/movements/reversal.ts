import type { Movement } from '@/api/types'

/** Mirrors the backend rule: quantity In/Out/Transfer and item In/Transfer, once, never a reversal. */
export function canReverse(m: Pick<Movement, 'type' | 'itemId' | 'reversesMovementId' | 'reversedByMovementId'>) {
  if (m.reversesMovementId || m.reversedByMovementId) return false
  return m.type === 'In' || m.type === 'Transfer' || (m.type === 'Out' && !m.itemId)
}
