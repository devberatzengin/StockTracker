import { AppShell, Burger, Group, NavLink, Text, Title } from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import { IconHome, IconScan } from '@tabler/icons-react'
import type { ReactNode } from 'react'
import { NavLink as RouterNavLink, Outlet, useLocation } from 'react-router'

interface NavItem {
  label: string
  to: string
  icon: ReactNode
}

const mainItems: NavItem[] = [{ label: 'Ana sayfa', to: '/', icon: <IconHome size={18} /> }]

// Temporary developer tools from Phase 0.
const devItems: NavItem[] = [
  { label: 'Tarama denemesi', to: '/dev/scan', icon: <IconScan size={18} /> },
]

export function AppLayout() {
  const [opened, { toggle, close }] = useDisclosure()
  const { pathname } = useLocation()

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
        <Group h="100%" px="md">
          <Burger opened={opened} onClick={toggle} hiddenFrom="sm" size="sm" aria-label="Menü" />
          <Title order={4}>StockTracker</Title>
        </Group>
      </AppShell.Header>

      <AppShell.Navbar p="xs">
        {mainItems.map(renderItem)}
        <Text size="xs" c="dimmed" fw={600} tt="uppercase" mt="md" mb={4} px="sm">
          Geliştirici
        </Text>
        {devItems.map(renderItem)}
      </AppShell.Navbar>

      <AppShell.Main>
        <Outlet />
      </AppShell.Main>
    </AppShell>
  )
}
