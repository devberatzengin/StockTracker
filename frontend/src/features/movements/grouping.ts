import type { Movement } from '@/api/types'

export interface MovementGroup {
  referenceId: string
  movements: Movement[]
}

/** Groups consecutive movements of the same operation (ReferenceId), keeping the list order. */
export function groupByReference(movements: Movement[]): MovementGroup[] {
  const groups: MovementGroup[] = []
  for (const movement of movements) {
    const last = groups.at(-1)
    if (last && last.referenceId === movement.referenceId) {
      last.movements.push(movement)
    } else {
      groups.push({ referenceId: movement.referenceId, movements: [movement] })
    }
  }
  return groups
}
