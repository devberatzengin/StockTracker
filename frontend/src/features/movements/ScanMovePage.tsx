import {
  ActionIcon,
  Alert,
  Badge,
  Box,
  Button,
  Group,
  Modal,
  NumberInput,
  Paper,
  Radio,
  Stack,
  Stepper,
  Text,
  Title,
} from '@mantine/core'
import { IconArrowLeft, IconCheck, IconTrash } from '@tabler/icons-react'
import { useState } from 'react'
import { useNavigate } from 'react-router'
import { getErrorMessage, isApiError } from '@/api/client'
import type { LocationQuantityDto, ScanResult } from '@/api/types'
import { formatQuantity, UNIT_LABELS } from '@/features/catalog/labels'
import { parseLineErrors, type LineErrors } from '@/features/movements/lineErrors'
import { fetchProductStock, useStockTransfer } from '@/features/movements/movementsApi'
import { fetchPickedProduct, type PickedProduct } from '@/features/movements/pickedProduct'
import { emptyErrors } from '@/features/movements/stockInForm'
import { toStockLines, type EditorLine } from '@/features/movements/stockLines'
import { lookupScan } from '@/features/scan/scanApi'
import { ScanPanel } from '@/features/scan/ScanDialog'

interface QuantityPrompt {
  product: PickedProduct
  sources: LocationQuantityDto[]
}

/**
 * Mobile "scan and move": (1) scan the target shelf, (2) scan items and products continuously,
 * (3) review and confirm. Creates one transfer operation.
 */
