import {
  Anchor,
  Badge,
  Group,
  Pagination,
  Paper,
  Select,
  SimpleGrid,
  Stack,
  Table,
  Text,
} from '@mantine/core'
import { useState } from 'react'
import { Link } from 'react-router'
import type { ItemStatus, ProductDto } from '@/api/types'
import { formatQuantity, UNIT_LABELS } from '@/features/catalog/labels'
import {
  ITEM_STATUS_COLORS,
  ITEM_STATUS_LABELS,
  ITEM_STATUS_OPTIONS,
} from '@/features/movements/labels'
import { useItems, useProductStock } from '@/features/movements/movementsApi'

export function ItemStatusBadge({ status }: { status: ItemStatus }) {
  return (
    <Badge variant="light" color={ITEM_STATUS_COLORS[status]}>
      {ITEM_STATUS_LABELS[status]}
    </Badge>
  )
}

/** Quantity products: quantity per location path. Serialized products: counts per status and per location. */
export function ProductStockTab({ product }: { product: ProductDto }) {
  const { data: stock, isPending } = useProductStock(product.id)
  const unit = UNIT_LABELS[product.unit].toLocaleLowerCase('tr-TR')

  if (isPending || !stock)
    return (
      <Text c="dimmed" size="sm">
        Yükleniyor…
      </Text>
    )

  return (
    <Stack>
      <Text size="sm">
        Toplam:{' '}
        <b>
          {formatQuantity(stock.totalQuantity)} {unit}
        </b>
        {product.minStockLevel !== null && product.minStockLevel !== undefined && (
          <Text span c={stock.totalQuantity < product.minStockLevel ? 'red' : 'dimmed'} size="sm">
            {' '}
            · minimum {formatQuantity(product.minStockLevel)}
          </Text>
        )}
      </Text>

      {stock.statusCounts.length > 0 && (
        <SimpleGrid cols={{ base: 2, sm: 4 }}>
          {stock.statusCounts.map((s) => (
            <Paper key={s.status} withBorder p="sm">
              <Text size="xs" c="dimmed">
                {ITEM_STATUS_LABELS[s.status]}
              </Text>
              <Text size="xl" fw={600}>
                {s.count}
              </Text>
            </Paper>
          ))}
        </SimpleGrid>
      )}

      {stock.locations.length === 0 ? (
        <Text size="sm" c="dimmed">
          Hiçbir lokasyonda stok yok.
        </Text>
      ) : (
        <Table striped withTableBorder>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Lokasyon</Table.Th>
              <Table.Th ta="right">
                {product.trackingType === 'Serialized' ? 'Adet' : 'Miktar'}
              </Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {stock.locations.map((l) => (
              <Table.Tr key={l.locationId}>
                <Table.Td>
                  <Anchor component={Link} to={`/locations/${l.locationId}`} size="sm">
                    {l.locationPath}
                  </Anchor>
                </Table.Td>
                <Table.Td ta="right">{formatQuantity(l.quantity)}</Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      )}
    </Stack>
  )
}

export function ProductItemsTab({ productId }: { productId: string }) {
  const [page, setPage] = useState(1)
  const [status, setStatus] = useState<ItemStatus | null>(null)
  const { data, isPending } = useItems({ productId, status, page, pageSize: 25 })
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / 25)) : 1

  return (
    <Stack>
      <Group>
        <Select
          placeholder="Tüm durumlar"
          aria-label="Durum filtresi"
          data={ITEM_STATUS_OPTIONS}
          value={status}
          onChange={(value) => {
            setStatus(value as ItemStatus | null)
            setPage(1)
          }}
          clearable
          w={200}
        />
      </Group>
      <Table.ScrollContainer minWidth={560}>
        <Table striped withTableBorder>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Kod</Table.Th>
              <Table.Th>Seri no</Table.Th>
              <Table.Th>Durum</Table.Th>
              <Table.Th>Konum</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {isPending || data?.items.length === 0 ? (
              <Table.Tr>
                <Table.Td colSpan={4}>
                  <Text size="sm" c="dimmed">
                    {isPending ? 'Yükleniyor…' : 'Adet yok.'}
                  </Text>
                </Table.Td>
              </Table.Tr>
            ) : (
              data?.items.map((item) => (
                <Table.Tr key={item.id}>
                  <Table.Td ff="monospace">
                    <Anchor component={Link} to={`/items/${item.id}`} size="sm">
                      {item.code}
                    </Anchor>
                  </Table.Td>
                  <Table.Td>{item.serialNumber ?? '—'}</Table.Td>
                  <Table.Td>
                    <ItemStatusBadge status={item.status} />
                  </Table.Td>
                  <Table.Td>{item.locationPath ?? '—'}</Table.Td>
                </Table.Tr>
              ))
            )}
          </Table.Tbody>
        </Table>
      </Table.ScrollContainer>
      {totalPages > 1 && (
        <Pagination value={page} onChange={setPage} total={totalPages} size="sm" />
      )}
    </Stack>
  )
}
