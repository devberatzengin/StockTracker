import { Alert, Anchor, Button, Group, Paper, Stack, Table, Text, Title } from '@mantine/core'
import { IconCircleCheck } from '@tabler/icons-react'
import { Link, useParams } from 'react-router'
import { MOVEMENT_TYPE_LABELS } from '@/features/movements/labels'
import { MovementTypeBadge } from '@/features/movements/MovementList'
import { describeMovement } from '@/features/movements/movementText'
import { useMovements } from '@/features/movements/movementsApi'
import { formatDateTime } from '@/lib/dates'

/** Summary after a stock operation: what moved and the codes of new items (labels in phase 3). */
export function MovementResultPage() {
  const { referenceId = '' } = useParams()
  const { data, isPending } = useMovements({ referenceId, pageSize: 100, page: 1 })

  if (isPending) return <Text c="dimmed">Yükleniyor…</Text>

  const movements = [...(data?.items ?? [])].reverse()
  if (movements.length === 0) {
    return <Alert color="orange">Bu işleme ait hareket bulunamadı.</Alert>
  }

  const first = movements[0]
  const createdItems = movements.filter((m) => m.type === 'In' && m.itemId)

  return (
    <Stack maw={860}>
      <Group gap="xs">
        <IconCircleCheck size={28} color="var(--mantine-color-green-6)" />
        <Title order={2}>{MOVEMENT_TYPE_LABELS[first.type]} tamamlandı</Title>
      </Group>
      <Text c="dimmed" size="sm">
        {first.userName} · {formatDateTime(first.occurredAt)} · {data?.totalCount} hareket
      </Text>

      {createdItems.length > 0 && (
        <Paper withBorder p="md">
          <Title order={4} mb="xs">
            Oluşan adetler ({createdItems.length})
          </Title>
          <Table striped withTableBorder>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Kod</Table.Th>
                <Table.Th>Ürün</Table.Th>
                <Table.Th>Seri no</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {createdItems.map((m) => (
                <Table.Tr key={m.id}>
                  <Table.Td ff="monospace">
                    <Anchor component={Link} to={`/items/${m.itemId}`} size="sm">
                      {m.itemCode}
                    </Anchor>
                  </Table.Td>
                  <Table.Td>{m.productName}</Table.Td>
                  <Table.Td>{m.serialNumber ?? '—'}</Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Paper>
      )}

      <Paper withBorder p="md">
        <Title order={4} mb="xs">
          Hareketler
        </Title>
        <Stack gap={6}>
          {movements.map((m) => (
            <Group key={m.id} gap="xs" wrap="nowrap" align="flex-start">
              <MovementTypeBadge movement={m} />
              <Text size="sm">{describeMovement(m)}</Text>
            </Group>
          ))}
        </Stack>
      </Paper>

      <Group>
        <Button component={Link} to="/movements" variant="light">
          Tüm hareketler
        </Button>
        <Button component={Link} to="/scan-move" variant="subtle">
          Tara-taşı
        </Button>
      </Group>
    </Stack>
  )
}
