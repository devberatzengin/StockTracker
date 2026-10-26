import type { AssignmentStatus, ReturnCondition } from '@/api/types'

export const ASSIGNMENT_STATUS_LABELS: Record<AssignmentStatus, string> = {
  Open: 'Açık',
  Overdue: 'Gecikmiş',
  Returned: 'İade edildi',
}

export const ASSIGNMENT_STATUS_COLORS: Record<AssignmentStatus, string> = {
  Open: 'blue',
  Overdue: 'red',
  Returned: 'gray',
}

export const RETURN_CONDITION_LABELS: Record<ReturnCondition, string> = {
  Good: 'Sağlam',
  Faulty: 'Arızalı',
}
