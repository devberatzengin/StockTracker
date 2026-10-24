import {
  ActionIcon,
  Badge,
  Button,
  Group,
  Select,
  Stack,
  Table,
  Text,
  TextInput,
  Tooltip,
} from '@mantine/core'
import { IconCamera, IconPlus, IconTrash } from '@tabler/icons-react'
import { useState } from 'react'
import type { BarcodeFormat } from '@/api/types'
import { BARCODE_FORMAT_LABELS, BARCODE_FORMAT_OPTIONS } from '@/features/catalog/labels'
import { toBarcodeFormat } from '@/features/scan/barcodeFormats'
import { ScanDialog } from '@/features/scan/ScanDialog'

export interface BarcodeRow {
  /** Server id for saved barcodes; a local key for unsaved ones. */
  key: string
  value: string
  format: BarcodeFormat
}

interface Props {
  barcodes: BarcodeRow[]
  /** Resolves when added; throws (with a user-facing message) to keep the input for correction. */
  onAdd: (value: string, format: BarcodeFormat) => Promise<void>
  onRemove: (row: BarcodeRow) => Promise<void>
  disabled?: boolean
}

const normalize = (value: string) => value.replace(/\s+/g, '')

export function BarcodeEditor({ barcodes, onAdd, onRemove, disabled }: Props) {
  const [value, setValue] = useState('')
  const [format, setFormat] = useState<BarcodeFormat>('Ean13')
  const [scanning, setScanning] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const add = async () => {
    const normalized = normalize(value)
    if (!normalized) {
      setError('Barkod değeri girin.')
      return
    }
    if (barcodes.some((b) => b.value === normalized)) {
      setError('Bu barkod listede zaten var.')
      return
    }

    setBusy(true)
    setError(null)
    try {
      await onAdd(normalized, format)
      setValue('')
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Barkod eklenemedi.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <Stack gap="xs">
      {barcodes.length > 0 ? (
        <Table withTableBorder>
          <Table.Tbody>
            {barcodes.map((barcode) => (
              <Table.Tr key={barcode.key}>
                <Table.Td ff="monospace">{barcode.value}</Table.Td>
                <Table.Td w={110}>
                  <Badge variant="light">{BARCODE_FORMAT_LABELS[barcode.format]}</Badge>
                </Table.Td>
                <Table.Td w={40}>
                  <ActionIcon
                    variant="subtle"
                    color="red"
                    aria-label={`${barcode.value} barkodunu sil`}
                    disabled={disabled}
                    onClick={() =>
                      void onRemove(barcode).catch((e: unknown) =>
                        setError(e instanceof Error ? e.message : 'Barkod silinemedi.'),
                      )
                    }
                  >
                    <IconTrash size={16} />
                  </ActionIcon>
                </Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      ) : (
        <Text size="sm" c="dimmed">
          Barkod yok.
        </Text>
      )}

      <Group align="flex-start" gap="xs" wrap="nowrap">
        <TextInput
          aria-label="Barkod değeri"
          placeholder="Barkod"
          value={value}
          onChange={(event) => setValue(event.currentTarget.value)}
          onKeyDown={(event) => {
            // Enter adds the barcode instead of submitting the surrounding form.
            if (event.key === 'Enter') {
              event.preventDefault()
              void add()
            }
          }}
          error={error}
          disabled={disabled}
          style={{ flex: 1 }}
        />
        <Select
          aria-label="Barkod formatı"
          data={BARCODE_FORMAT_OPTIONS}
          value={format}
          onChange={(next) => next && setFormat(next as BarcodeFormat)}
          allowDeselect={false}
          disabled={disabled}
          w={130}
        />
        <Tooltip label="Kamerayla okut">
          <ActionIcon
            variant="light"
            size="lg"
            aria-label="Kamerayla okut"
            onClick={() => setScanning(true)}
            disabled={disabled}
          >
            <IconCamera size={18} />
          </ActionIcon>
        </Tooltip>
        <Button
          variant="light"
          leftSection={<IconPlus size={16} />}
          onClick={() => void add()}
          loading={busy}
          disabled={disabled}
        >
          Ekle
        </Button>
      </Group>

      <ScanDialog
        opened={scanning}
        onClose={() => setScanning(false)}
        title="Barkod okut"
        onScan={(scanned, scannedFormat) => {
          // The value is only filled in; the user confirms it with "Ekle".
          setValue(normalize(scanned))
          setFormat(toBarcodeFormat(scannedFormat) ?? format)
          setError(null)
          setScanning(false)
        }}
      />
    </Stack>
  )
}
