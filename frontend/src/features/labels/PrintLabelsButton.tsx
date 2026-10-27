import { Button, type ButtonProps } from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import { IconPrinter } from '@tabler/icons-react'
import type { LabelTemplate } from '@/api/types'
import { usePermission } from '@/features/auth/authContext'
import { Permissions } from '@/features/auth/permissions'
import { LabelPrintModal } from '@/features/labels/LabelPrintModal'
import type { LabelTarget } from '@/features/labels/labelTemplates'

interface Props extends Omit<ButtonProps, 'children'> {
  target: LabelTarget
  children?: string
  summary?: string
  defaultTemplate?: LabelTemplate
}

/** "Etiket yazdır" button with its modal; hidden without the move-stock permission. */
export function PrintLabelsButton({
  target,
  children = 'Etiket yazdır',
  summary,
  defaultTemplate,
  ...button
}: Props) {
  const allowed = usePermission(Permissions.CanMoveStock)
  const [opened, modal] = useDisclosure()
  if (!allowed) return null

  return (
    <>
      <Button
        leftSection={<IconPrinter size={16} />}
        variant="light"
        onClick={modal.open}
        {...button}
      >
        {children}
      </Button>
      <LabelPrintModal
        opened={opened}
        onClose={modal.close}
        target={target}
        summary={summary}
        defaultTemplate={defaultTemplate}
      />
    </>
  )
}
