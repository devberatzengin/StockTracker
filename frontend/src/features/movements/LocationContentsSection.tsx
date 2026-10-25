import { Anchor, Group, Stack, Switch, Table, Text, Title } from '@mantine/core'
import { useState } from 'react'
import { Link } from 'react-router'
import { formatQuantity, UNIT_LABELS } from '@/features/catalog/labels'
import { useLocationContents } from '@/features/movements/movementsApi'
import { ItemStatusBadge } from '@/features/movements/ProductStockTabs'

/** What a location holds: balances and items, optionally including its whole subtree. */
export function LocationContentsSection({ locationId }: { locationId: string }) {
  const [includeChildren, setIncludeChildren] = useState(false)
  const { data, isPending } = useLocationContents(locationId, includeChildren)
  const empty = data && data.balances.length === 0 && data.items.length === 0

  return (
    <Stack gap="xs">
      <Group justify="space-between">
        <Title order={4}>İçerik</Title>
        <Switch
          label="Alt lokasyonlar dahil"
          checked={includeChildren}
          onChange={(event) => setIncludeChildren(event.currentTarget.checked)}
        />
      </Group>

      {isPending ? (
        <Text size="sm" c="dimmed">
          Yükleniyor…
        </Text>
      ) : empty ? (
        <Text size="sm" c="dimmed">
          Bu lokasyonda stok yok.
        </Text>
      ) : (
        <Table.ScrollContainer minWidth={560}>
          <Table striped withTableBorder>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Ürün</Table.Th>
                <Table.Th>Adet / seri no</Table.Th>
                {includeChildren && <Table.Th>Lokasyon</Table.Th>}
                <Table.Th ta="right">Miktar</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {data?.balances.map((b) => (
                <Table.Tr key={`${b.productId}-${b.locationId}`}>
                  <Table.Td>
                    <Anchor component={Link} to={`/products/${b.productId}`} size="sm">
                      {b.productName}
                    </Anchor>
                  </Table.Td>
                  <Table.Td>—</Table.Td>
                  {includeChildren && <Table.Td>{b.locationPath}</Table.Td>}
                  <Table.Td ta="right">
                    {formatQuantity(b.quantity)} {UNIT_LABELS[b.unit].toLocaleLowerCase('tr-TR')}
                  </Table.Td>
                </Table.Tr>
              ))}
              {data?.items.map((i) => (
                <Table.Tr key={i.itemId}>
                  <Table.Td>
                    <Anchor component={Link} to={`/products/${i.productId}`} size="sm">
                      {i.productName}
                    </Anchor>
                  </Table.Td>
                  <Table.Td>
                    <Group gap={6} wrap="nowrap">
                      <Anchor component={Link} to={`/items/${i.itemId}`} size="sm" ff="monospace">
                        {i.code}
                      </Anchor>
                      {i.serialNumber && (
                        <Text size="xs" c="dimmed">
                          {i.serialNumber}
                        </Text>
                      )}
                      {i.status !== 'InStock' && <ItemStatusBadge status={i.status} />}
                    </Group>
                  </Table.Td>
                  {includeChildren && <Table.Td>{i.locationPath}</Table.Td>}
                  <Table.Td ta="right">1 adet</Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      )}
    </Stack>
  )
}
