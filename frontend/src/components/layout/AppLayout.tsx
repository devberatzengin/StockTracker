import {
  AppShell,
  Avatar,
  Burger,
  Group,
  Menu,
  NavLink,
  Text,
  Title,
  UnstyledButton,
} from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import {
  IconArrowsMove,
  IconCategory,
  IconChevronDown,
  IconHistory,
  IconHome,
  IconKey,
  IconLogout,
  IconMapPin,
  IconPackage,
  IconUserCheck,
  IconUsers,
  IconUserShare,
} from '@tabler/icons-react'
import type { ReactNode } from 'react'
import { NavLink as RouterNavLink, Outlet, useLocation } from 'react-router'
import { ChangePasswordModal } from '@/features/auth/ChangePasswordModal'
import { useAuth } from '@/features/auth/authContext'
import { Permissions, type Permission } from '@/features/auth/permissions'
import { ScanButton } from '@/features/scan/ScanButton'
import { roleLabel } from '@/features/users/roles'

interface NavItem {
  label: string
  to: string
  icon: ReactNode
  permission?: Permission
}

const mainItems: NavItem[] = [
  { label: 'Ana sayfa', to: '/', icon: <IconHome size={18} /> },
  { label: 'Ürünler', to: '/products', icon: <IconPackage size={18} /> },
  { label: 'Lokasyonlar', to: '/locations', icon: <IconMapPin size={18} /> },
  { label: 'Hareketler', to: '/movements', icon: <IconHistory size={18} /> },
  {
    label: 'Tara-taşı',
    to: '/scan-move',
    icon: <IconArrowsMove size={18} />,
    permission: Permissions.CanMoveStock,
  },
  {
    label: 'Zimmetler',
    to: '/assignments',
    icon: <IconUserShare size={18} />,
    permission: Permissions.CanMoveStock,
  },
  { label: 'Zimmetlerim', to: '/my-assignments', icon: <IconUserCheck size={18} /> },
  {
    label: 'Kategoriler',
    to: '/categories',
    icon: <IconCategory size={18} />,
    permission: Permissions.CanManageCatalog,
  },
  {
    label: 'Kullanıcılar',
    to: '/users',
    icon: <IconUsers size={18} />,
    permission: Permissions.CanManageUsers,
  },
]

function initials(fullName: string) {
  return fullName
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]!.toLocaleUpperCase('tr-TR'))
    .join('')
}

export function AppLayout() {
  const [opened, { toggle, close }] = useDisclosure()
  const [passwordOpened, passwordModal] = useDisclosure()
  const { pathname } = useLocation()
  const { user, logout, hasPermission } = useAuth()

  const visible = (items: NavItem[]) =>
    items.filter((item) => !item.permission || hasPermission(item.permission))

  const renderItem = (item: NavItem) => (
    <NavLink
      key={item.to}
      component={RouterNavLink}
      to={item.to}
      label={item.label}
      leftSection={item.icon}
      active={item.to === '/' ? pathname === '/' : pathname.startsWith(item.to)}
      onClick={close}
    />
  )

  return (
    <AppShell
      header={{ height: 56 }}
      navbar={{ width: 240, breakpoint: 'sm', collapsed: { mobile: !opened } }}
      padding="md"
    >
      <AppShell.Header>
        <Group h="100%" px="md" justify="space-between" wrap="nowrap">
          <Group wrap="nowrap">
            <Burger opened={opened} onClick={toggle} hiddenFrom="sm" size="sm" aria-label="Menü" />
            <Title order={4}>StockTracker</Title>
          </Group>

          {user && (
            <Group gap="sm" wrap="nowrap">
              <ScanButton />
              <Menu position="bottom-end" width={220}>
                <Menu.Target>
                  <UnstyledButton aria-label="Profil menüsü">
                    <Group gap="xs" wrap="nowrap">
                      <Avatar radius="xl" size="sm" color="blue">
                        {initials(user.fullName)}
                      </Avatar>
                      <div style={{ lineHeight: 1.1 }}>
                        <Text size="sm" fw={500} visibleFrom="xs">
                          {user.fullName}
                        </Text>
                        <Text size="xs" c="dimmed" visibleFrom="xs">
                          {roleLabel(user.role)}
                        </Text>
                      </div>
                      <IconChevronDown size={14} />
                    </Group>
                  </UnstyledButton>
                </Menu.Target>
                <Menu.Dropdown>
                  <Menu.Label>{user.email}</Menu.Label>
                  <Menu.Item leftSection={<IconKey size={16} />} onClick={passwordModal.open}>
                    Şifre değiştir
                  </Menu.Item>
                  <Menu.Item
                    color="red"
                    leftSection={<IconLogout size={16} />}
                    onClick={() => void logout()}
                  >
                    Çıkış
                  </Menu.Item>
                </Menu.Dropdown>
              </Menu>
            </Group>
          )}
        </Group>
      </AppShell.Header>

      <AppShell.Navbar p="xs">{visible(mainItems).map(renderItem)}</AppShell.Navbar>

      <AppShell.Main>
        <Outlet />
      </AppShell.Main>

      <ChangePasswordModal opened={passwordOpened} onClose={passwordModal.close} />
    </AppShell>
  )
}
