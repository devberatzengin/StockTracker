import {
  Alert,
  Anchor,
  Button,
  Group,
  Modal,
  Pagination,
  SegmentedControl,
  Stack,
  Switch,
  Table,
  Text,
  Textarea,
  Title,
} from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import { IconClipboardCheck } from '@tabler/icons-react'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import type { CountStatus } from '@/api/types'
import { usePermission } from '@/features/auth/authContext'
import { Permissions } from '@/features/auth/permissions'
import { CountStatusBadge } from '@/features/counts/CountStatusBadge'
import { useCounts, useStartCount } from '@/features/counts/countsApi'
import { COUNT_STATUS_LABELS } from '@/features/counts/labels'
import { parseLineErrors } from '@/features/movements/lineErrors'
import { LocationPicker } from '@/features/movements/LocationPicker'
import { formatDateTime } from '@/lib/dates'

type StatusFilter = CountStatus | 'all'

const STATUS_OPTIONS: { value: StatusFilter; label: string }[] = [
  { value: 'all', label: 'Tümü' },
  ...(Object.keys(COUNT_STATUS_LABELS) as CountStatus[]).map((value) => ({
    value,
    label: COUNT_STATUS_LABELS[value],
  })),
]

export function CountsPage() {
  const canStart = usePermission(Permissions.CanApproveCount)
  const [status, setStatus] = useState<StatusFilter>('all')
  const [page, setPage] = useState(1)
  const [starting, start] = useDisclosure()
  const { data, isPending } = useCounts({ page, status: status === 'all' ? null : status })
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>Sayımlar</Title>
        {canStart && (
          <Button leftSection={<IconClipboardCheck size={16} />} onClick={start.open}>
            Sayım başlat
          </Button>
        )}
      </Group>

      <SegmentedControl
        value={status}
        onChange={(value) => {
          setStatus(value as StatusFilter)
          setPage(1)
        }}
        data={STATUS_OPTIONS}
        style={{ alignSelf: 'flex-start' }}
      />

      <Table.ScrollContainer minWidth={720}>
        <Table striped verticalSpacing="xs">
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Lokasyon</Table.Th>
              <Table.Th>Durum</Table.Th>
              <Table.Th>İlerleme</Table.Th>
              <Table.Th>Başlatan</Table.Th>
              <Table.Th>Başlangıç</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {isPending ? (
              <Table.Tr>
                <Table.Td colSpan={5}>
                  <Text c="dimmed">Yükleniyor…</Text>
                </Table.Td>
              </Table.Tr>
            ) : !data || data.items.length === 0 ? (
              <Table.Tr>
                <Table.Td colSpan={5}>
                  <Text c="dimmed">Sayım yok.</Text>
                </Table.Td>
              </Table.Tr>
            ) : (
              data.items.map((c) => (
                <Table.Tr key={c.id}>
                  <Table.Td>
                    <Anchor component={Link} to={`/counts/${c.id}`} size="sm" fw={500}>
                      {c.locationPath}
                    </Anchor>
                    {c.includeChildren && (
                      <Text size="xs" c="dimmed">
                        alt lokasyonlar dahil
                      </Text>
                    )}
                  </Table.Td>
                  <Table.Td>
                    <CountStatusBadge status={c.status} />
                  </Table.Td>
                  <Table.Td>
                    <Text size="sm">
                      {c.countedExpectedLines} / {c.expectedLines}
                    </Text>
                    {c.unexpectedLines > 0 && (
                      <Text size="xs" c="orange">
                        {c.unexpectedLines} beklenmeyen
                      </Text>
                    )}
                  </Table.Td>
                  <Table.Td>{c.startedByName}</Table.Td>
                  <Table.Td>{formatDateTime(c.startedAt)}</Table.Td>
                </Table.Tr>
              ))
            )}
          </Table.Tbody>
        </Table>
      </Table.ScrollContainer>
      {totalPages > 1 && (
        <Pagination value={page} onChange={setPage} total={totalPages} size="sm" />
      )}

      <Modal opened={starting} onClose={start.close} title="Sayım başlat">
        <StartCountForm onClose={start.close} />
      </Modal>
    </Stack>
  )
}

function StartCountForm({ onClose }: { onClose: () => void }) {
  const navigate = useNavigate()
  const startCount = useStartCount()
  const [locationId, setLocationId] = useState<string | null>(null)
  const [includeChildren, setIncludeChildren] = useState(false)
  const [note, setNote] = useState('')
  const [error, setError] = useState<string | null>(null)

  const submit = async () => {
    setError(null)
    try {
      const { id } = await startCount.mutateAsync({
        locationId: locationId!,
        includeChildren,
        note: note.trim() || null,
      })
      onClose()
      navigate(`/counts/${id}`)
    } catch (e) {
      const parsed = parseLineErrors(e)
      setError(parsed.fields.locationId ?? parsed.general[0])
    }
  }

  return (
    <Stack>
      {error && <Alert color="red">{error}</Alert>}
      <LocationPicker
        label="Sayılacak lokasyon"
        value={locationId}
        onChange={setLocationId}
        required
      />
      <Switch
        label="Alt lokasyonlar dahil"
        checked={includeChildren}
        onChange={(e) => setIncludeChildren(e.currentTarget.checked)}
      />
      <Textarea
        label="Not (opsiyonel)"
        value={note}
        onChange={(e) => setNote(e.currentTarget.value)}
        maxLength={500}
        autosize
      />
      <Text size="xs" c="dimmed">
        Başlatıldığı andaki stok beklenen olarak kaydedilir. Sayım sırasında hareketler engellenmez.
      </Text>
      <Group justify="flex-end">
        <Button variant="default" onClick={onClose}>
          Vazgeç
        </Button>
        <Button onClick={() => void submit()} disabled={!locationId} loading={startCount.isPending}>
          Başlat
        </Button>
      </Group>
    </Stack>
  )
}
