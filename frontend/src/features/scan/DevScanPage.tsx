import { Alert, Badge, Box, Button, Group, Paper, Stack, Table, Text, Title } from '@mantine/core'
import { IconAlertTriangle, IconPlayerPlay, IconPlayerStop } from '@tabler/icons-react'
import { BrowserMultiFormatReader, type IScannerControls } from '@zxing/browser'
import { BarcodeFormat, DecodeHintType } from '@zxing/library'
import { useEffect, useRef, useState } from 'react'

const SUPPORTED_FORMATS = [
  BarcodeFormat.QR_CODE,
  BarcodeFormat.EAN_13,
  BarcodeFormat.EAN_8,
  BarcodeFormat.UPC_A,
  BarcodeFormat.UPC_E,
  BarcodeFormat.CODE_128,
]

interface ScanEntry {
  id: number
  text: string
  format: string
  count: number
  lastSeenAt: Date
}

function createReader() {
  const hints = new Map<DecodeHintType, unknown>([
    [DecodeHintType.POSSIBLE_FORMATS, SUPPORTED_FORMATS],
    [DecodeHintType.TRY_HARDER, true],
  ])
  return new BrowserMultiFormatReader(hints)
}

function describeCameraError(error: unknown): string {
  if (error instanceof DOMException) {
    if (error.name === 'NotAllowedError') return 'Kamera izni reddedildi.'
    if (error.name === 'NotFoundError') return 'Kamera bulunamadı.'
    if (error.name === 'NotReadableError')
      return 'Kamera başka bir uygulama tarafından kullanılıyor.'
  }
  return error instanceof Error ? error.message : 'Kamera başlatılamadı.'
}

const timeFormatter = new Intl.DateTimeFormat('tr-TR', {
  timeZone: 'Europe/Istanbul',
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
})

/** Phase 0 camera check: continuous reading with the rear camera. */
export function DevScanPage() {
  const videoRef = useRef<HTMLVideoElement>(null)
  const controlsRef = useRef<IScannerControls | null>(null)
  const nextIdRef = useRef(1)
  const [scanning, setScanning] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [entries, setEntries] = useState<ScanEntry[]>([])

  const cameraAvailable = window.isSecureContext && !!navigator.mediaDevices?.getUserMedia

  useEffect(() => () => controlsRef.current?.stop(), [])

  const addEntry = (text: string, format: string) => {
    setEntries((current) => {
      const [latest, ...rest] = current
      if (latest && latest.text === text && latest.format === format) {
        return [{ ...latest, count: latest.count + 1, lastSeenAt: new Date() }, ...rest]
      }
      navigator.vibrate?.(50)
      const entry = { id: nextIdRef.current++, text, format, count: 1, lastSeenAt: new Date() }
      return [entry, ...current]
    })
  }

  const start = async () => {
    if (!videoRef.current) return
    setError(null)
    try {
      controlsRef.current = await createReader().decodeFromConstraints(
        { video: { facingMode: { ideal: 'environment' } }, audio: false },
        videoRef.current,
        (result) => {
          if (result) {
            addEntry(result.getText(), BarcodeFormat[result.getBarcodeFormat()])
          }
        },
      )
      setScanning(true)
    } catch (e) {
      setError(describeCameraError(e))
    }
  }

  const stop = () => {
    controlsRef.current?.stop()
    controlsRef.current = null
    setScanning(false)
  }

  return (
    <Stack maw={720}>
      <Title order={2}>Tarama denemesi</Title>
      <Text c="dimmed" size="sm">
        Arka kamera ile QR ve barkod (EAN-13, EAN-8, UPC, Code 128) okuma testi.
      </Text>

      {!cameraAvailable && (
        <Alert color="orange" icon={<IconAlertTriangle size={18} />} title="Kamera kullanılamıyor">
          Kamera erişimi yalnızca HTTPS (veya localhost) üzerinden çalışır.
        </Alert>
      )}

      {error && (
        <Alert color="red" icon={<IconAlertTriangle size={18} />} title="Kamera hatası">
          {error}
        </Alert>
      )}

      <Paper withBorder p={0} style={{ overflow: 'hidden' }} bg="dark.9">
        <Box
          component="video"
          ref={videoRef}
          muted
          playsInline
          w="100%"
          display="block"
          style={{ aspectRatio: '4 / 3', objectFit: 'cover' }}
        />
      </Paper>

      <Group>
        {scanning ? (
          <Button color="red" leftSection={<IconPlayerStop size={18} />} onClick={stop}>
            Durdur
          </Button>
        ) : (
          <Button
            leftSection={<IconPlayerPlay size={18} />}
            onClick={start}
            disabled={!cameraAvailable}
          >
            Taramayı başlat
          </Button>
        )}
        {entries.length > 0 && (
          <Button variant="subtle" onClick={() => setEntries([])}>
            Listeyi temizle
          </Button>
        )}
      </Group>

      <Table striped withTableBorder>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Saat</Table.Th>
            <Table.Th>Format</Table.Th>
            <Table.Th>Metin</Table.Th>
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {entries.length === 0 ? (
            <Table.Tr>
              <Table.Td colSpan={3}>
                <Text c="dimmed" size="sm">
                  Henüz okuma yok.
                </Text>
              </Table.Td>
            </Table.Tr>
          ) : (
            entries.map((entry) => (
              <Table.Tr key={entry.id}>
                <Table.Td>{timeFormatter.format(entry.lastSeenAt)}</Table.Td>
                <Table.Td>
                  <Group gap={6} wrap="nowrap">
                    <Badge variant="light">{entry.format}</Badge>
                    {entry.count > 1 && (
                      <Text size="xs" c="dimmed">
                        ×{entry.count}
                      </Text>
                    )}
                  </Group>
                </Table.Td>
                <Table.Td style={{ wordBreak: 'break-all' }}>{entry.text}</Table.Td>
              </Table.Tr>
            ))
          )}
        </Table.Tbody>
      </Table>
    </Stack>
  )
}
