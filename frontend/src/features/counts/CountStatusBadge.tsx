import { Badge } from '@mantine/core'
import type { CountStatus } from '@/api/types'
import { COUNT_STATUS_COLORS, COUNT_STATUS_LABELS } from '@/features/counts/labels'

export function CountStatusBadge({ status }: { status: CountStatus }) {
  return (
    <Badge variant="light" color={COUNT_STATUS_COLORS[status]}>
      {COUNT_STATUS_LABELS[status]}
    </Badge>
  )
}
