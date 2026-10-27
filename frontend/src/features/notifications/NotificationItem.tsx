import { Badge, Group, Text, UnstyledButton } from '@mantine/core'
import type { AppNotification } from '@/api/types'
import { NOTIFICATION_COLORS } from '@/features/notifications/notificationLinks'
import { formatDateTime } from '@/lib/dates'

/** One notification row: unread ones are bold with a coloured dot; resolved ones say so. */
export function NotificationItem({
  notification: n,
  onOpen,
}: {
  notification: AppNotification
  onOpen: () => void
}) {
  const unread = !n.isRead && !n.resolvedAt
  return (
    <UnstyledButton
      onClick={onOpen}
      w="100%"
      p="xs"
      style={{ borderRadius: 4 }}
      data-unread={unread}
    >
      <Group gap="xs" wrap="nowrap" align="flex-start">
        <Badge
          size="xs"
          circle
          color={unread ? NOTIFICATION_COLORS[n.type] : 'gray'}
          variant={unread ? 'filled' : 'outline'}
          mt={4}
        >
          {' '}
        </Badge>
        <div style={{ minWidth: 0 }}>
          <Text size="sm" fw={unread ? 600 : 400} lineClamp={1}>
            {n.title}
          </Text>
          <Text size="xs" c="dimmed" lineClamp={2}>
            {n.message}
          </Text>
          <Group gap={6} mt={2}>
            <Text size="xs" c="dimmed">
              {formatDateTime(n.createdAt)}
            </Text>
            {n.resolvedAt && (
              <Badge size="xs" color="green" variant="light">
                Çözüldü
              </Badge>
            )}
          </Group>
        </div>
      </Group>
    </UnstyledButton>
  )
}