export function ScanMovePage() {
  const navigate = useNavigate()
  const transfer = useStockTransfer()
  const [step, setStep] = useState(0)
  const [target, setTarget] = useState<ScanResult | null>(null)
  const [lines, setLines] = useState<EditorLine[]>([])
  const [message, setMessage] = useState<{ color: string; text: string } | null>(null)
  const [prompt, setPrompt] = useState<QuantityPrompt | null>(null)
  const [errors, setErrors] = useState<LineErrors>(emptyErrors)

  const scanTarget = async (input: string) => {
    setMessage(null)
    try {
      const result = await lookupScan(input)
      if (result.type !== 'location') {
        setMessage({
          color: 'orange',
          text: `"${result.title}" bir lokasyon değil. Önce hedef rafın etiketini okutun.`,
        })
        return
      }
      if (!result.isActive) {
        setMessage({ color: 'orange', text: 'Bu lokasyon pasif.' })
        return
      }
      setTarget(result)
      setStep(1)
    } catch (e) {
      setMessage({
        color: 'red',
        text: isApiError(e, 404) ? 'Lokasyon bulunamadı.' : getErrorMessage(e),
      })
    }
  }

  const scanThing = async (input: string) => {
    setMessage(null)
    try {
      const result = await lookupScan(input)
      if (result.type === 'item') {
        if (lines.some((l) => l.kind === 'item' && l.itemId === result.id)) {
          setMessage({ color: 'yellow', text: `${result.code} zaten listede.` })
          return
        }
        setLines((current) => [
          ...current,
          {
            key: crypto.randomUUID(),
            kind: 'item',
            itemId: result.id,
            code: result.code,
            title: result.title,
            subtitle: result.subtitle ?? null,
          },
        ])
        setMessage({ color: 'green', text: `Eklendi: ${result.title}` })
      } else if (result.type === 'product') {
        const product = await fetchPickedProduct(result.id)
        if (product.trackingType === 'Serialized') {
          setMessage({
            color: 'orange',
            text: `${product.name} tekil takipli: adetin kendi etiketini (ITM-…) okutun.`,
          })
          return
        }
        const stock = await fetchProductStock(product.id)
        const sources = stock.locations.filter((l) => l.locationId !== target?.id)
        if (sources.length === 0) {
          setMessage({ color: 'orange', text: `${product.name} için taşınabilecek stok yok.` })
          return
        }
        setPrompt({ product, sources })
      } else {
        setMessage({
          color: 'yellow',
          text: 'Hedef zaten seçili. Taşınacak adetleri ya da ürünleri okutun.',
        })
      }
    } catch (e) {
      setMessage({
        color: 'red',
        text: isApiError(e, 404) ? 'Kayıt bulunamadı.' : getErrorMessage(e),
      })
    }
  }

  const confirm = async () => {
    setErrors(emptyErrors)
    try {
      const result = await transfer.mutateAsync({
        toLocationId: target!.id,
        lines: toStockLines(lines),
      })
      navigate(`/movements/ref/${result.referenceId}`)
    } catch (e) {
      setErrors(parseLineErrors(e))
    }
  }

  const goTo = (next: number) => {
    setMessage(null)
    setStep(next)
  }

  const remove = (key: string) => setLines((current) => current.filter((l) => l.key !== key))

  return (
    <Stack maw={560} mx="auto" pb={96}>
      <Title order={2}>Tara-taşı</Title>
      <Stepper
        active={step}
        size="sm"
        allowNextStepsSelect={false}
        onStepClick={(s) => s < step && goTo(s)}
      >
        <Stepper.Step label="Hedef" description={target?.title} />
        <Stepper.Step
          label="Okut"
          description={lines.length ? `${lines.length} satır` : undefined}
        />
        <Stepper.Step label="Onay" />
      </Stepper>

      {message && <Alert color={message.color}>{message.text}</Alert>}

      {step === 0 && (
        <>
          <Text size="sm">Ürünlerin taşınacağı rafın etiketini okutun.</Text>
          <ScanPanel onScan={scanTarget} mode="single" />
        </>
      )}

      {step === 1 && target && (
        <>
          <Paper withBorder p="sm" bg="blue.0">
            <Text size="xs" c="dimmed">
              Hedef
            </Text>
            <Text fw={600}>{target.title}</Text>
            {target.subtitle && (
              <Text size="xs" c="dimmed">
                {target.subtitle}
              </Text>
            )}
          </Paper>
          <ScanPanel onScan={scanThing} mode="continuous" />
          <ScannedLines lines={lines} onRemove={remove} />
        </>
      )}

      {step === 2 && target && (
        <>
          <Paper withBorder p="sm">
            <Text size="sm">
              <b>{lines.length}</b> satır <b>{target.title}</b> lokasyonuna taşınacak.
            </Text>
          </Paper>
          {errors.general.map((text) => (
            <Alert key={text} color="red">
              {text}
            </Alert>
          ))}
          <ScannedLines lines={lines} onRemove={remove} errors={errors} />
        </>
      )}

      {/* Sticky action bar: large touch targets at the bottom of the phone screen. */}
      {step > 0 && (
        <Box
          pos="fixed"
          bottom={0}
          left={0}
          right={0}
          p="sm"
          bg="var(--mantine-color-body)"
          style={{ borderTop: '1px solid var(--mantine-color-default-border)', zIndex: 150 }}
        >
          <Group maw={560} mx="auto" grow>
            <Button
              size="lg"
              variant="default"
              leftSection={<IconArrowLeft size={18} />}
              onClick={() => goTo(step - 1)}
            >
              Geri
            </Button>
            {step === 1 ? (
              <Button size="lg" onClick={() => goTo(2)} disabled={lines.length === 0}>
                Özet ({lines.length})
              </Button>
            ) : (
              <Button
                size="lg"
                leftSection={<IconCheck size={18} />}
                onClick={() => void confirm()}
                loading={transfer.isPending}
              >
                Taşı
              </Button>
            )}
          </Group>
        </Box>
      )}

      <QuantityModal
        prompt={prompt}
        onClose={() => setPrompt(null)}
        onAdd={(product, fromLocationId, quantity, sourcePath) => {
          setLines((current) => [
            ...current,
            {
              key: crypto.randomUUID(),
              kind: 'quantity',
              product,
              fromLocationId,
              quantity,
              sourcePath,
            },
          ])
          setMessage({
            color: 'green',
            text: `Eklendi: ${formatQuantity(quantity)} ${UNIT_LABELS[product.unit].toLocaleLowerCase('tr-TR')} ${product.name} (${sourcePath})`,
          })
          setPrompt(null)
        }}
      />
    </Stack>
  )
}

