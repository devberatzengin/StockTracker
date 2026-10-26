import {
  Alert,
  Badge,
  Button,
  Group,
  SegmentedControl,
  Stack,
  Switch,
  Table,
  Text,
  Title,
} from '@mantine/core'
import { modals } from '@mantine/modals'
import { notifications } from '@mantine/notifications'
import { IconCheck } from '@tabler/icons-react'
import { useMemo, useState } from 'react'
import type { CountDetail, CountLineState, CountResolution, CountReviewLine } from '@/api/types'
import { differenceText, lineTitle, quantityText } from '@/features/counts/countFormat'
import { useApproveCount, useCountAction, useCountReview } from '@/features/counts/countsApi'
import { COUNT_LINE_STATE_LABELS, COUNT_RESOLUTION_LABELS } from '@/features/counts/labels'
import { parseLineErrors } from '@/features/movements/lineErrors'
import { MovementList } from '@/features/movements/MovementList'
import { ITEM_STATUS_LABELS } from '@/features/movements/labels'

const needsDecision = (l: CountReviewLine) => l.options.length > 0

/**
 * Desktop approval: each difference gets a decision; "apply to all" per difference type.
 * Differences are computed against the stock now, so movements since the start are listed.
 */
export function ReviewScreen({ count }: { count: CountDetail }) {
  const { data: review, isPending } = useCountReview(count.id, true)
  const approve = useApproveCount(count.id)
  const action = useCountAction(count.id)
  const [decisions, setDecisions] = useState<Record<string, CountResolution>>({})
  const [showAll, setShowAll] = useState(false)
  const [errors, setErrors] = useState<string[]>([])

  const decide = (lineId: string, resolution: CountResolution) =>
    setDecisions((current) => ({ ...current, [lineId]: resolution }))

  const lines = useMemo(() => review?.lines ?? [], [review])
  const toDecide = lines.filter(needsDecision)
  const info = lines.filter(
    (l) => !needsDecision(l) && l.state !== 'Matched' && l.state !== 'NotCountedEmpty',
  )
  const matched = lines.filter((l) => l.state === 'Matched' || l.state === 'NotCountedEmpty')
  const undecided = toDecide.filter((l) => !decisions[l.line.id]).length

  // "Tümüne uygula": one row per difference type that needs a decision.
  const groups = useMemo(() => {
    const byState = new Map<CountLineState, CountReviewLine[]>()
    for (const l of lines.filter(needsDecision)) {
      byState.set(l.state, [...(byState.get(l.state) ?? []), l])
    }
    return [...byState.entries()]
  }, [lines])

  const submit = async () => {
    setErrors([])
    try {
      const result = await approve.mutateAsync(
        toDecide.map((l) => ({ lineId: l.line.id, resolution: decisions[l.line.id]! })),
      )
      notifications.show({
        color: 'green',
        message: `Sayım onaylandı; ${result.movementCount} hareket oluşturuldu.`,
      })
    } catch (e) {
      const parsed = parseLineErrors(e)
      setErrors([...parsed.general, ...Object.values(parsed.fields)])
    }
  }

  const cancel = () =>
    modals.openConfirmModal({
      title: 'Sayımı iptal et',
      children: <Text size="sm">Sayım iptal edilir; hiçbir hareket oluşturulmaz.</Text>,
      labels: { confirm: 'İptal et', cancel: 'Vazgeç' },
      confirmProps: { color: 'red' },
      onConfirm: () => void action.mutateAsync('cancel'),
    })

  if (isPending || !review) return <Text c="dimmed">Fark tablosu hazırlanıyor…</Text>

  return (
    <Stack>
      {review.movementsSinceStart > 0 && (
        <Alert color="orange" title="Sayım başladıktan sonra kapsamda hareket oldu">
          <Text size="sm" mb="xs">
            Farklar onay anındaki güncel stoğa göre hesaplandı. {review.movementsSinceStart}{' '}
            hareketi kontrol edin:
          </Text>
          <MovementList filters={{ countId: count.id, pageSize: 10 }} variant="grouped" />
        </Alert>
      )}

      {errors.map((text) => (
        <Alert key={text} color="red">
          {text}
        </Alert>
      ))}

      <Group justify="space-between">
        <Title order={4}>Fark satırları ({toDecide.length})</Title>
        {undecided > 0 && <Badge color="orange">{undecided} satır kararsız</Badge>}
      </Group>

      {groups.length > 0 && (
        <Stack gap={6}>
          {groups.map(([state, group]) => (
            <Group key={state} gap="xs">
              <Text size="sm" w={200}>
                Tümüne uygula · {COUNT_LINE_STATE_LABELS[state]} ({group.length}):
              </Text>
              {group[0]!.options.map((option) => (
                <Button
                  key={option}
                  size="compact-sm"
                  variant="light"
                  onClick={() =>
                    setDecisions((current) => ({
                      ...current,
                      ...Object.fromEntries(group.map((l) => [l.line.id, option])),
                    }))
                  }
                >
                  {COUNT_RESOLUTION_LABELS[option]}
                </Button>
              ))}
            </Group>
          ))}
        </Stack>
      )}

      {toDecide.length === 0 ? (
        <Text c="dimmed">Karar gerektiren fark yok.</Text>
      ) : (
        <ReviewTable lines={toDecide} decisions={decisions} onDecide={decide} />
      )}

      {info.length > 0 && (
        <>
          <Title order={5}>Bilgi</Title>
          <ReviewTable lines={info} />
        </>
      )}

      <Switch
        label={`Eşleşen satırları göster (${matched.length})`}
        checked={showAll}
        onChange={(e) => setShowAll(e.currentTarget.checked)}
      />
      {showAll && matched.length > 0 && <ReviewTable lines={matched} />}

      <Group justify="flex-end">
        <Button variant="default" color="red" onClick={cancel} loading={action.isPending}>
          Sayımı iptal et
        </Button>
        <Button
          leftSection={<IconCheck size={16} />}
          onClick={() => void submit()}
          disabled={undecided > 0}
          loading={approve.isPending}
        >
          Onayla
        </Button>
      </Group>
    </Stack>
  )
}

