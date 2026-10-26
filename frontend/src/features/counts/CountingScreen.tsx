import {
  Alert,
  Button,
  Group,
  Modal,
  NumberInput,
  Paper,
  Progress,
  Stack,
  Text,
} from '@mantine/core'
import { modals } from '@mantine/modals'
import { IconArrowBackUp, IconMapPin, IconSend } from '@tabler/icons-react'
import { useState } from 'react'
import type { CountDetail, CountScanResult, Unit } from '@/api/types'
import { quantityText } from '@/features/counts/countFormat'
import { useCountAction, useScanCount, useUndoCountScan } from '@/features/counts/countsApi'
import { parseLineErrors } from '@/features/movements/lineErrors'
import { ITEM_STATUS_LABELS } from '@/features/movements/labels'
import { ScanPanel } from '@/features/scan/ScanDialog'
import { formatTime } from '@/lib/dates'

interface Context {
  id: string
  name: string
}

interface QuantityPrompt {
  code: string
  title: string
  unit: Unit
}

type Message = { color: string; text: string; detail?: string }

function describe(result: CountScanResult): Message {
  const total =
    result.countedQuantity !== null && result.countedQuantity !== undefined && result.unit
      ? ` (toplam ${quantityText(result.countedQuantity, result.unit)})`
      : ''
  switch (result.kind) {
    case 'Counted':
      return { color: 'green', text: `Sayıldı: ${result.title}${total}` }
    case 'AlreadyCounted':
      return { color: 'yellow', text: `Zaten sayıldı: ${result.title}` }
    case 'Unexpected': {
      const notInStock = result.itemStatus && result.itemStatus !== 'InStock'
      return {
        color: notInStock ? 'red' : 'orange',
        text: `Beklenmeyen: ${result.title}${total}`,
        detail: [
          notInStock
            ? `Bu adet ${ITEM_STATUS_LABELS[result.itemStatus!].toLocaleLowerCase('tr-TR')}.`
            : null,
          result.itemLocationPath ? `Kayıtlı yeri: ${result.itemLocationPath}` : null,
        ]
          .filter(Boolean)
          .join(' '),
      }
    }
    default:
      return { color: 'blue', text: result.title }
  }
}

/**
 * Mobile counting: the shelf label sets the context location, then items and products are scanned
 * continuously. Quantity products ask for the counted quantity; repeated scans add up.
 */
