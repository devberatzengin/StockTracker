import {
  Alert,
  Button,
  Group,
  List,
  Modal,
  Paper,
  Select,
  Stack,
  Text,
  Textarea,
  Title,
} from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import { useState } from 'react'
import { useNavigate } from 'react-router'
import type { MovementReason } from '@/api/types'
import { formatQuantity } from '@/features/catalog/labels'
import { MOVEMENT_REASON_LABELS, MOVEMENT_REASON_OPTIONS } from '@/features/movements/labels'
import { parseLineErrors, type LineErrors } from '@/features/movements/lineErrors'
import { useStockOut } from '@/features/movements/movementsApi'
import { emptyErrors, hasErrors } from '@/features/movements/stockInForm'
import { StockLinesEditor } from '@/features/movements/StockLinesEditor'
import { toStockLines, validateStockLines, type EditorLine } from '@/features/movements/stockLines'

const NOTE_MAX = 500

export function StockOutPage() {
  const navigate = useNavigate()
  const stockOut = useStockOut()
  const [reason, setReason] = useState<MovementReason>('Consumed')
  const [note, setNote] = useState('')
  const [lines, setLines] = useState<EditorLine[]>([])
  const [errors, setErrors] = useState<LineErrors>(emptyErrors)
  const [confirming, confirm] = useDisclosure()

  const review = () => {
    const found = validateStockLines(lines)
    if (reason === 'Other' && !note.trim()) found.fields.note = 'Sebep "Diğer" ise not zorunludur.'
    if (note.length > NOTE_MAX) found.fields.note = `Not en fazla ${NOTE_MAX} karakter olabilir.`
    setErrors(found)
    if (!hasErrors(found)) confirm.open()
  }

  const submit = async () => {
    try {
      const result = await stockOut.mutateAsync({
        reason,
        note: note.trim() || null,
        lines: toStockLines(lines),
      })
      navigate(`/movements/ref/${result.referenceId}`)
    } catch (e) {
      confirm.close()
      setErrors(parseLineErrors(e))
    }
  }

  return (
    <Stack maw={860}>
      <Title order={2}>Stok çıkışı</Title>
      {errors.general.map((message) => (
        <Alert key={message} color="red">
          {message}
        </Alert>
      ))}

      <Paper withBorder p="md">
        <Stack gap="sm">
          <Select
            label="Sebep"
            required
            data={MOVEMENT_REASON_OPTIONS}
            value={reason}
            onChange={(value) => value && setReason(value as MovementReason)}
            allowDeselect={false}
            error={errors.fields.reason}
          />
          <Textarea
            label="Not"
            required={reason === 'Other'}
            description={reason === 'Other' ? 'Sebep "Diğer" ise not zorunludur.' : undefined}
            value={note}
            onChange={(event) => setNote(event.currentTarget.value)}
            maxLength={NOTE_MAX}
            autosize
            minRows={2}
            error={errors.fields.note}
          />
        </Stack>
      </Paper>

      <StockLinesEditor lines={lines} onChange={setLines} errors={errors} />

      <Group justify="flex-end">
        <Button color="red" onClick={review}>
          Özeti gör
        </Button>
      </Group>

      <Modal opened={confirming} onClose={confirm.close} title="Çıkışı onayla">
        <Stack>
          <Text size="sm">
            Sebep: <b>{MOVEMENT_REASON_LABELS[reason]}</b>
            {note.trim() && ` · ${note.trim()}`}
          </Text>
          <LinesSummary lines={lines} />
          <Group justify="flex-end">
            <Button variant="default" onClick={confirm.close}>
              Geri dön
            </Button>
            <Button color="red" onClick={() => void submit()} loading={stockOut.isPending}>
              Onayla
            </Button>
          </Group>
        </Stack>
      </Modal>
    </Stack>
  )
}

export function LinesSummary({ lines }: { lines: EditorLine[] }) {
  return (
    <List size="sm" spacing={4}>
      {lines.map((l) => (
        <List.Item key={l.key}>
          {l.kind === 'item'
            ? `${l.title}${l.subtitle ? ` — ${l.subtitle}` : ''}`
            : `${l.product?.name} · ${formatQuantity(Number(l.quantity))}`}
        </List.Item>
      ))}
    </List>
  )
}
