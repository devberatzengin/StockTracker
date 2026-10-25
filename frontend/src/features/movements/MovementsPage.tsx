import {
  Button,
  Group,
  Paper,
  Select,
  SimpleGrid,
  Stack,
  Switch,
  TextInput,
  Title,
} from '@mantine/core'
import { IconArrowsMove, IconLogin, IconLogout, IconScan } from '@tabler/icons-react'
import { useState } from 'react'
import { Link } from 'react-router'
import type { MovementType } from '@/api/types'
import { useAuth } from '@/features/auth/authContext'
import { Permissions } from '@/features/auth/permissions'
import { MOVEMENT_TYPE_OPTIONS } from '@/features/movements/labels'
import { LocationPicker } from '@/features/movements/LocationPicker'
import { MovementList } from '@/features/movements/MovementList'
import type { PickedProduct } from '@/features/movements/pickedProduct'
import { ProductPicker } from '@/features/movements/ProductPicker'
import { useUsers } from '@/features/users/usersApi'
import { endOfDayIstanbul, startOfDayIstanbul } from '@/lib/dates'

function UserFilter({
  value,
  onChange,
}: {
  value: string | null
  onChange: (id: string | null) => void
}) {
  const { data } = useUsers({ page: 1, pageSize: 100 })
  return (
    <Select
      label="Kullanıcı"
      placeholder="Tümü"
      data={(data?.items ?? []).map((u) => ({ value: u.id, label: u.fullName }))}
      value={value}
      onChange={onChange}
      searchable
      clearable
    />
  )
}

/** All movements with filters, grouped by operation (ReferenceId), newest first. */
export function MovementsPage() {
  const { user, hasPermission } = useAuth()
  const canMove = hasPermission(Permissions.CanMoveStock)
  const canListUsers = hasPermission(Permissions.CanManageUsers)
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [type, setType] = useState<MovementType | null>(null)
  const [product, setProduct] = useState<PickedProduct | null>(null)
  const [locationId, setLocationId] = useState<string | null>(null)
  const [userId, setUserId] = useState<string | null>(null)
  const [onlyMine, setOnlyMine] = useState(false)

  const filters = {
    from: from ? startOfDayIstanbul(from) : undefined,
    to: to ? endOfDayIstanbul(to) : undefined,
    type,
    productId: product?.id ?? null,
    locationId,
    userId: canListUsers ? userId : onlyMine ? (user?.id ?? null) : null,
  }

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>Hareketler</Title>
        {canMove && (
          <Group gap="xs">
            <Button
              component={Link}
              to="/movements/in"
              variant="light"
              color="green"
              leftSection={<IconLogin size={16} />}
            >
              Giriş
            </Button>
            <Button
              component={Link}
              to="/movements/out"
              variant="light"
              color="red"
              leftSection={<IconLogout size={16} />}
            >
              Çıkış
            </Button>
            <Button
              component={Link}
              to="/movements/transfer"
              variant="light"
              leftSection={<IconArrowsMove size={16} />}
            >
              Transfer
            </Button>
            <Button
              component={Link}
              to="/scan-move"
              variant="light"
              leftSection={<IconScan size={16} />}
            >
              Tara-taşı
            </Button>
          </Group>
        )}
      </Group>

      <Paper withBorder p="md">
        <SimpleGrid cols={{ base: 1, sm: 2, lg: 3 }}>
          <TextInput
            type="date"
            label="Başlangıç"
            value={from}
            onChange={(e) => setFrom(e.currentTarget.value)}
          />
          <TextInput
            type="date"
            label="Bitiş"
            value={to}
            onChange={(e) => setTo(e.currentTarget.value)}
          />
          <Select
            label="Tip"
            placeholder="Tümü"
            data={MOVEMENT_TYPE_OPTIONS}
            value={type}
            onChange={(v) => setType(v as MovementType | null)}
            clearable
          />
          <ProductPicker value={product} onChange={setProduct} />
          <LocationPicker label="Lokasyon" value={locationId} onChange={setLocationId} />
          {canListUsers ? (
            <UserFilter value={userId} onChange={setUserId} />
          ) : (
            <Switch
              label="Sadece benim hareketlerim"
              checked={onlyMine}
              onChange={(e) => setOnlyMine(e.currentTarget.checked)}
              mt="lg"
            />
          )}
        </SimpleGrid>
      </Paper>

      <MovementList
        key={JSON.stringify(filters)}
        filters={filters}
        variant="grouped"
        emptyText="Filtrelere uyan hareket yok."
      />
    </Stack>
  )
}
