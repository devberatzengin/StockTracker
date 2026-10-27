import {
  Alert,
  Box,
  Button,
  Group,
  Modal,
  Radio,
  SimpleGrid,
  Stack,
  Text,
  UnstyledButton,
} from '@mantine/core'
import { IconPrinter } from '@tabler/icons-react'
import { useState } from 'react'
import type { LabelTemplate } from '@/api/types'
import { usePrintLabels } from '@/features/labels/labelsApi'
import { isSheet, LABEL_TEMPLATES, type LabelTarget } from '@/features/labels/labelTemplates'
import { parseLineErrors } from '@/features/movements/lineErrors'

interface Props {
  opened: boolean
  onClose: () => void
  target: LabelTarget
  /** Shown in the title, e.g. "3 adet". */
  summary?: string
  defaultTemplate?: LabelTemplate
}

/** Template, start position on a half-used sheet, then a PDF to print at 100 %. */
export function LabelPrintModal({
  opened,
  onClose,
  target,
  summary,
  defaultTemplate = 'Sheet3x8',
}: Props) {
  return (
    <Modal
      opened={opened}
      onClose={onClose}
      title={`Etiket yazdır${summary ? ` · ${summary}` : ''}`}
      size="lg"
    >
      {opened && (
        <LabelPrintForm target={target} defaultTemplate={defaultTemplate} onDone={onClose} />
      )}
    </Modal>
  )
}

function LabelPrintForm({
  target,
  defaultTemplate,
  onDone,
}: {
  target: LabelTarget
  defaultTemplate: LabelTemplate
  onDone: () => void
}) {
  const print = usePrintLabels()
  const [template, setTemplate] = useState<LabelTemplate>(defaultTemplate)
  const [start, setStart] = useState(1)
  const [error, setError] = useState<string | null>(null)
  const info = LABEL_TEMPLATES[template]

  const submit = async () => {
    setError(null)
    const tab = window.open('', '_blank')
    try {
      await print.mutateAsync({
        template,
        startPosition: isSheet(template) ? start : 1,
        target,
        tab,
      })
      onDone()
    } catch (e) {
      tab?.close()
      const parsed = parseLineErrors(e)
      setError(Object.values(parsed.fields)[0] ?? parsed.general[0] ?? 'PDF oluşturulamadı.')
    }
  }

  return (
    <Stack>
      {error && <Alert color="red">{error}</Alert>}
      <Radio.Group
        label="Şablon"
        value={template}
        onChange={(value) => {
          setTemplate(value as LabelTemplate)
          setStart(1)
        }}
      >
        <Stack gap="xs" mt="xs">
          {(Object.keys(LABEL_TEMPLATES) as LabelTemplate[]).map((t) => (
            <Radio
              key={t}
              value={t}
              label={LABEL_TEMPLATES[t].label}
              description={LABEL_TEMPLATES[t].description}
            />
          ))}
        </Stack>
      </Radio.Group>

      {isSheet(template) && (
        <div>
          <Text size="sm" fw={500}>
            Başlangıç pozisyonu: {start}
          </Text>
          <Text size="xs" c="dimmed" mb="xs">
            Yarısı kullanılmış kâğıtta ilk boş hücreye tıklayın; öncekiler boş bırakılır.
          </Text>
          <SimpleGrid
            cols={info.columns}
            spacing={3}
            maw={info.columns * 44}
            aria-label="Etiket kâğıdı"
          >
            {Array.from({ length: info.columns * info.rows }, (_, i) => i + 1).map((cell) => (
              <UnstyledButton
                key={cell}
                onClick={() => setStart(cell)}
                aria-label={`Hücre ${cell}`}
                aria-pressed={cell === start}
              >
                <Box
                  h={info.rows > 8 ? 14 : 22}
                  style={{
                    borderRadius: 2,
                    fontSize: 9,
                    textAlign: 'center',
                    border: '1px solid var(--mantine-color-default-border)',
                    background:
                      cell < start
                        ? 'var(--mantine-color-default-hover)'
                        : cell === start
                          ? 'var(--mantine-color-blue-filled)'
                          : undefined,
                    color: cell === start ? 'white' : 'var(--mantine-color-dimmed)',
                  }}
                >
                  {info.rows > 8 ? '' : cell}
                </Box>
              </UnstyledButton>
            ))}
          </SimpleGrid>
        </div>
      )}

      <Alert color="blue" icon={<IconPrinter size={16} />}>
        Yazdırırken <b>%100 ölçek / gerçek boyut</b> kullanın; "sayfaya sığdır" kapalı olsun. Aksi
        halde etiketler kâğıttaki hücrelere denk gelmez.
      </Alert>

      <Group justify="flex-end">
        <Button variant="default" onClick={onDone}>
          Vazgeç
        </Button>
        <Button
          leftSection={<IconPrinter size={16} />}
          onClick={() => void submit()}
          loading={print.isPending}
        >
          PDF oluştur
        </Button>
      </Group>
    </Stack>
  )
}