export function CountingScreen({ count }: { count: CountDetail }) {
  const scan = useScanCount(count.id)
  const undo = useUndoCountScan(count.id)
  const action = useCountAction(count.id)
  // A count without sub-locations has only one place to be.
  const [context, setContext] = useState<Context | null>(
    count.includeChildren ? null : { id: count.locationId, name: count.locationName },
  )
  const [message, setMessage] = useState<Message | null>(null)
  const [prompt, setPrompt] = useState<QuantityPrompt | null>(null)

  const send = async (code: string, quantity?: number) => {
    setMessage(null)
    try {
      const result = await scan.mutateAsync({ locationId: context?.id ?? null, code, quantity })
      if (result.kind === 'Location') {
        setContext({ id: result.locationId!, name: result.title })
        setMessage({
          color: 'blue',
          text: `Bulunduğunuz yer: ${result.locationPath ?? result.title}`,
        })
      } else if (result.kind === 'QuantityRequired') {
        setPrompt({ code, title: result.title, unit: result.unit! })
      } else {
        setMessage(describe(result))
      }
    } catch (e) {
      const parsed = parseLineErrors(e)
      setMessage({
        color: 'red',
        text: Object.values(parsed.fields)[0] ?? parsed.general[0] ?? 'Okutulamadı.',
      })
    }
  }

  const { expectedLines, countedExpectedLines, unexpectedLines } = count.progress
  const percent =
    expectedLines === 0 ? 100 : Math.round((countedExpectedLines / expectedLines) * 100)

  const submit = () =>
    modals.openConfirmModal({
      title: 'İncelemeye gönder',
      children: (
        <Text size="sm">
          Sayım kilitlenir, okutma yapılamaz. {expectedLines - countedExpectedLines} beklenen satır
          sayılmadı.
        </Text>
      ),
      labels: { confirm: 'Gönder', cancel: 'Vazgeç' },
      onConfirm: () => void action.mutateAsync('submit'),
    })

  return (
    <Stack maw={560} mx="auto" w="100%">
      <Paper withBorder p="sm" bg={context ? 'blue.0' : 'yellow.0'}>
        <Group gap="xs" wrap="nowrap">
          <IconMapPin size={18} />
          <div>
            <Text size="xs" c="dimmed">
              Bulunduğunuz yer
            </Text>
            <Text fw={600}>{context?.name ?? 'Rafın etiketini okutun'}</Text>
          </div>
        </Group>
      </Paper>

      <div>
        <Group justify="space-between" mb={4}>
          <Text size="sm">
            {countedExpectedLines} / {expectedLines} beklenen satır sayıldı
          </Text>
          {unexpectedLines > 0 && (
            <Text size="sm" c="orange">
              {unexpectedLines} beklenmeyen
            </Text>
          )}
        </Group>
        <Progress value={percent} size="lg" aria-label="Sayım ilerlemesi" />
      </div>

      {message && (
        <Alert color={message.color}>
          {message.text}
          {message.detail && (
            <Text size="sm" mt={4}>
              {message.detail}
            </Text>
          )}
        </Alert>
      )}

      <ScanPanel onScan={(code) => send(code)} mode="continuous" />

      <div>
        <Text fw={600} mb="xs">
          Son okutulanlar
        </Text>
        {count.recentScans.length === 0 ? (
          <Text size="sm" c="dimmed">
            Henüz okutma yok.
          </Text>
        ) : (
          <Stack gap="xs">
            {count.recentScans.map((s) => (
              <Paper key={s.id} withBorder p="xs">
                <Group justify="space-between" wrap="nowrap">
                  <div>
                    <Text size="sm" fw={500}>
                      {s.itemCode ? `${s.itemCode} · ${s.productName}` : s.productName}
                      {!s.itemCode && ` · ${quantityText(s.quantity, s.unit)}`}
                    </Text>
                    <Text size="xs" c="dimmed">
                      {s.locationName} · {formatTime(s.scannedAt)} · {s.scannedByName}
                      {s.lineType === 'Unexpected' ? ' · beklenmeyen' : ''}
                    </Text>
                  </div>
                  {s.canUndo && (
                    <Button
                      size="compact-sm"
                      variant="subtle"
                      color="red"
                      leftSection={<IconArrowBackUp size={14} />}
                      onClick={() => void undo.mutateAsync(s.lineId)}
                      loading={undo.isPending && undo.variables === s.lineId}
                    >
                      Geri al
                    </Button>
                  )}
                </Group>
              </Paper>
            ))}
          </Stack>
        )}
      </div>

      <Button
        size="lg"
        leftSection={<IconSend size={18} />}
        onClick={submit}
        loading={action.isPending}
      >
        İncelemeye gönder
      </Button>

      <Modal opened={prompt !== null} onClose={() => setPrompt(null)} title={prompt?.title}>
        {prompt && (
          <QuantityForm
            prompt={prompt}
            onSubmit={async (quantity) => {
              setPrompt(null)
              await send(prompt.code, quantity)
            }}
          />
        )}
      </Modal>
    </Stack>
  )
}

function QuantityForm({
  prompt,
  onSubmit,
}: {
  prompt: QuantityPrompt
  onSubmit: (quantity: number) => Promise<void>
}) {
  const [quantity, setQuantity] = useState<number | ''>('')
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault()
        if (typeof quantity === 'number' && quantity > 0) void onSubmit(quantity)
      }}
    >
      <Stack>
        <NumberInput
          label={`Sayılan miktar (${quantityText(1, prompt.unit).replace(/^1 /, '')})`}
          description="Aynı ürünü tekrar okutursanız miktar mevcut sayıma eklenir."
          value={quantity}
          onChange={(v) => setQuantity(typeof v === 'number' ? v : '')}
          min={0}
          decimalScale={3}
          size="lg"
          inputMode="decimal"
          data-autofocus
        />
        <Button type="submit" size="lg" disabled={!(typeof quantity === 'number' && quantity > 0)}>
          Ekle
        </Button>
      </Stack>
    </form>
  )
}
