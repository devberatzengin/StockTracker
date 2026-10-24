import {
  Badge,
  Button,
  Group,
  Pagination,
  Select,
  Stack,
  Switch,
  Table,
  Text,
  TextInput,
  Title,
} from '@mantine/core'
import { useDebouncedValue, useDisclosure } from '@mantine/hooks'
import { IconPlus, IconSearch } from '@tabler/icons-react'
import { useState } from 'react'
import { useNavigate } from 'react-router'
import type { TrackingType } from '@/api/types'
import { usePermission } from '@/features/auth/authContext'
import { Permissions } from '@/features/auth/permissions'
import { useCategoryOptions, useProducts } from '@/features/catalog/catalogApi'
import { TRACKING_TYPE_LABELS, TRACKING_TYPE_OPTIONS, UNIT_LABELS } from '@/features/catalog/labels'
import { ProductFormModal } from '@/features/catalog/ProductFormModal'

const PAGE_SIZE = 25

export function ProductsPage() {
  const navigate = useNavigate()
  const canManage = usePermission(Permissions.CanManageCatalog)
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [categoryId, setCategoryId] = useState<string | null>(null)
  const [trackingType, setTrackingType] = useState<TrackingType | null>(null)
  const [includeInactive, setIncludeInactive] = useState(false)
  const [debouncedSearch] = useDebouncedValue(search.trim(), 300)
  const [formOpened, form] = useDisclosure()

  const { data: categories } = useCategoryOptions()
  const { data, isPending } = useProducts({
    page,
    pageSize: PAGE_SIZE,
    search: debouncedSearch,
    categoryId,
    trackingType,
    includeInactive,
  })

  // Any filter change goes back to the first page.
  const filter =
    <T,>(set: (value: T) => void) =>
    (value: T) => {
      set(value)
      setPage(1)
    }

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / PAGE_SIZE)) : 1

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>Ürünler</Title>
        {canManage && (
          <Button leftSection={<IconPlus size={18} />} onClick={form.open}>
            Yeni ürün
          </Button>
        )}
      </Group>

      <Group align="flex-end">
        <TextInput
          placeholder="Ad, kod veya barkod ara"
          leftSection={<IconSearch size={16} />}
          value={search}
          onChange={(event) => filter(setSearch)(event.currentTarget.value)}
          w={280}
        />
        <Select
          placeholder="Tüm kategoriler"
          aria-label="Kategori filtresi"
          data={(categories?.items ?? []).map((c) => ({ value: c.id, label: c.name }))}
          value={categoryId}
          onChange={filter(setCategoryId)}
          clearable
          searchable
          w={220}
        />
        <Select
          placeholder="Tüm takip tipleri"
          aria-label="Takip tipi filtresi"
          data={TRACKING_TYPE_OPTIONS}
          value={trackingType}
          onChange={(value) => filter(setTrackingType)(value as TrackingType | null)}
          clearable
          w={200}
        />
        <Switch
          label="Pasifleri göster"
          checked={includeInactive}
          onChange={(event) => filter(setIncludeInactive)(event.currentTarget.checked)}
          mb={8}
        />
      </Group>

      <Table.ScrollContainer minWidth={820}>
        <Table striped highlightOnHover withTableBorder>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Kod</Table.Th>
              <Table.Th>Ad</Table.Th>
              <Table.Th>Kategori</Table.Th>
              <Table.Th>Takip tipi</Table.Th>
              <Table.Th>Birim</Table.Th>
              <Table.Th>Barkod</Table.Th>
              <Table.Th>Durum</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {isPending || data?.items.length === 0 ? (
              <Table.Tr>
                <Table.Td colSpan={7}>
                  <Text c="dimmed" size="sm">
                    {isPending ? 'Yükleniyor…' : 'Ürün bulunamadı.'}
                  </Text>
                </Table.Td>
              </Table.Tr>
            ) : (
              data?.items.map((product) => (
                <Table.Tr
                  key={product.id}
                  onClick={() => navigate(`/products/${product.id}`)}
                  style={{ cursor: 'pointer' }}
                >
                  <Table.Td ff="monospace">{product.code}</Table.Td>
                  <Table.Td>
                    <Group gap={6}>
                      <Text size="sm" fw={500}>
                        {product.name}
                      </Text>
                      {product.isAssignable && (
                        <Badge size="xs" variant="light" color="grape">
                          Zimmetlenebilir
                        </Badge>
                      )}
                    </Group>
                  </Table.Td>
                  <Table.Td>{product.categoryName}</Table.Td>
                  <Table.Td>{TRACKING_TYPE_LABELS[product.trackingType]}</Table.Td>
                  <Table.Td>{UNIT_LABELS[product.unit]}</Table.Td>
                  <Table.Td ff="monospace">
                    {product.barcodes[0] ?? '—'}
                    {product.barcodes.length > 1 && (
                      <Text span size="xs" c="dimmed">
                        {' '}
                        +{product.barcodes.length - 1}
                      </Text>
                    )}
                  </Table.Td>
                  <Table.Td>
                    <Badge variant="dot" color={product.isActive ? 'green' : 'gray'}>
                      {product.isActive ? 'Aktif' : 'Pasif'}
                    </Badge>
                  </Table.Td>
                </Table.Tr>
              ))
            )}
          </Table.Tbody>
        </Table>
      </Table.ScrollContainer>

      {totalPages > 1 && <Pagination value={page} onChange={setPage} total={totalPages} />}

      {canManage && <ProductFormModal opened={formOpened} onClose={form.close} />}
    </Stack>
  )
}
