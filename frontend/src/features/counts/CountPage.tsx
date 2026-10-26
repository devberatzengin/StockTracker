import { Alert, Group, Stack, Text, Title } from '@mantine/core'
import { useParams } from 'react-router'
import { isApiError } from '@/api/client'
import { NotFoundPage } from '@/components/NotFoundPage'
import { usePermission } from '@/features/auth/authContext'
import { Permissions } from '@/features/auth/permissions'
import { CountingScreen } from '@/features/counts/CountingScreen'
import { CountReport } from '@/features/counts/CountReport'
import { CountStatusBadge } from '@/features/counts/CountStatusBadge'
import { useCount } from '@/features/counts/countsApi'
import { ReviewScreen } from '@/features/counts/ReviewScreen'
import { formatDateTime } from '@/lib/dates'

/** One count; the screen follows its status (count → review → report). */
export function CountPage() {
  const { id = '' } = useParams()
  const { data: count, isPending, error } = useCount(id)
  const canApprove = usePermission(Permissions.CanApproveCount)

  if (isApiError(error, 404) || isApiError(error, 400)) return <NotFoundPage />
  if (isPending || !count) return <Text c="dimmed">Yükleniyor…</Text>

  return (
    <Stack>
      <div>
        <Group gap="xs">
          <Title order={2}>Sayım</Title>
          <CountStatusBadge status={count.status} />
        </Group>
        <Text fw={500}>
          {count.locationPath}
          {count.includeChildren ? ' (alt lokasyonlar dahil)' : ''}
        </Text>
        <Text size="sm" c="dimmed">
          {count.startedByName} · {formatDateTime(count.startedAt)}
          {count.note ? ` · ${count.note}` : ''}
        </Text>
      </div>

      {count.status === 'Counting' && <CountingScreen count={count} />}
      {count.status === 'Reviewing' &&
        (canApprove ? (
          <ReviewScreen count={count} />
        ) : (
          <Alert color="orange">Sayım incelemede; onay için depo sorumlusunu bekliyor.</Alert>
        ))}
      {count.status === 'Approved' && <CountReport count={count} />}
      {count.status === 'Cancelled' && (
        <Alert color="gray">Bu sayım iptal edildi; hiçbir hareket oluşturulmadı.</Alert>
      )}
    </Stack>
  )
}