function ReviewTable({
  lines,
  decisions,
  onDecide,
}: {
  lines: CountReviewLine[]
  decisions?: Record<string, CountResolution>
  onDecide?: (lineId: string, resolution: CountResolution) => void
}) {
  return (
    <Table.ScrollContainer minWidth={900}>
      <Table striped verticalSpacing="xs">
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Lokasyon</Table.Th>
            <Table.Th>Ürün / adet</Table.Th>
            <Table.Th>Başlangıç</Table.Th>
            <Table.Th>Güncel</Table.Th>
            <Table.Th>Sayılan</Table.Th>
            <Table.Th>Fark</Table.Th>
            <Table.Th>Durum</Table.Th>
            {onDecide && <Table.Th>Karar</Table.Th>}
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {lines.map((l) => (
            <Table.Tr key={l.line.id} data-testid={`review-line-${l.line.id}`}>
              <Table.Td>
                <Text size="sm">{l.line.locationName}</Text>
              </Table.Td>
              <Table.Td>
                <Text size="sm" fw={500}>
                  {lineTitle(l.line)}
                </Text>
                {l.line.lineType === 'Unexpected' && (
                  <Badge size="xs" color="orange" variant="light">
                    Beklenmeyen
                  </Badge>
                )}
                {l.itemStatus && l.state !== 'Matched' && (
                  <Text size="xs" c="dimmed">
                    {ITEM_STATUS_LABELS[l.itemStatus]}
                    {l.itemLocationPath ? ` · kayıtlı yeri: ${l.itemLocationPath}` : ''}
                  </Text>
                )}
              </Table.Td>
              <Table.Td>{quantityText(l.line.expectedQuantity, l.line.unit)}</Table.Td>
              <Table.Td>{quantityText(l.currentQuantity, l.line.unit)}</Table.Td>
              <Table.Td>{quantityText(l.line.countedQuantity, l.line.unit)}</Table.Td>
              <Table.Td>
                <Text
                  size="sm"
                  c={l.difference < 0 ? 'red' : l.difference > 0 ? 'green' : undefined}
                >
                  {differenceText(l.difference)}
                </Text>
              </Table.Td>
              <Table.Td>
                <Text size="sm">{COUNT_LINE_STATE_LABELS[l.state]}</Text>
              </Table.Td>
              {onDecide && (
                <Table.Td>
                  <SegmentedControl
                    size="xs"
                    value={decisions?.[l.line.id] ?? ''}
                    onChange={(value) => onDecide(l.line.id, value as CountResolution)}
                    data={l.options.map((o) => ({ value: o, label: COUNT_RESOLUTION_LABELS[o] }))}
                    aria-label={`${lineTitle(l.line)} kararı`}
                  />
                </Table.Td>
              )}
            </Table.Tr>
          ))}
        </Table.Tbody>
      </Table>
    </Table.ScrollContainer>
  )
}
