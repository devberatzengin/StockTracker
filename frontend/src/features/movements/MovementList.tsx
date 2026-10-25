import { Anchor, Badge, Group, Pagination, Paper, Stack, Text, Timeline } from '@mantine/core'
import { useState } from 'react'
import { Link } from 'react-router'
import type { Movement } from '@/api/types'
import { groupByReference } from '@/features/movements/grouping'
import { MOVEMENT_TYPE_COLORS, MOVEMENT_TYPE_LABELS } from '@/features/movements/labels'
import { describeMovement } from '@/features/movements/movementText'
import { useMovements, type MovementFilters } from '@/features/movements/movementsApi'
import { formatDateTime } from '@/lib/dates'

interface Props {
  filters: Omit<MovementFilters, 'page'>
  /** list: one row per movement · grouped: cards per operation · timeline: for a single item. */
  variant?: 'list' | 'grouped' | 'timeline'
  emptyText?: string
}

export function MovementTypeBadge({ movement }: { movement: Pick<Movement, 'type'> }) {
  return (
    <Badge variant="light" color={MOVEMENT_TYPE_COLORS[movement.type]} size="sm">
      {MOVEMENT_TYPE_LABELS[movement.type]}
    </Badge>
  )
}

/** Paged movement history with sentences like "Berat, 200 adet Vida M4'ü Raf A1'den Raf B2'ye taşıdı." */
export function MovementList({ filters, variant = 'list', emptyText = 'Hareket yok.' }: Props) {
  const [page, setPage] = useState(1)
  const pageSize = filters.pageSize ?? 25
  const { data, isPending } = useMovements({ ...filters, page, pageSize })

  if (isPending) {
    return (
      <Text c="dimmed" size="sm">
        Yükleniyor…
      </Text>
    )
  }

  const items = data?.items ?? []
  if (items.length === 0) {
    return (
      <Text c="dimmed" size="sm">
        {emptyText}
      </Text>
    )
  }

  const totalPages = Math.max(1, Math.ceil((data?.totalCount ?? 0) / pageSize))

  return (
    <Stack gap="sm">
      {variant === 'timeline' && (
        <Timeline bulletSize={14} lineWidth={2}>
          {items.map((m) => (
            <Timeline.Item
              key={m.id}
              title={
                <Group gap="xs">
                  <MovementTypeBadge movement={m} />
                  <Text size="xs" c="dimmed">
                    {formatDateTime(m.occurredAt)}
                  </Text>
                </Group>
              }
            >
              <Text size="sm">{describeMovement(m)}</Text>
              {m.note && (
                <Text size="xs" c="dimmed">
                  Not: {m.note}
                </Text>
              )}
            </Timeline.Item>
          ))}
        </Timeline>
      )}

      {variant === 'list' &&
        items.map((m) => (
          <Paper key={m.id} withBorder p="sm">
            <Group gap="xs" mb={4}>
              <MovementTypeBadge movement={m} />
              <Text size="xs" c="dimmed">
                {formatDateTime(m.occurredAt)}
              </Text>
            </Group>
            <Text size="sm">{describeMovement(m)}</Text>
            {m.note && (
              <Text size="xs" c="dimmed">
                Not: {m.note}
              </Text>
            )}
          </Paper>
        ))}

      {variant === 'grouped' &&
        groupByReference(items).map((group) => {
          const first = group.movements[0]
          return (
            <Paper key={`${group.referenceId}-${first.id}`} withBorder p="sm">
              <Group justify="space-between" mb={6} wrap="nowrap">
                <Group gap="xs">
                  <MovementTypeBadge movement={first} />
                  <Text size="sm" fw={600}>
                    {first.userName}
                  </Text>
                  <Text size="xs" c="dimmed">
                    {formatDateTime(first.occurredAt)}
                  </Text>
                </Group>
                <Anchor component={Link} to={`/movements/ref/${group.referenceId}`} size="xs">
                  İşlemin tamamı
                </Anchor>
              </Group>
              <Stack gap={2}>
                {group.movements.map((m) => (
                  <Text key={m.id} size="sm">
                    {describeMovement(m)}
                  </Text>
                ))}
              </Stack>
              {first.note && (
                <Text size="xs" c="dimmed" mt={4}>
                  Not: {first.note}
                </Text>
              )}
            </Paper>
          )
        })}

      {totalPages > 1 && (
        <Pagination value={page} onChange={setPage} total={totalPages} size="sm" />
      )}
    </Stack>
  )
}
