import { Paper, SimpleGrid, Stack, Text, ThemeIcon, Title, UnstyledButton } from '@mantine/core'
import {
  IconArrowBackUp,
  IconArrowsMove,
  IconClipboardCheck,
  IconHistory,
  IconLogin,
  IconPackage,
  IconUserCheck,
  IconUserShare,
} from '@tabler/icons-react'
import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { useAuth } from '@/features/auth/authContext'
import { Permissions, type Permission } from '@/features/auth/permissions'

interface Shortcut {
  to: string
  label: string
  description: string
  icon: ReactNode
  color: string
  permission?: Permission
}

// Large touch targets: the home page doubles as the phone's start screen.
const shortcuts: Shortcut[] = [
  {
    to: '/scan-move',
    label: 'Tara-taşı',
    description: 'Rafı okut, ürünleri okut, taşı',
    icon: <IconArrowsMove size={26} />,
    color: 'blue',
    permission: Permissions.CanMoveStock,
  },
  {
    to: '/movements/in',
    label: 'Stok girişi',
    description: 'Yeni gelen ürünleri rafa al',
    icon: <IconLogin size={26} />,
    color: 'green',
    permission: Permissions.CanMoveStock,
  },
  {
    to: '/assignments/new',
    label: 'Zimmet ver',
    description: 'Adeti okut, kişiyi seç',
    icon: <IconUserShare size={26} />,
    color: 'grape',
    permission: Permissions.CanMoveStock,
  },
  {
    to: '/assignments/return',
    label: 'İade al',
    description: 'Zimmetli adeti rafa geri al',
    icon: <IconArrowBackUp size={26} />,
    color: 'teal',
    permission: Permissions.CanMoveStock,
  },
  {
    to: '/my-assignments',
    label: 'Zimmetlerim',
    description: 'Üzerimdeki adetler ve teslim tarihleri',
    icon: <IconUserCheck size={26} />,
    color: 'indigo',
  },
  {
    to: '/counts',
    label: 'Sayım',
    description: 'Rafı say, farkları onayla',
    icon: <IconClipboardCheck size={26} />,
    color: 'orange',
  },
  {
    to: '/products',
    label: 'Ürünler',
    description: 'Ürünler ve stok durumu',
    icon: <IconPackage size={26} />,
    color: 'grape',
  },
  {
    to: '/movements',
    label: 'Hareketler',
    description: 'Kim, neyi, nereye taşıdı',
    icon: <IconHistory size={26} />,
    color: 'gray',
  },
]

export function HomePage() {
  const { user, hasPermission } = useAuth()
  const visible = shortcuts.filter((s) => !s.permission || hasPermission(s.permission))

  return (
    <Stack>
      <div>
        <Title order={2}>Merhaba{user ? `, ${user.fullName.split(' ')[0]}` : ''}</Title>
        <Text c="dimmed">
          Üst bardaki "Tara" ile herhangi bir etiketi ya da barkodu okutabilirsiniz.
        </Text>
      </div>
      <SimpleGrid cols={{ base: 1, xs: 2 }} maw={720}>
        {visible.map((s) => (
          <UnstyledButton key={s.to} component={Link} to={s.to}>
            <Paper withBorder p="lg" radius="md">
              <ThemeIcon size={48} radius="md" variant="light" color={s.color} mb="sm">
                {s.icon}
              </ThemeIcon>
              <Text fw={600}>{s.label}</Text>
              <Text size="sm" c="dimmed">
                {s.description}
              </Text>
            </Paper>
          </UnstyledButton>
        ))}
      </SimpleGrid>
    </Stack>
  )
}
