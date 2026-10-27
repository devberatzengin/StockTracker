import {
  ActionIcon,
  Button,
  Divider,
  Group,
  Indicator,
  Popover,
  ScrollArea,
  Stack,
  Text,
} from '@mantine/core'
import { IconBell } from '@tabler/icons-react'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import type { AppNotification } from '@/api/types'
import { usePermission } from '@/features/auth/authContext'
import { Permissions } from '@/features/auth/permissions'
import { notificationLink } from '@/features/notifications/notificationLinks'
import { NotificationItem } from '@/features/notifications/NotificationItem'
import {
  useMarkAllRead,
  useMarkRead,
  useNotifications,
  useUnreadCount,
} from '@/features/notifications/notificationsApi'

/** Bell with the unread count (polled every 60 s) and the latest notifications. */
export function NotificationBell() {
  const navigate = useNavigate()
  const canMoveStock = usePermission(Permissions.CanMoveStock)
  const [opened, setOpened] = useState(false)
  const { data: unread } = useUnreadCount()
  const { data: latest } = useNotifications(1, 8, false, opened)
  const markRead = useMarkRead()
  const markAll = useMarkAllRead()
  const count = unread?.count ?? 0

  const open = (n: AppNotification) => {
    setOpened(false)
    if (!n.isRead) markRead.mutate(n.id)
    navigate(notificationLink(n, canMoveStock))
  }

  return (
    <Popover opened={opened} onChange={setOpened} position="bottom-end" width={340} shadow="md">
      <Popover.Target>
        <Indicator
          label={count > 99 ? '99+' : count}
          size={16}
          disabled={count === 0}
          color="red"
          offset={4}
        >
          <ActionIcon
            variant="subtle"
            size="lg"
            aria-label={count > 0 ? `Bildirimler (${count} okunmamış)` : 'Bildirimler'}
            onClick={() => setOpened((o) => !o)}
          >
            <IconBell size={20} />
          </ActionIcon>
        </Indicator>
      </Popover.Target>
      <Popover.Dropdown p={0}>
        <Group justify="space-between" p="xs">
          <Text fw={600} size="sm">
            Bildirimler
          </Text>
          <Button
            size="compact-xs"
            variant="subtle"
            disabled={count === 0}
            onClick={() => markAll.mutate()}
          >
            Tümünü okundu yap
          </Button>
        </Group>
        <Divider />
        <ScrollArea.Autosize mah={360}>
          {latest && latest.items.length > 0 ? (
            <Stack gap={0} p={4}>
              {latest.items.map((n) => (
                <NotificationItem key={n.id} notification={n} onOpen={() => open(n)} />
              ))}
            </Stack>
          ) : (
            <Text size="sm" c="dimmed" p="md">
              Bildirim yok.
            </Text>
          )}
        </ScrollArea.Autosize>
        <Divider />
        <Button
          component={Link}
          to="/notifications"
          variant="subtle"
          fullWidth
          onClick={() => setOpened(false)}
        >
          Tümünü gör
        </Button>
      </Popover.Dropdown>
    </Popover>
  )
}
