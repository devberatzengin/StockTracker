import { Button, Center, Group, Loader, Modal, Stack, Text, TextInput } from '@mantine/core'
import { IconSearch } from '@tabler/icons-react'
import type { BarcodeFormat as ZxingFormat } from '@zxing/library'
import { lazy, Suspense, useMemo, useRef, useState, type ReactNode } from 'react'
import { beep, createDuplicateFilter, vibrate } from '@/features/scan/scanFeedback'

// The barcode reader library is large; load it only when a scan dialog opens.
const CameraScanner = lazy(async () => ({
  default: (await import('@/features/scan/CameraScanner')).CameraScanner,
}))

export type ScanMode = 'single' | 'continuous'

interface Props {
  opened: boolean
  onClose: () => void
  /**
   * Called for every accepted read; `format` is null for manual entry.
   * In single mode further reads are ignored until the returned promise settles;
   * the caller closes the dialog when it is done.
   */
  onScan: (value: string, format: ZxingFormat | null) => void | Promise<void>
  mode?: ScanMode
  title?: string
  /** Extra content under the scanner, e.g. a "not found" message. */
  children?: ReactNode
}

export function ScanDialog({
  opened,
  onClose,
  onScan,
  mode = 'single',
  title = 'Tara',
  children,
}: Props) {
  return (
    <Modal opened={opened} onClose={onClose} title={title} size="md">
      {/* Content unmounts on close, which also stops the camera. */}
      <ScanPanel onScan={onScan} mode={mode}>
        {children}
      </ScanPanel>
    </Modal>
  )
}

/** Camera + manual entry, usable inside a dialog or directly on a page (e.g. the scan-and-move flow). */
export function ScanPanel({ onScan, mode, children }: Pick<Props, 'onScan' | 'mode' | 'children'>) {
  const [manualValue, setManualValue] = useState('')
  const [busy, setBusy] = useState(false)
  const busyRef = useRef(false)
  const isNew = useMemo(() => createDuplicateFilter(2000), [])

  const handle = async (value: string, format: ZxingFormat | null) => {
    if (mode === 'single' && busyRef.current) return
    busyRef.current = true
    setBusy(true)
    try {
      await onScan(value, format)
    } finally {
      busyRef.current = false
      setBusy(false)
    }
  }

  const onDetected = (text: string, format: ZxingFormat) => {
    if (!isNew(text) || (mode === 'single' && busyRef.current)) return
    vibrate()
    beep()
    void handle(text, format)
  }

  const submitManual = () => {
    const value = manualValue.trim()
    if (!value) return
    setManualValue('')
    void handle(value, null)
  }

  return (
    <Stack>
      <Suspense
        fallback={
          <Center py="md">
            <Loader size="sm" aria-label="Kamera yükleniyor" />
          </Center>
        }
      >
        <CameraScanner paused={mode === 'single' && busy} onDetected={onDetected} />
      </Suspense>

      <form
        onSubmit={(event) => {
          event.preventDefault()
          submitManual()
        }}
      >
        <Group align="flex-end" gap="xs" wrap="nowrap">
          <TextInput
            label="Elle giriş"
            placeholder="Kod (PRD-…, LOC-…) ya da barkod"
            value={manualValue}
            onChange={(event) => setManualValue(event.currentTarget.value)}
            style={{ flex: 1 }}
            autoComplete="off"
          />
          <Button
            type="submit"
            leftSection={<IconSearch size={16} />}
            loading={busy && mode === 'single'}
          >
            Bul
          </Button>
        </Group>
      </form>

      {mode === 'continuous' && (
        <Text size="xs" c="dimmed">
          Sürekli mod: kodları art arda okutabilirsiniz.
        </Text>
      )}

      {children}
    </Stack>
  )
}
