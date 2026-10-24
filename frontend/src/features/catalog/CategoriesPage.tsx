import {
  ActionIcon,
  Badge,
  Button,
  Group,
  Menu,
  Pagination,
  Stack,
  Switch,
  Table,
  Text,
  TextInput,
  Title,
} from '@mantine/core'
import { useDebouncedValue, useDisclosure } from '@mantine/hooks'
import { modals } from '@mantine/modals'
import { notifications } from '@mantine/notifications'
import { IconArchive, IconDots, IconPencil, IconPlus, IconSearch } from '@tabler/icons-react'
import { useState } from 'react'
import type { CategoryDto } from '@/api/types'
import { useCategories, useDeactivateCategory } from '@/features/catalog/catalogApi'
import { CategoryFormModal } from '@/features/catalog/CategoryFormModal'

const PAGE_SIZE = 20

export function CategoriesPage() {
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [includeInactive, setIncludeInactive] = useState(false)
  const [debouncedSearch] = useDebouncedValue(search.trim(), 300)
  const { data, isPending } = useCategories({
    page,
    pageSize: PAGE_SIZE,
    search: debouncedSearch,
    includeInactive,
  })
  const deactivate = useDeactivateCategory()

  const [formOpened, form] = useDisclosure()
  const [editing, setEditing] = useState<CategoryDto | undefined>()

  const openForm = (category?: CategoryDto) => {
    setEditing(category)
    form.open()
  }

  const confirmDeactivate = (category: CategoryDto) =>
    modals.openConfirmModal({
      title: 'Kategoriyi pasifleştir',
      children: (
        <Text size="sm">
          <b>{category.name}</b> pasifleştirilsin mi? Pasif kategoriye yeni ürün eklenemez.
        </Text>
      ),
      labels: { confirm: 'Pasifleştir', cancel: 'Vazgeç' },
      confirmProps: { color: 'red' },
      onConfirm: () =>
        deactivate.mutate(category.id, {
          onSuccess: () =>
            notifications.show({ color: 'green', message: 'Kategori pasifleştirildi.' }),
        }),
    })

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / PAGE_SIZE)) : 1

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>Kategoriler</Title>
        <Button leftSection={<IconPlus size={18} />} onClick={() => openForm()}>
          Yeni kategori
        </Button>
      </Group>

      <Group>
        <TextInput
          placeholder="Kategori ara"
          leftSection={<IconSearch size={16} />}
          value={search}
          onChange={(event) => {
            setSearch(event.currentTarget.value)
            setPage(1)
          }}
          w={280}
        />
        <Switch
          label="Pasifleri göster"
          checked={includeInactive}
          onChange={(event) => {
            setIncludeInactive(event.currentTarget.checked)
            setPage(1)
          }}
        />
      </Group>

      <Table.ScrollContainer minWidth={480}>
        <Table striped highlightOnHover withTableBorder>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Ad</Table.Th>
              <Table.Th>Aktif ürün</Table.Th>
              <Table.Th>Durum</Table.Th>
              <Table.Th w={48} />
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {isPending || data?.items.length === 0 ? (
              <Table.Tr>
                <Table.Td colSpan={4}>
                  <Text c="dimmed" size="sm">
                    {isPending ? 'Yükleniyor…' : 'Kategori bulunamadı.'}
                  </Text>
                </Table.Td>
              </Table.Tr>
            ) : (
              data?.items.map((category) => (
                <Table.Tr key={category.id}>
                  <Table.Td>{category.name}</Table.Td>
                  <Table.Td>{category.activeProductCount}</Table.Td>
                  <Table.Td>
                    <Badge variant="dot" color={category.isActive ? 'green' : 'gray'}>
                      {category.isActive ? 'Aktif' : 'Pasif'}
                    </Badge>
                  </Table.Td>
                  <Table.Td>
                    <Menu position="bottom-end" withinPortal>
                      <Menu.Target>
                        <ActionIcon
                          variant="subtle"
                          color="gray"
                          aria-label={`${category.name} işlemleri`}
                        >
                          <IconDots size={18} />
                        </ActionIcon>
                      </Menu.Target>
                      <Menu.Dropdown>
                        <Menu.Item
                          leftSection={<IconPencil size={16} />}
                          onClick={() => openForm(category)}
                        >
                          Düzenle
                        </Menu.Item>
                        {category.isActive && (
                          <Menu.Item
                            color="red"
                            leftSection={<IconArchive size={16} />}
                            disabled={category.activeProductCount > 0}
                            onClick={() => confirmDeactivate(category)}
                          >
                            Pasifleştir
                          </Menu.Item>
                        )}
                      </Menu.Dropdown>
                    </Menu>
                  </Table.Td>
                </Table.Tr>
              ))
            )}
          </Table.Tbody>
        </Table>
      </Table.ScrollContainer>

      {totalPages > 1 && <Pagination value={page} onChange={setPage} total={totalPages} />}

      <CategoryFormModal opened={formOpened} onClose={form.close} category={editing} />
    </Stack>
  )
}
