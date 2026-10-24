import { ActionIcon, Alert, Box, Group, Paper, Stack, Text, Tooltip } from '@mantine/core'
import { IconAlertTriangle, IconBolt, IconBoltOff, IconCameraRotate } from '@tabler/icons-react'
import { BrowserMultiFormatReader, type IScannerControls } from '@zxing/browser'
import { DecodeHintType, type BarcodeFormat as ZxingFormat } from '@zxing/library'
import { useEffect, useRef, useState } from 'react'
import { SCANNED_FORMATS } from '@/features/scan/barcodeFormats'

type CameraStatus = 'starting' | 'running' | 'denied' | 'error'

interface Props {
  /** Results are ignored while paused (the camera keeps running). */
  paused?: boolean
  onDetected: (text: string, format: ZxingFormat) => void
}

const BACK_CAMERA = /back|rear|environment|arka/i

function createReader() {
  const hints = new Map<DecodeHintType, unknown>([
    [DecodeHintType.POSSIBLE_FORMATS, SCANNED_FORMATS],
    [DecodeHintType.TRY_HARDER, true],
  ])
  return new BrowserMultiFormatReader(hints)
}

const isCameraAvailable = () => window.isSecureContext && !!navigator.mediaDevices?.getUserMedia

/** Live camera preview that reports decoded codes; rear camera first, with switch and torch buttons. */
export function CameraScanner({ paused = false, onDetected }: Props) {
  const videoRef = useRef<HTMLVideoElement>(null)
  const controlsRef = useRef<IScannerControls | null>(null)
  const onDetectedRef = useRef(onDetected)
  const pausedRef = useRef(paused)
  const [status, setStatus] = useState<CameraStatus>('starting')
  const [devices, setDevices] = useState<MediaDeviceInfo[]>([])
  // null = rear camera via facingMode; afterwards the device picked with the switch button.
  const [deviceId, setDeviceId] = useState<string | null>(null)
  const [torchOn, setTorchOn] = useState(false)
  const [torchSupported, setTorchSupported] = useState(false)

  useEffect(() => {
    onDetectedRef.current = onDetected
    pausedRef.current = paused
  })

  const available = isCameraAvailable()

  useEffect(() => {
    if (!available || !videoRef.current) return
    let cancelled = false
    const video = videoRef.current
    const reader = createReader()
    const callback = (
      result: { getText(): string; getBarcodeFormat(): ZxingFormat } | undefined,
    ) => {
      if (result && !pausedRef.current) {
        onDetectedRef.current(result.getText(), result.getBarcodeFormat())
      }
    }

    const start = deviceId
      ? reader.decodeFromVideoDevice(deviceId, video, callback)
      : reader.decodeFromConstraints(
          { video: { facingMode: { ideal: 'environment' } }, audio: false },
          video,
          callback,
        )

    start
      .then(async (controls) => {
        if (cancelled) {
          controls.stop()
          return
        }
        controlsRef.current = controls
        setStatus('running')
        setTorchSupported(typeof controls.switchTorch === 'function')
        // Device labels are only available after permission was granted.
        setDevices(await BrowserMultiFormatReader.listVideoInputDevices())
      })
      .catch((error: unknown) => {
        if (cancelled) return
        setStatus(
          error instanceof DOMException && error.name === 'NotAllowedError' ? 'denied' : 'error',
        )
      })

    return () => {
      cancelled = true
      controlsRef.current?.stop()
      controlsRef.current = null
    }
  }, [available, deviceId])

  const switchCamera = () => {
    if (devices.length < 2) return
    const currentIndex = deviceId
      ? devices.findIndex((d) => d.deviceId === deviceId)
      : devices.findIndex((d) => BACK_CAMERA.test(d.label))
    const next = devices[(currentIndex + 1) % devices.length]
    setTorchOn(false)
    setDeviceId(next.deviceId)
  }

  const toggleTorch = async () => {
    try {
      await controlsRef.current?.switchTorch?.(!torchOn)
      setTorchOn(!torchOn)
    } catch {
      setTorchSupported(false)
    }
  }

  if (!available) {
    return (
      <Alert color="orange" icon={<IconAlertTriangle size={18} />} title="Kamera kullanılamıyor">
        Kamera yalnızca HTTPS (veya localhost) üzerinden açılabilir. Kodu aşağıya elle
        girebilirsiniz.
      </Alert>
    )
  }

  const failed = status === 'denied' || status === 'error'

  return (
    <Stack>
      {status === 'denied' && (
        <Alert color="orange" icon={<IconAlertTriangle size={18} />} title="Kamera izni verilmedi">
          Tarayıcının adres çubuğundaki site ayarlarından kamera iznini açıp sayfayı yenileyin. O
          zamana kadar kodu aşağıya elle girebilirsiniz.
        </Alert>
      )}
      {status === 'error' && (
        <Alert color="red" icon={<IconAlertTriangle size={18} />} title="Kamera açılamadı">
          Kamera başka bir uygulama tarafından kullanılıyor olabilir. Kodu aşağıya elle
          girebilirsiniz.
        </Alert>
      )}
      <Paper
        withBorder
        p={0}
        pos="relative"
        bg="dark.9"
        style={{ overflow: 'hidden', display: failed ? 'none' : undefined }}
      >
        <Box
          component="video"
          ref={videoRef}
          muted
          playsInline
          w="100%"
          display="block"
          style={{ aspectRatio: '4 / 3', objectFit: 'cover' }}
        />
        <Group pos="absolute" top={8} right={8} gap={6}>
          {devices.length > 1 && (
            <Tooltip label="Kamerayı değiştir">
              <ActionIcon
                variant="filled"
                color="dark"
                aria-label="Kamerayı değiştir"
                onClick={switchCamera}
              >
                <IconCameraRotate size={18} />
              </ActionIcon>
            </Tooltip>
          )}
          {torchSupported && (
            <Tooltip label={torchOn ? 'Feneri kapat' : 'Feneri aç'}>
              <ActionIcon
                variant="filled"
                color={torchOn ? 'yellow' : 'dark'}
                aria-label={torchOn ? 'Feneri kapat' : 'Feneri aç'}
                onClick={() => void toggleTorch()}
              >
                {torchOn ? <IconBoltOff size={18} /> : <IconBolt size={18} />}
              </ActionIcon>
            </Tooltip>
          )}
        </Group>
        {status === 'starting' && (
          <Text c="gray.4" size="sm" pos="absolute" top="45%" w="100%" ta="center">
            Kamera açılıyor…
          </Text>
        )}
      </Paper>
    </Stack>
  )
}
