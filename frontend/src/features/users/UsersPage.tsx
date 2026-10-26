import {
  ActionIcon,
  Anchor,
  Badge,
  Button,
  Group,
  Menu,
  Pagination,
  Stack,
  Table,
  Text,
  TextInput,
  Title,
} from '@mantine/core'
import { useDebouncedValue, useDisclosure } from '@mantine/hooks'
import { modals } from '@mantine/modals'
import { notifications } from '@mantine/notifications'
import {
  IconDots,
  IconKey,
  IconPencil,
  IconPlus,
  IconSearch,
  IconUserOff,
} from '@tabler/icons-react'
import { useState } from 'react'
import { Link } from 'react-router'
import type { UserDto } from '@/api/types'
import { ResetPasswordModal } from '@/features/users/ResetPasswordModal'
import { roleLabel } from '@/features/users/roles'
import { UserFormModal } from '@/features/users/UserFormModal'
import { useDeactivateUser, useUsers } from '@/features/users/usersApi'

const PAGE_SIZE = 20

export function UsersPage() {
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [debouncedSearch] = useDebouncedValue(search.trim(), 300)
  const { data, isPending } = useUsers({ page, pageSize: PAGE_SIZE, search: debouncedSearch })
  const deactivate = useDeactivateUser()

  const [formOpened, form] = useDisclosure()
  const [editing, setEditing] = useState<UserDto | undefined>()
  const [resetting, setResetting] = useState<UserDto | null>(null)

  const openCreate = () => {
    setEditing(undefined)
    form.open()
  }

  const openEdit = (user: UserDto) => {
    setEditing(user)
    form.open()
  }

  const confirmDeactivate = (user: UserDto) =>
    modals.openConfirmModal({
      title: 'Kullanıcıyı pasifleştir',
      children: (
        <Text size="sm">
          <b>{user.fullName}</b> pasifleştirilsin mi? Kullanıcı giriş yapamaz ve açık oturumları
          kapatılır.
        </Text>
      ),
      labels: { confirm: 'Pasifleştir', cancel: 'Vazgeç' },
      confirmProps: { color: 'red' },
      onConfirm: () =>
        deactivate.mutate(user.id, {
          onSuccess: () =>
            notifications.show({ color: 'green', message: 'Kullanıcı pasifleştirildi.' }),
        }),
    })

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / PAGE_SIZE)) : 1

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>Kullanıcılar</Title>
        <Button leftSection={<IconPlus size={18} />} onClick={openCreate}>
          Yeni kullanıcı
        </Button>
      </Group>

      <TextInput
        placeholder="Ad, e-posta veya departman ara"
        leftSection={<IconSearch size={16} />}
        value={search}
        onChange={(event) => {
          setSearch(event.currentTarget.value)
          setPage(1)
        }}
        maw={360}
      />

      <Table.ScrollContainer minWidth={720}>
        <Table striped highlightOnHover withTableBorder>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Ad soyad</Table.Th>
              <Table.Th>Departman</Table.Th>
              <Table.Th>Rol</Table.Th>
              <Table.Th>Giriş yetkisi</Table.Th>
              <Table.Th>Durum</Table.Th>
              <Table.Th w={48} />
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {isPending ? (
              <Table.Tr>
                <Table.Td colSpan={6}>
                  <Text c="dimmed" size="sm">
                    Yükleniyor…
                  </Text>
                </Table.Td>
              </Table.Tr>
            ) : data?.items.length === 0 ? (
              <Table.Tr>
                <Table.Td colSpan={6}>
                  <Text c="dimmed" size="sm">
                    Kullanıcı bulunamadı.
                  </Text>
                </Table.Td>
              </Table.Tr>
            ) : (
              data?.items.map((user) => (
                <Table.Tr key={user.id}>
                  <Table.Td>
                    <Anchor component={Link} to={`/users/${user.id}`} size="sm" fw={500}>
                      {user.fullName}
                    </Anchor>
                    {user.email && (
                      <Text size="xs" c="dimmed">
                        {user.email}
                      </Text>
                    )}
                  </Table.Td>
                  <Table.Td>{user.department ?? '—'}</Table.Td>
                  <Table.Td>{roleLabel(user.role)}</Table.Td>
                  <Table.Td>
                    <Badge variant="light" color={user.canLogin ? 'blue' : 'gray'}>
                      {user.canLogin ? 'Var' : 'Yok'}
                    </Badge>
                  </Table.Td>
                  <Table.Td>
                    <Badge variant="dot" color={user.isActive ? 'green' : 'gray'}>
                      {user.isActive ? 'Aktif' : 'Pasif'}
                    </Badge>
                  </Table.Td>
                  <Table.Td>
                    <Menu position="bottom-end" withinPortal>
                      <Menu.Target>
                        <ActionIcon
                          variant="subtle"
                          color="gray"
                          aria-label={`${user.fullName} işlemleri`}
                        >
                          <IconDots size={18} />
                        </ActionIcon>
                      </Menu.Target>
                      <Menu.Dropdown>
                        <Menu.Item
                          leftSection={<IconPencil size={16} />}
                          onClick={() => openEdit(user)}
                        >
                          Düzenle
                        </Menu.Item>
                        {user.canLogin && (
                          <Menu.Item
                            leftSection={<IconKey size={16} />}
                            onClick={() => setResetting(user)}
                          >
                            Şifre sıfırla
                          </Menu.Item>
                        )}
                        {user.isActive && (
                          <Menu.Item
                            color="red"
                            leftSection={<IconUserOff size={16} />}
                            onClick={() => confirmDeactivate(user)}
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

      <UserFormModal opened={formOpened} onClose={form.close} user={editing} />
      <ResetPasswordModal user={resetting} onClose={() => setResetting(null)} />
    </Stack>
  )
}
