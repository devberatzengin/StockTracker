import { Alert, Button, Group, Modal, Paper, Stack, Text, Title } from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router'
import { useLocationTree } from '@/features/locations/locationsApi'
import { flattenTree } from '@/features/locations/treeUtils'
import { parseLineErrors, type LineErrors } from '@/features/movements/lineErrors'
import { LocationPicker } from '@/features/movements/LocationPicker'
import { useStockTransfer } from '@/features/movements/movementsApi'
import { emptyErrors, hasErrors } from '@/features/movements/stockInForm'
import { StockLinesEditor } from '@/features/movements/StockLinesEditor'
import { LinesSummary } from '@/features/movements/StockOutPage'
import { toStockLines, validateStockLines, type EditorLine } from '@/features/movements/stockLines'

export function StockTransferPage() {
  const navigate = useNavigate()
  const transfer = useStockTransfer()
  const { data: tree = [] } = useLocationTree()
  const [target, setTarget] = useState<string | null>(null)
  const [lines, setLines] = useState<EditorLine[]>([])
  const [errors, setErrors] = useState<LineErrors>(emptyErrors)
  const [confirming, confirm] = useDisclosure()
  const targetPath = useMemo(
    () => flattenTree(tree).find((f) => f.node.id === target)?.pathLabel,
    [tree, target],
  )

  const review = () => {
    const found = validateStockLines(lines, target)
    if (!target) found.fields.toLocationId = 'Hedef lokasyon seçin.'
    setErrors(found)
    if (!hasErrors(found)) confirm.open()
  }

  const submit = async () => {
    try {
      const result = await transfer.mutateAsync({
        toLocationId: target!,
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
      <Title order={2}>Transfer</Title>
      {errors.general.map((message) => (
        <Alert key={message} color="red">
          {message}
        </Alert>
      ))}

      <Paper withBorder p="md">
        <LocationPicker
          label="Hedef lokasyon"
          required
          value={target}
          onChange={setTarget}
          error={errors.fields.toLocationId}
        />
      </Paper>

      <StockLinesEditor
        lines={lines}
        onChange={setLines}
        errors={errors}
        targetLocationId={target}
      />

      <Group justify="flex-end">
        <Button onClick={review}>Özeti gör</Button>
      </Group>

      <Modal opened={confirming} onClose={confirm.close} title="Transferi onayla">
        <Stack>
          <Text size="sm">
            Hedef: <b>{targetPath}</b>
          </Text>
          <LinesSummary lines={lines} />
          <Group justify="flex-end">
            <Button variant="default" onClick={confirm.close}>
              Geri dön
            </Button>
            <Button onClick={() => void submit()} loading={transfer.isPending}>
              Onayla
            </Button>
          </Group>
        </Stack>
      </Modal>
    </Stack>
  )
}
