import {
  Alert,
  Anchor,
  Button,
  Group,
  Modal,
  Paper,
  SimpleGrid,
  Stack,
  Text,
  TextInput,
  Title,
} from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import { notifications } from '@mantine/notifications'
import { IconPencil } from '@tabler/icons-react'
import { useState, type ReactNode } from 'react'
import { Link, useParams } from 'react-router'
import { isApiError } from '@/api/client'
import type { ItemDetail } from '@/api/types'
import { NotFoundPage } from '@/components/NotFoundPage'
import { usePermission } from '@/features/auth/authContext'
import { Permissions } from '@/features/auth/permissions'
import { parseLineErrors } from '@/features/movements/lineErrors'
import { MovementList } from '@/features/movements/MovementList'
import { useItem, useUpdateItem } from '@/features/movements/movementsApi'
import { ItemStatusBadge } from '@/features/movements/ProductStockTabs'
import { formatDateTime } from '@/lib/dates'

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <Text size="xs" c="dimmed" fw={500}>
        {label}
      </Text>
      <Text size="sm" component="div">
        {children}
      </Text>
    </div>
  )
}

export function ItemDetailPage() {
  const { id = '' } = useParams()
  const { data: item, isPending, error } = useItem(id)

  if (isApiError(error, 404) || isApiError(error, 400)) return <NotFoundPage />
  if (isPending || !item) return <Text c="dimmed">Yükleniyor…</Text>

  return <ItemDetail item={item} />
}

function ItemDetail({ item }: { item: ItemDetail }) {
  const canEdit = usePermission(Permissions.CanMoveStock)
  const [editing, edit] = useDisclosure()

  return (
    <Stack maw={860}>
      <div>
        <Group gap="xs">
          <Title order={2} ff="monospace">
            {item.code}
          </Title>
          <ItemStatusBadge status={item.status} />
        </Group>
        <Anchor component={Link} to={`/products/${item.productId}`}>
          {item.productName}
        </Anchor>
        <Text span c="dimmed" size="sm">
          {' '}
          · {item.productCode}
        </Text>
      </div>

      <Paper withBorder p="md">
        <SimpleGrid cols={{ base: 1, xs: 2, md: 3 }}>
          <Field label="Seri no">
            <Group gap={4} wrap="nowrap">
              <span>{item.serialNumber ?? '—'}</span>
              {canEdit && (
                <Button
                  size="compact-xs"
                  variant="subtle"
                  leftSection={<IconPencil size={12} />}
                  onClick={edit.open}
                >
                  Düzenle
                </Button>
              )}
            </Group>
          </Field>
          <Field label="Konum">
            {item.currentLocationId ? (
              <Anchor component={Link} to={`/locations/${item.currentLocationId}`} size="sm">
                {item.locationPath}
              </Anchor>
            ) : (
              '—'
            )}
          </Field>
          <Field label="Oluşturulma">{formatDateTime(item.createdAt)}</Field>
        </SimpleGrid>
      </Paper>

      <div>
        <Title order={4} mb="sm">
          Geçmiş
        </Title>
        <MovementList filters={{ itemId: item.id }} variant="timeline" />
      </div>

      <Modal opened={editing} onClose={edit.close} title="Seri numarasını düzenle">
        <SerialForm item={item} onClose={edit.close} />
      </Modal>
    </Stack>
  )
}

function SerialForm({ item, onClose }: { item: ItemDetail; onClose: () => void }) {
  const update = useUpdateItem(item.id)
  const [value, setValue] = useState(item.serialNumber ?? '')
  const [error, setError] = useState<string | null>(null)

  const save = async () => {
    setError(null)
    try {
      await update.mutateAsync(value.trim() || null)
      notifications.show({ color: 'green', message: 'Seri numarası güncellendi.' })
      onClose()
    } catch (e) {
      const parsed = parseLineErrors(e)
      setError(parsed.fields.serialNumber ?? parsed.general[0])
    }
  }

  return (
    <Stack>
      {error && <Alert color="red">{error}</Alert>}
      <TextInput
        label="Seri no"
        description="Boş bırakılırsa seri numarası kaldırılır."
        value={value}
        onChange={(e) => setValue(e.currentTarget.value)}
        maxLength={100}
        data-autofocus
      />
      <Group justify="flex-end">
        <Button variant="default" onClick={onClose}>
          Vazgeç
        </Button>
        <Button onClick={() => void save()} loading={update.isPending}>
          Kaydet
        </Button>
      </Group>
    </Stack>
  )
}
