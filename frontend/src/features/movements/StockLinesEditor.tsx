import {
  ActionIcon,
  Alert,
  Badge,
  Button,
  Group,
  NumberInput,
  Paper,
  Select,
  Stack,
  Text,
} from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import { IconBarcode, IconPlus, IconTrash } from '@tabler/icons-react'
import { useState } from 'react'
import { getErrorMessage, isApiError } from '@/api/client'
import { formatQuantity, UNIT_LABELS } from '@/features/catalog/labels'
import type { LineErrors } from '@/features/movements/lineErrors'
import { useProductStock } from '@/features/movements/movementsApi'
import { ProductPicker } from '@/features/movements/ProductPicker'
import {
  MAX_STOCK_LINES,
  newQuantityLine,
  type EditorLine,
  type ItemLine,
  type QuantityLine,
} from '@/features/movements/stockLines'
import { lookupScan } from '@/features/scan/scanApi'
import { ScanDialog } from '@/features/scan/ScanDialog'

interface Props {
  lines: EditorLine[]
  onChange: (lines: EditorLine[]) => void
  errors: LineErrors
  /** Target of a transfer: cannot be a source. */
  targetLocationId?: string | null
}

/** Lines for out and transfer: items (scanned or typed code) or quantities from a source location. */
export function StockLinesEditor({ lines, onChange, errors, targetLocationId }: Props) {
  const [scanning, scan] = useDisclosure()
  const [scanError, setScanError] = useState<string | null>(null)

  const update = (key: string, patch: Partial<QuantityLine>) =>
    onChange(lines.map((l) => (l.key === key && l.kind === 'quantity' ? { ...l, ...patch } : l)))
  const remove = (key: string) => onChange(lines.filter((l) => l.key !== key))

  const addItem = async (input: string) => {
    setScanError(null)
    try {
      const result = await lookupScan(input)
      if (result.type !== 'item') {
        setScanError(
          `"${result.title}" bir adet (Item) değil. Adetin kendi etiketini (ITM-…) okutun.`,
        )
        return
      }
      if (lines.some((l) => l.kind === 'item' && l.itemId === result.id)) {
        setScanError(`${result.code} zaten listede.`)
        return
      }
      const line: ItemLine = {
        key: crypto.randomUUID(),
        kind: 'item',
        itemId: result.id,
        code: result.code,
        title: result.title,
        subtitle: result.subtitle ?? null,
      }
      onChange([...lines, line])
      scan.close()
    } catch (e) {
      setScanError(isApiError(e, 404) ? 'Bu koda ait adet bulunamadı.' : getErrorMessage(e))
    }
  }

  return (
    <Stack gap="sm">
      {lines.map((line, i) =>
        line.kind === 'item' ? (
          <Paper key={line.key} withBorder p="md">
            <Group justify="space-between" wrap="nowrap" align="flex-start">
              <div>
                <Group gap={6}>
                  <Badge variant="light" size="sm">
                    Adet
                  </Badge>
                  <Text fw={600} size="sm" ff="monospace">
                    {line.title}
                  </Text>
                </Group>
                {line.subtitle && (
                  <Text size="xs" c="dimmed">
                    {line.subtitle}
                  </Text>
                )}
                {errors.lines[i] && (
                  <Text size="sm" c="red" mt={4}>
                    {Object.values(errors.lines[i])[0]}
                  </Text>
                )}
              </div>
              <ActionIcon
                variant="subtle"
                color="red"
                aria-label={`Satır ${i + 1} sil`}
                onClick={() => remove(line.key)}
              >
                <IconTrash size={16} />
              </ActionIcon>
            </Group>
          </Paper>
        ) : (
          <QuantityLineRow
            key={line.key}
            index={i}
            line={line}
            errors={errors.lines[i] ?? {}}
            targetLocationId={targetLocationId}
            onChange={(patch) => update(line.key, patch)}
            onRemove={() => remove(line.key)}
          />
        ),
      )}

      <Group>
        <Button
          variant="light"
          leftSection={<IconBarcode size={16} />}
          onClick={scan.open}
          disabled={lines.length >= MAX_STOCK_LINES}
        >
          Adet ekle
        </Button>
        <Button
          variant="light"
          leftSection={<IconPlus size={16} />}
          onClick={() => onChange([...lines, newQuantityLine()])}
          disabled={lines.length >= MAX_STOCK_LINES}
        >
          Miktar satırı ekle
        </Button>
      </Group>

      <ScanDialog
        opened={scanning}
        onClose={() => {
          setScanError(null)
          scan.close()
        }}
        onScan={addItem}
        title="Adet okut"
      >
        {scanError && <Alert color="orange">{scanError}</Alert>}
      </ScanDialog>
    </Stack>
  )
}

function QuantityLineRow({
  index,
  line,
  errors,
  targetLocationId,
  onChange,
  onRemove,
}: {
  index: number
  line: QuantityLine
  errors: Record<string, string>
  targetLocationId?: string | null
  onChange: (patch: Partial<QuantityLine>) => void
  onRemove: () => void
}) {
  const quantityProduct = line.product?.trackingType === 'Quantity' ? line.product : null
  const { data: stock } = useProductStock(quantityProduct?.id)
  const sources = (stock?.locations ?? []).filter((l) => l.locationId !== targetLocationId)
  const selected = sources.find((s) => s.locationId === line.fromLocationId)
  const unit = line.product ? UNIT_LABELS[line.product.unit].toLocaleLowerCase('tr-TR') : ''

  return (
    <Paper withBorder p="md">
      <Stack gap="xs">
        <Group justify="space-between">
          <Text size="sm" fw={600}>
            Satır {index + 1}
          </Text>
          <ActionIcon
            variant="subtle"
            color="red"
            aria-label={`Satır ${index + 1} sil`}
            onClick={onRemove}
          >
            <IconTrash size={16} />
          </ActionIcon>
        </Group>
        <ProductPicker
          value={line.product}
          onChange={(product) => onChange({ product, fromLocationId: null })}
          error={
            errors.productId ??
            (line.product?.trackingType === 'Serialized'
              ? 'Tekil takipli ürünlerde "Adet ekle" ile adetin kodunu okutun.'
              : undefined)
          }
        />
        <Group grow align="flex-start">
          <Select
            label="Kaynak lokasyon"
            placeholder={
              quantityProduct ? (sources.length ? 'Seçin' : 'Stok yok') : 'Önce ürün seçin'
            }
            data={sources.map((s) => ({
              value: s.locationId,
              label: `${s.locationPath} (${formatQuantity(s.quantity)} ${unit})`,
            }))}
            value={line.fromLocationId}
            onChange={(fromLocationId) => onChange({ fromLocationId })}
            disabled={!quantityProduct}
            error={errors.fromLocationId}
            searchable
          />
          <NumberInput
            label={`Miktar${unit ? ` (${unit})` : ''}`}
            description={
              selected ? `Mevcut: ${formatQuantity(selected.quantity)} ${unit}` : undefined
            }
            value={line.quantity}
            onChange={(value) => onChange({ quantity: typeof value === 'number' ? value : '' })}
            min={0}
            max={selected?.quantity}
            decimalScale={3}
            error={errors.quantity}
          />
        </Group>
        {errors.toLocationId && (
          <Text size="sm" c="red">
            {errors.toLocationId}
          </Text>
        )}
      </Stack>
    </Paper>
  )
}
