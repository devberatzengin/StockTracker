import { Paper, SimpleGrid, Stack, Table, Text, Title } from '@mantine/core'
import type { CountDetail } from '@/api/types'
import { lineTitle, quantityText } from '@/features/counts/countFormat'
import { COUNT_RESOLUTION_LABELS } from '@/features/counts/labels'
import { MovementList } from '@/features/movements/MovementList'
import { formatDateTime } from '@/lib/dates'

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <Paper withBorder p="md">
      <Text size="xs" c="dimmed">
        {label}
      </Text>
      <Text fw={700} size="xl">
        {value}
      </Text>
    </Paper>
  )
}

/** Approved count: what was counted, what matched, what was decided and the movements created. */
export function CountReport({ count }: { count: CountDetail }) {
  const counted = count.lines.filter(
    (l) => l.countedQuantity !== null && l.countedQuantity !== undefined,
  )
  const matched = count.lines.filter(
    (l) => l.lineType === 'Expected' && l.countedQuantity === l.expectedQuantity,
  )
  const decided = count.lines.filter((l) => l.resolution)

  return (
    <Stack>
      <Text size="sm" c="dimmed">
        {count.approvedByName} onayladı · {count.approvedAt ? formatDateTime(count.approvedAt) : ''}
      </Text>
      <SimpleGrid cols={{ base: 2, sm: 4 }}>
        <Stat label="Sayılan satır" value={counted.length} />
        <Stat label="Beklenenle aynı" value={matched.length} />
        <Stat label="Beklenmeyen" value={count.progress.unexpectedLines} />
        <Stat label="Karar verilen fark" value={decided.length} />
      </SimpleGrid>

      {decided.length > 0 && (
        <>
          <Title order={4}>Kararlar</Title>
          <Table.ScrollContainer minWidth={640}>
            <Table striped verticalSpacing="xs">
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Lokasyon</Table.Th>
                  <Table.Th>Ürün / adet</Table.Th>
                  <Table.Th>Başlangıç</Table.Th>
                  <Table.Th>Sayılan</Table.Th>
                  <Table.Th>Karar</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {decided.map((l) => (
                  <Table.Tr key={l.id}>
                    <Table.Td>{l.locationName}</Table.Td>
                    <Table.Td>{lineTitle(l)}</Table.Td>
                    <Table.Td>{quantityText(l.expectedQuantity, l.unit)}</Table.Td>
                    <Table.Td>{quantityText(l.countedQuantity, l.unit)}</Table.Td>
                    <Table.Td>{COUNT_RESOLUTION_LABELS[l.resolution!]}</Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </>
      )}

      <Title order={4}>Oluşan hareketler</Title>
      {count.approvalReferenceId ? (
        <MovementList
          filters={{ referenceId: count.approvalReferenceId, pageSize: 100 }}
          emptyText="Hareket oluşmadı."
        />
      ) : (
        <Text c="dimmed">Hareket oluşmadı.</Text>
      )}
    </Stack>
  )
}
