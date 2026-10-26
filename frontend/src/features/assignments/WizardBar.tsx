import { Box, Button, Group } from '@mantine/core'
import { IconArrowLeft, IconArrowRight, IconCheck } from '@tabler/icons-react'

interface Props {
  onBack?: () => void
  onNext: () => void
  nextLabel?: string
  nextDisabled?: boolean
  /** The last step confirms instead of moving on. */
  confirm?: boolean
  loading?: boolean
}

/** Sticky bottom bar with large touch targets for the step-by-step flows. */
export function WizardBar({ onBack, onNext, nextLabel, nextDisabled, confirm, loading }: Props) {
  return (
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
          onClick={onBack}
          disabled={!onBack}
        >
          Geri
        </Button>
        <Button
          size="lg"
          rightSection={confirm ? undefined : <IconArrowRight size={18} />}
          leftSection={confirm ? <IconCheck size={18} /> : undefined}
          onClick={onNext}
          disabled={nextDisabled}
          loading={loading}
        >
          {nextLabel ?? (confirm ? 'Onayla' : 'İleri')}
        </Button>
      </Group>
    </Box>
  )
}
