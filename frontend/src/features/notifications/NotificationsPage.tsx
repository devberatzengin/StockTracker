import { Button, Group, Pagination, Paper, Stack, Switch, Text, Title } from '@mantine/core'
import { useState } from 'react'
import { useNavigate } from 'react-router'
import type { AppNotification } from '@/api/types'
import { usePermission } from '@/features/auth/authContext'
import { Permissions } from '@/features/auth/permissions'
import { notificationLink } from '@/features/notifications/notificationLinks'
import { NotificationItem } from '@/features/notifications/NotificationItem'
import {
  useMarkAllRead,
  useMarkRead,
  useNotifications,
} from '@/features/notifications/notificationsApi'

const PAGE_SIZE = 20

export function NotificationsPage() {
  const navigate = useNavigate()
  const canMoveStock = usePermission(Permissions.CanMoveStock)
  const [page, setPage] = useState(1)
  const [unreadOnly, setUnreadOnly] = useState(false)
  const { data, isPending } = useNotifications(page, PAGE_SIZE, unreadOnly)
  const markRead = useMarkRead()
  const markAll = useMarkAllRead()
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / PAGE_SIZE)) : 1

  const open = (n: AppNotification) => {
    if (!n.isRead) markRead.mutate(n.id)
    navigate(notificationLink(n, canMoveStock))
  }

  return (
    <Stack maw={720}>
      <Group justify="space-between">
        <Title order={2}>Bildirimler</Title>
        <Button variant="light" onClick={() => markAll.mutate()} loading={markAll.isPending}>
          Tümünü okundu yap
        </Button>
      </Group>
      <Switch
        label="Sadece okunmamışlar"
        checked={unreadOnly}
        onChange={(e) => {
          setUnreadOnly(e.currentTarget.checked)
          setPage(1)
        }}
      />
      {isPending ? (
        <Text c="dimmed">Yükleniyor…</Text>
      ) : !data || data.items.length === 0 ? (
        <Text c="dimmed">Bildirim yok.</Text>
      ) : (
        <Paper withBorder p={4}>
          <Stack gap={0}>
            {data.items.map((n) => (
              <NotificationItem key={n.id} notification={n} onOpen={() => open(n)} />
            ))}
          </Stack>
        </Paper>
      )}
      {totalPages > 1 && (
        <Pagination value={page} onChange={setPage} total={totalPages} size="sm" />
      )}
    </Stack>
  )
}
