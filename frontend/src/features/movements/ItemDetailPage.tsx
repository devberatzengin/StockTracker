import {
  Alert,
  Anchor,
  Badge,
  Button,
  Group,
  Modal,
  Paper,
  SimpleGrid,
  Stack,
  Text,
  Textarea,
  TextInput,
  Title,
} from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import { notifications } from '@mantine/notifications'
import {
  IconArrowBackUp,
  IconCircleCheck,
  IconPencil,
  IconTool,
  IconUserShare,
} from '@tabler/icons-react'
import { useState, type ReactNode } from 'react'
import { Link, useParams } from 'react-router'
import { isApiError } from '@/api/client'
import type { ItemDetail } from '@/api/types'
import { useItemStatusChange, type StatusChangeAction } from '@/features/assignments/assignmentsApi'
import { AssignmentTable } from '@/features/assignments/AssignmentTable'
import { AuditHistory } from '@/features/audit/AuditHistory'
import { PrintLabelsButton } from '@/features/labels/PrintLabelsButton'
import { NotFoundPage } from '@/components/NotFoundPage'
import { usePermission } from '@/features/auth/authContext'
import { Permissions } from '@/features/auth/permissions'
import { parseLineErrors } from '@/features/movements/lineErrors'
import { MovementList } from '@/features/movements/MovementList'
import { useItem, useUpdateItem } from '@/features/movements/movementsApi'
import { ItemStatusBadge } from '@/features/movements/ProductStockTabs'
import { formatDateTime, formatDay } from '@/lib/dates'

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

const STATUS_CHANGES: Record<StatusChangeAction, { title: string; done: string }> = {
  'send-to-repair': { title: 'Tamire gönder', done: 'Adet tamire gönderildi.' },
  'return-from-repair': { title: 'Tamirden döndü', done: 'Adet tamirden döndü, stokta.' },
}

function ItemDetail({ item }: { item: ItemDetail }) {
  const canEdit = usePermission(Permissions.CanMoveStock)
  const canViewAudit = usePermission(Permissions.CanViewAudit)
  const [editing, edit] = useDisclosure()
  const [statusChange, setStatusChange] = useState<StatusChangeAction | null>(null)
  const [assignmentPage, setAssignmentPage] = useState(1)
  // Only the transitions the item's state allows (from the server's State pattern).
  const can = (action: ItemDetail['availableActions'][number]) =>
    canEdit && item.availableActions.includes(action)

  return (
    <Stack maw={860}>
      <div>
        <Group gap="xs">
          <Title order={2} ff="monospace">
            {item.code}
          </Title>
          <ItemStatusBadge status={item.status} />
          <PrintLabelsButton
            target={{ itemIds: [item.id] }}
            summary={item.code}
            size="compact-sm"
            ml="auto"
          />
        </Group>
        <Anchor component={Link} to={`/products/${item.productId}`}>
          {item.productName}
        </Anchor>
        <Text span c="dimmed" size="sm">
          {' '}
          · {item.productCode}
        </Text>
      </div>

      {(can('Assign') || can('Return') || can('SendToRepair') || can('ReturnFromRepair')) && (
        <Group gap="xs">
          {can('Assign') && (
            <Button
              component={Link}
              to={`/assignments/new?itemId=${item.id}`}
              leftSection={<IconUserShare size={16} />}
            >
              Zimmet ver
            </Button>
          )}
          {can('Return') && (
            <Button
              component={Link}
              to={`/assignments/return?itemId=${item.id}`}
              leftSection={<IconArrowBackUp size={16} />}
            >
              İade al
            </Button>
          )}
          {can('SendToRepair') && (
            <Button
              variant="light"
              color="orange"
              leftSection={<IconTool size={16} />}
              onClick={() => setStatusChange('send-to-repair')}
            >
              Tamire gönder
            </Button>
          )}
          {can('ReturnFromRepair') && (
            <Button
              variant="light"
              color="green"
              leftSection={<IconCircleCheck size={16} />}
              onClick={() => setStatusChange('return-from-repair')}
            >
              Tamirden döndü
            </Button>
          )}
        </Group>
      )}

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
          {item.openAssignment && (
            <Field label="Zimmetli">
              <Group gap={6}>
                <span>{item.openAssignment.assigneeName}</span>
                {item.openAssignment.isOverdue && (
                  <Badge size="sm" color="red" variant="light">
                    Gecikmiş
                  </Badge>
                )}
              </Group>
              <Text size="xs" c="dimmed">
                {formatDateTime(item.openAssignment.assignedAt)}
                {item.openAssignment.dueDate
                  ? ` · teslim ${formatDay(item.openAssignment.dueDate)}`
                  : ''}
              </Text>
            </Field>
          )}
        </SimpleGrid>
      </Paper>

      {item.productIsAssignable && canEdit && (
        <div>
          <Title order={4} mb="sm">
            Zimmet geçmişi
          </Title>
          <AssignmentTable
            filters={{ itemId: item.id, sort: '-assignedAt' }}
            page={assignmentPage}
            onPageChange={setAssignmentPage}
            hide="item"
          />
        </div>
      )}

      <div>
        <Title order={4} mb="sm">
          Geçmiş
        </Title>
        <MovementList filters={{ itemId: item.id }} variant="timeline" />
      </div>

      {canViewAudit && (
        <div>
          <Title order={4} mb="sm">
            Değişiklik geçmişi
          </Title>
          <AuditHistory filters={{ entityType: 'Item', entityId: item.id }} />
        </div>
      )}

      <Modal opened={editing} onClose={edit.close} title="Seri numarasını düzenle">
        <SerialForm item={item} onClose={edit.close} />
      </Modal>

      <Modal
        opened={statusChange !== null}
        onClose={() => setStatusChange(null)}
        title={statusChange ? STATUS_CHANGES[statusChange].title : undefined}
      >
        {statusChange && (
          <StatusChangeForm
            item={item}
            action={statusChange}
            onClose={() => setStatusChange(null)}
          />
        )}
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

function StatusChangeForm({
  item,
  action,
  onClose,
}: {
  item: ItemDetail
  action: StatusChangeAction
  onClose: () => void
}) {
  const change = useItemStatusChange(item.id)
  const [note, setNote] = useState('')
  const [error, setError] = useState<string | null>(null)

  const save = async () => {
    setError(null)
    try {
      await change.mutateAsync({ action, note: note.trim() })
      notifications.show({ color: 'green', message: STATUS_CHANGES[action].done })
      onClose()
    } catch (e) {
      const parsed = parseLineErrors(e)
      setError(parsed.fields.note ?? parsed.general[0])
    }
  }

  return (
    <Stack>
      {error && <Alert color="red">{error}</Alert>}
      <Textarea
        label="Not"
        description={
          action === 'send-to-repair'
            ? 'Arızayı ya da tamire gönderme sebebini yazın.'
            : 'Yapılan tamiri kısaca yazın.'
        }
        required
        value={note}
        onChange={(e) => setNote(e.currentTarget.value)}
        maxLength={500}
        autosize
        minRows={2}
        data-autofocus
      />
      <Group justify="flex-end">
        <Button variant="default" onClick={onClose}>
          Vazgeç
        </Button>
        <Button
          onClick={() => void save()}
          loading={change.isPending}
          disabled={note.trim() === ''}
        >
          {STATUS_CHANGES[action].title}
        </Button>
      </Group>
    </Stack>
  )
}