function ScannedLines({
  lines,
  onRemove,
  errors,
}: {
  lines: EditorLine[]
  onRemove: (key: string) => void
  errors?: LineErrors
}) {
  if (lines.length === 0) {
    return (
      <Text size="sm" c="dimmed">
        Henüz bir şey okutulmadı.
      </Text>
    )
  }
  return (
    <Stack gap="xs">
      {lines.map((line, i) => (
        <Paper key={line.key} withBorder p="sm">
          <Group justify="space-between" wrap="nowrap">
            <div>
              <Group gap={6}>
                <Badge size="xs" variant="light" color={line.kind === 'item' ? 'blue' : 'teal'}>
                  {line.kind === 'item' ? 'Adet' : 'Miktar'}
                </Badge>
                <Text size="sm" fw={600}>
                  {line.kind === 'item'
                    ? line.title
                    : `${line.product?.name} · ${formatQuantity(Number(line.quantity))}`}
                </Text>
              </Group>
              {(line.kind === 'item' ? line.subtitle : line.sourcePath) && (
                <Text size="xs" c="dimmed">
                  {line.kind === 'item' ? line.subtitle : `Kaynak: ${line.sourcePath}`}
                </Text>
              )}
              {errors?.lines[i] && (
                <Text size="xs" c="red">
                  {Object.values(errors.lines[i])[0]}
                </Text>
              )}
            </div>
            <ActionIcon
              size="lg"
              variant="subtle"
              color="red"
              aria-label={`Satır ${i + 1} sil`}
              onClick={() => onRemove(line.key)}
            >
              <IconTrash size={18} />
            </ActionIcon>
          </Group>
        </Paper>
      ))}
    </Stack>
  )
}

function QuantityModal({
  prompt,
  onClose,
  onAdd,
}: {
  prompt: QuantityPrompt | null
  onClose: () => void
  onAdd: (
    product: PickedProduct,
    fromLocationId: string,
    quantity: number,
    sourcePath: string,
  ) => void
}) {
  return (
    <Modal opened={!!prompt} onClose={onClose} title={prompt?.product.name} fullScreen={false}>
      {prompt && <QuantityForm prompt={prompt} onAdd={onAdd} />}
    </Modal>
  )
}

function QuantityForm({
  prompt,
  onAdd,
}: {
  prompt: QuantityPrompt
  onAdd: (
    product: PickedProduct,
    fromLocationId: string,
    quantity: number,
    sourcePath: string,
  ) => void
}) {
  const [source, setSource] = useState<string>(prompt.sources[0].locationId)
  const [quantity, setQuantity] = useState<number | ''>('')
  const selected = prompt.sources.find((s) => s.locationId === source)!
  const unit = UNIT_LABELS[prompt.product.unit].toLocaleLowerCase('tr-TR')
  const invalid = quantity === '' || quantity <= 0 || quantity > selected.quantity

  return (
    <Stack>
      <Radio.Group label="Kaynak lokasyon" value={source} onChange={setSource}>
        <Stack gap="xs" mt="xs">
          {prompt.sources.map((s) => (
            <Radio
              key={s.locationId}
              value={s.locationId}
              label={`${s.locationPath} — ${formatQuantity(s.quantity)} ${unit}`}
            />
          ))}
        </Stack>
      </Radio.Group>
      <NumberInput
        label={`Miktar (${unit})`}
        description={`En fazla ${formatQuantity(selected.quantity)}`}
        value={quantity}
        onChange={(value) => setQuantity(typeof value === 'number' ? value : '')}
        min={0}
        max={selected.quantity}
        decimalScale={3}
        size="lg"
        inputMode="decimal"
        data-autofocus
      />
      <Button
        size="lg"
        disabled={invalid}
        onClick={() => onAdd(prompt.product, source, Number(quantity), selected.locationPath)}
      >
        Ekle
      </Button>
    </Stack>
  )
}
