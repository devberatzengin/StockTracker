import { Alert, Badge, Button, Group, Modal, Stack, Text, Textarea } from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import { notifications } from '@mantine/notifications'
import { useState } from 'react'
import { getErrorMessage, isApiError } from '@/api/client'
import type { Movement } from '@/api/types'
import { usePermission } from '@/features/auth/authContext'
import { Permissions } from '@/features/auth/permissions'
import { describeMovement } from '@/features/movements/movementText'
import { useReverseMovement } from '@/features/movements/movementsApi'
import { canReverse } from '@/features/movements/reversal'

/** "Ters kayıt" / "Ters çevrildi" badges and, where allowed, the button that reverses the movement. */
export function MovementReversal({ movement }: { movement: Movement }) {
  const canMoveStock = usePermission(Permissions.CanMoveStock)
  const [opened, { open, close }] = useDisclosure()

  return (
    <>
      {movement.reversesMovementId && (
        <Badge size="sm" variant="outline" color="gray">
          Ters kayıt
        </Badge>
      )}
      {movement.reversedByMovementId && (
        <Badge size="sm" variant="outline" color="orange">
          Ters çevrildi
        </Badge>
      )}
      {canMoveStock && canReverse(movement) && (
        <Button size="compact-xs" variant="subtle" color="orange" onClick={open}>
          Ters kayıt
        </Button>
      )}
      <Modal opened={opened} onClose={close} title="Hareketi ters çevir">
        {opened && <ReverseForm movement={movement} onClose={close} />}
      </Modal>
    </>
  )
}

function ReverseForm({ movement, onClose }: { movement: Movement; onClose: () => void }) {
  const reverse = useReverseMovement()
  const [note, setNote] = useState('')
  const [noteError, setNoteError] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const submit = async () => {
    setError(null)
    setNoteError(note.trim() ? null : 'Ters kayıt için not zorunludur.')
    if (!note.trim()) return
    try {
      await reverse.mutateAsync({ id: movement.id, note: note.trim() })
      notifications.show({ color: 'green', message: 'Hareket ters çevrildi.' })
      onClose()
    } catch (e) {
      const fieldError = isApiError(e, 400) ? e.errors?.note?.[0] : undefined
      if (fieldError) setNoteError(fieldError)
      else setError(getErrorMessage(e))
    }
  }

  return (
    <Stack>
      <Text size="sm">{describeMovement(movement)}</Text>
      <Text size="sm" c="dimmed">
        Hareket silinmez; tersi yönde yeni bir hareket yazılır ve ikisi de geçmişte görünür. Bu işlem
        bir kez yapılabilir.
      </Text>
      {error && <Alert color="red">{error}</Alert>}
      <Textarea
        label="Not"
        description="Neden ters çevrildiğini yazın."
        required
        autosize
        minRows={2}
        maxLength={500}
        value={note}
        error={noteError}
        onChange={(e) => setNote(e.currentTarget.value)}
      />
      <Group justify="flex-end">
        <Button variant="default" onClick={onClose}>
          Vazgeç
        </Button>
        <Button color="orange" loading={reverse.isPending} onClick={submit}>
          Ters çevir
        </Button>
      </Group>
    </Stack>
  )
}
