import {
  ActionIcon,
  Alert,
  Button,
  Group,
  List,
  Modal,
  NumberInput,
  Paper,
  Stack,
  TagsInput,
  Text,
  Title,
} from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import { IconPlus, IconTrash } from '@tabler/icons-react'
import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router'
import { formatQuantity, UNIT_LABELS } from '@/features/catalog/labels'
import { useLocationTree } from '@/features/locations/locationsApi'
import { flattenTree } from '@/features/locations/treeUtils'
import { parseLineErrors, type LineErrors } from '@/features/movements/lineErrors'
import {
  emptyErrors,
  hasErrors,
  MAX_ITEMS_PER_LINE,
  MAX_LINES,
  newInLine,
  splitSerialNumbers,
  validateInLines,
  type InLine,
} from '@/features/movements/stockInForm'
import { LocationPicker } from '@/features/movements/LocationPicker'
import { useStockIn } from '@/features/movements/movementsApi'
import { ProductPicker } from '@/features/movements/ProductPicker'

export function StockInPage() {
  const navigate = useNavigate()
  const stockIn = useStockIn()
  const { data: tree = [] } = useLocationTree()
  const [target, setTarget] = useState<string | null>(null)
  const [lines, setLines] = useState<InLine[]>([newInLine()])
  const [errors, setErrors] = useState<LineErrors>(emptyErrors)
  const [confirming, confirm] = useDisclosure()

  const targetPath = useMemo(
    () => flattenTree(tree).find((f) => f.node.id === target)?.pathLabel,
    [tree, target],
  )

  const update = (key: string, patch: Partial<InLine>) =>
    setLines((current) => current.map((l) => (l.key === key ? { ...l, ...patch } : l)))

  const review = () => {
    const found = validateInLines(target, lines)
    setErrors(found)
    if (!hasErrors(found)) confirm.open()
  }

  const submit = async () => {
    try {
      const result = await stockIn.mutateAsync({
        toLocationId: target!,
        lines: lines.map((l) => ({
          productId: l.product!.id,
          quantity: Number(l.quantity),
          serialNumbers: l.product!.trackingType === 'Serialized' ? l.serialNumbers : null,
        })),
      })
      navigate(`/movements/ref/${result.referenceId}`)
    } catch (e) {
      confirm.close()
      setErrors(parseLineErrors(e))
    }
  }

  return (
    <Stack maw={860}>
      <Title order={2}>Stok girişi</Title>

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

      <Stack gap="sm">
        {lines.map((line, i) => {
          const serialized = line.product?.trackingType === 'Serialized'
          const lineErrors = errors.lines[i] ?? {}
          return (
            <Paper key={line.key} withBorder p="md" data-testid={`in-line-${i}`}>
              <Stack gap="xs">
                <Group justify="space-between">
                  <Text size="sm" fw={600}>
                    Satır {i + 1}
                  </Text>
                  <ActionIcon
                    variant="subtle"
                    color="red"
                    aria-label={`Satır ${i + 1} sil`}
                    onClick={() => setLines((current) => current.filter((l) => l.key !== line.key))}
                    disabled={lines.length === 1}
                  >
                    <IconTrash size={16} />
                  </ActionIcon>
                </Group>
                <ProductPicker
                  value={line.product}
                  onChange={(product) => update(line.key, { product, serialNumbers: [] })}
                  error={lineErrors.productId}
                />
                <Group grow align="flex-start">
                  <NumberInput
                    label={
                      serialized
                        ? 'Adet'
                        : `Miktar${line.product ? ` (${UNIT_LABELS[line.product.unit]})` : ''}`
                    }
                    value={line.quantity}
                    onChange={(value) =>
                      update(line.key, { quantity: typeof value === 'number' ? value : '' })
                    }
                    min={0}
                    max={serialized ? MAX_ITEMS_PER_LINE : undefined}
                    allowDecimal={!serialized}
                    decimalScale={3}
                    error={lineErrors.quantity}
                  />
                </Group>
                {serialized && (
                  <TagsInput
                    label="Seri numaraları (opsiyonel)"
                    description="Her seri numarasından sonra Enter'a basın. Girilmeyenler seri numarasız oluşturulur."
                    value={line.serialNumbers}
                    onChange={(serialNumbers) =>
                      update(line.key, { serialNumbers: splitSerialNumbers(serialNumbers) })
                    }
                    maxTags={typeof line.quantity === 'number' ? line.quantity : undefined}
                    error={lineErrors.serialNumbers}
                    splitChars={[',', ' ']}
                    clearable
                  />
                )}
                {lineErrors.line && (
                  <Text size="sm" c="red">
                    {lineErrors.line}
                  </Text>
                )}
              </Stack>
            </Paper>
          )
        })}
      </Stack>

      <Group justify="space-between">
        <Button
          variant="light"
          leftSection={<IconPlus size={16} />}
          onClick={() => setLines((current) => [...current, newInLine()])}
          disabled={lines.length >= MAX_LINES}
        >
          Satır ekle
        </Button>
        <Button onClick={review}>Özeti gör</Button>
      </Group>

      <Modal opened={confirming} onClose={confirm.close} title="Girişi onayla">
        <Stack>
          <Text size="sm">
            Hedef: <b>{targetPath}</b>
          </Text>
          <List size="sm" spacing={4}>
            {lines.map((l) => (
              <List.Item key={l.key}>
                {l.product?.name} ·{' '}
                {l.product?.trackingType === 'Serialized'
                  ? `${l.quantity} adet${l.serialNumbers.length ? ` (${l.serialNumbers.join(', ')})` : ''}`
                  : `${formatQuantity(Number(l.quantity))} ${(l.product ? UNIT_LABELS[l.product.unit] : '').toLocaleLowerCase('tr-TR')}`}
              </List.Item>
            ))}
          </List>
          <Group justify="flex-end">
            <Button variant="default" onClick={confirm.close}>
              Geri dön
            </Button>
            <Button onClick={() => void submit()} loading={stockIn.isPending}>
              Onayla
            </Button>
          </Group>
        </Stack>
      </Modal>
    </Stack>
  )
}
